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
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;

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
    /// Expande la expresión de una proyección, con caché por instancia.
    /// </summary>
    /// <param name="projection">Instancia que implementa IProjection de los tipos dados.</param>
    /// <param name="source">Tipo de origen.</param>
    /// <param name="destination">Tipo de destino.</param>
    /// <returns>La expresión expandida.</returns>
    internal static LambdaExpression ExpandProjection(object projection, Type source, Type destination)
        => Expand(projection, source, destination, new List<object>());

    /// <summary>
    /// Expande cualquier expresión, sin caché.
    /// </summary>
    /// <param name="expression">Expresión.</param>
    /// <returns>La expresión expandida.</returns>
    internal static Expression ExpandExpression(LambdaExpression expression)
        => new InlineVisitor(new List<object>()).Visit(expression);

    private static LambdaExpression Expand(object projection, Type source, Type destination, IReadOnlyList<object> chain)
    {
        var contract = typeof(IProjection<,>).MakeGenericType(source, destination);
        var cached = Cache.GetOrCreateValue(projection);

        if (cached.TryGetValue(contract, out var expression))
        {
            return expression;
        }

        if (chain.Any(p => ReferenceEquals(p, projection)))
        {
            throw new InvalidOperationException(MapperMessages.Cycle(chain, projection));
        }

        var property = contract.GetProperty(nameof(IProjection<object, object>.Selector))
            ?? throw new MissingMemberException(contract.Name, nameof(IProjection<object, object>.Selector));
        var selector = (LambdaExpression?)property.GetValue(projection)
            ?? throw new InvalidOperationException(MapperMessages.NullSelector(projection.GetType()));

        var expanded = (LambdaExpression)new InlineVisitor(new List<object>(chain) { projection }).Visit(selector);

        cached[contract] = expanded;

        return expanded;
    }

    private static object Evaluate(Expression expression, Type source, Type destination)
    {
        if (FreeParameterFinder.HasFreeParameters(expression))
        {
            throw new InvalidOperationException(MapperMessages.NotEvaluable(source, destination, expression.ToString()));
        }

        // Lo habitual es un campo de la clase de cierre (un parámetro de constructor capturado). Se compila sólo al
        // expandir, y el resultado de Expand queda en caché.
        var value = Expression.Lambda<Func<object?>>(Expression.Convert(expression, typeof(object))).Compile()();

        return value ?? throw new InvalidOperationException(MapperMessages.NullProjection(source, destination));
    }

    /// <summary>
    /// Cambia las llamadas a Inline por el cuerpo de su proyección (expandido a su vez).
    /// </summary>
    private sealed class InlineVisitor : ExpressionVisitor
    {
        private readonly IReadOnlyList<object> chain;

        /// <summary>
        /// Inicializa una nueva instancia de la clase <see cref="InlineVisitor"/>.
        /// </summary>
        /// <param name="chain">Proyecciones que se están expandiendo, para detectar ciclos.</param>
        internal InlineVisitor(IReadOnlyList<object> chain) => this.chain = chain;

        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            if (!node.Method.IsGenericMethod || node.Method.GetGenericMethodDefinition() != InlineDefinition)
            {
                return base.VisitMethodCall(node);
            }

            var types = node.Method.GetGenericArguments();
            var projection = Evaluate(node.Arguments[0], types[0], types[1]);
            var nested = Expand(projection, types[0], types[1], chain);
            var argument = Visit(node.Arguments[1]);

            return new ParameterReplacer(nested.Parameters[0], argument).Visit(nested.Body);
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
