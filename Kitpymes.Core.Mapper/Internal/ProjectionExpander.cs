// -----------------------------------------------------------------------
// <copyright file="ProjectionExpander.cs" company="Kitpymes">
// Copyright (c) Kitpymes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project docs folder for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace Kitpymes.Core.Mapper.Internal;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;

/// <summary>
/// Sustituye las llamadas a <see cref="ProjectionExtensions.Inline"/> por el cuerpo de su proyección.
/// </summary>
internal static class ProjectionExpander
{
    private static readonly MethodInfo InlineDefinition = typeof(ProjectionExtensions).GetMethod(nameof(ProjectionExtensions.Inline))
        ?? throw new MissingMethodException(nameof(ProjectionExtensions), nameof(ProjectionExtensions.Inline));

    /// <summary>
    /// Expresiones ya expandidas, por instancia y por contrato: una misma clase puede implementar varias IProjection.
    /// </summary>
    private static readonly ConditionalWeakTable<object, ConcurrentDictionary<Type, LambdaExpression>> Cache = new ();

    /// <summary>
    /// Expande la expresión de una proyección, con caché por instancia y contrato.
    /// </summary>
    /// <param name="projection">Instancia que implementa <paramref name="contract"/>.</param>
    /// <param name="contract">IProjection cerrado que se expande.</param>
    /// <returns>La expresión expandida.</returns>
    internal static LambdaExpression ExpandProjection(object projection, Type contract)
        => Expand(projection, contract, new List<KeyValuePair<object, Type>>());

    /// <summary>
    /// Expande cualquier expresión, sin caché.
    /// </summary>
    /// <param name="expression">Expresión.</param>
    /// <returns>La expresión expandida.</returns>
    internal static Expression ExpandExpression(LambdaExpression expression)
        => new InlineVisitor(new List<KeyValuePair<object, Type>>()).Visit(expression);

    private static LambdaExpression Expand(object projection, Type contract, IReadOnlyList<KeyValuePair<object, Type>> chain)
    {
        var cached = Cache.GetOrCreateValue(projection);

        if (cached.TryGetValue(contract, out var expression))
        {
            return expression;
        }

        // El ciclo es la misma instancia con el mismo contrato: una clase puede implementar dos proyecciones y usar una en la otra.
        if (chain.Any(p => ReferenceEquals(p.Key, projection) && p.Value == contract))
        {
            throw new InvalidOperationException(MapperMessages.Cycle(chain.Select(p => p.Key), projection));
        }

        var expanded = (LambdaExpression)new InlineVisitor(new List<KeyValuePair<object, Type>>(chain) { new (projection, contract) })
            .Visit(ReadSelector(projection, contract));

        // Si otro hilo la expandió a la vez, se devuelve la suya: así Expand da siempre la misma instancia.
        return cached.GetOrAdd(contract, expanded);
    }

    [UnconditionalSuppressMessage("Trimming", "IL2070", Justification = "contract es siempre IProjection<,> cerrado, de esta librería: Selector se usa a través de la interfaz y no se recorta.")]
    private static LambdaExpression ReadSelector(object projection, Type contract)
    {
        var property = contract.GetProperty(nameof(IProjection<object, object>.Selector))
            ?? throw new MissingMemberException(contract.Name, nameof(IProjection<object, object>.Selector));

        object? selector;

        try
        {
            selector = property.GetValue(projection);
        }
        catch (TargetInvocationException exception)
        {
            // Que se vea la excepción del propio Selector, no el envoltorio de la reflexión.
            ExceptionDispatchInfo.Throw(exception.InnerException!);
            throw;
        }

        return (LambdaExpression?)selector ?? throw new InvalidOperationException(MapperMessages.NullSelector(projection.GetType()));
    }

    private static object Evaluate(Expression expression, Type contract)
    {
        if (FreeParameterFinder.HasFreeParameters(expression))
        {
            throw new InvalidOperationException(MapperMessages.NotEvaluable(contract, expression.ToString()));
        }

        // Lo habitual es un campo de la clase de cierre (un parámetro de constructor capturado). Se compila sólo al
        // expandir una proyección, y su resultado queda en caché.
        var value = Expression.Lambda<Func<object?>>(Expression.Convert(expression, typeof(object))).Compile()();

        return value ?? throw new InvalidOperationException(MapperMessages.NullProjection(contract));
    }

    /// <summary>
    /// Cambia las llamadas a Inline por el cuerpo de su proyección (expandido a su vez).
    /// </summary>
    private sealed class InlineVisitor : ExpressionVisitor
    {
        private readonly IReadOnlyList<KeyValuePair<object, Type>> chain;

        /// <summary>
        /// Inicializa una nueva instancia de la clase <see cref="InlineVisitor"/>.
        /// </summary>
        /// <param name="chain">Proyecciones que se están expandiendo, para detectar ciclos.</param>
        internal InlineVisitor(IReadOnlyList<KeyValuePair<object, Type>> chain) => this.chain = chain;

        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            if (!node.Method.IsGenericMethod || node.Method.GetGenericMethodDefinition() != InlineDefinition)
            {
                return base.VisitMethodCall(node);
            }

            // El primer parámetro de Inline<TS, TD> es el IProjection<TS, TD> cerrado.
            var contract = node.Method.GetParameters()[0].ParameterType;
            var projection = Evaluate(node.Arguments[0], contract);
            var nested = Expand(projection, contract, chain);
            var argument = Visit(node.Arguments[1]);

            return new ParameterReplacer(nested.Parameters[0], argument).Visit(nested.Body);
        }

        protected override Expression VisitConstant(ConstantExpression node)
        {
            // Select(roles.Inline) sin lambda: el árbol lleva el método como constante, que nadie puede sustituir.
            if (node.Value is MethodInfo method && method.IsGenericMethod && method.GetGenericMethodDefinition() == InlineDefinition)
            {
                throw new InvalidOperationException(MapperMessages.InlineAsMethodGroup(method.GetParameters()[0].ParameterType));
            }

            return base.VisitConstant(node);
        }
    }

    /// <summary>
    /// Cambia un parámetro por una expresión.
    /// </summary>
    private sealed class ParameterReplacer : ExpressionVisitor
    {
        private readonly ParameterExpression parameter;

        private readonly Expression replacement;

        /// <summary>
        /// Inicializa una nueva instancia de la clase <see cref="ParameterReplacer"/>.
        /// </summary>
        /// <param name="parameter">Parámetro que se cambia.</param>
        /// <param name="replacement">Expresión que lo sustituye.</param>
        internal ParameterReplacer(ParameterExpression parameter, Expression replacement)
        {
            this.parameter = parameter;
            this.replacement = replacement;
        }

        protected override Expression VisitParameter(ParameterExpression node)
            => node == parameter ? replacement : base.VisitParameter(node);
    }

    /// <summary>
    /// Busca parámetros que no declara ninguna lambda de la propia expresión.
    /// </summary>
    private sealed class FreeParameterFinder : ExpressionVisitor
    {
        private readonly HashSet<ParameterExpression> declared = new ();

        private bool found;

        internal static bool HasFreeParameters(Expression expression)
        {
            var finder = new FreeParameterFinder();
            finder.Visit(expression);
            return finder.found;
        }

        protected override Expression VisitLambda<T>(Expression<T> node)
        {
            declared.UnionWith(node.Parameters);
            return base.VisitLambda(node);
        }

        protected override Expression VisitParameter(ParameterExpression node)
        {
            if (!declared.Contains(node))
            {
                found = true;
            }

            return base.VisitParameter(node);
        }
    }
}
