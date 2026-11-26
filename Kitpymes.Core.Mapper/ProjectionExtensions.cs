// -----------------------------------------------------------------------
// <copyright file="ProjectionExtensions.cs" company="Kitpymes">
// Copyright (c) Kitpymes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project docs folder for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace Kitpymes.Core.Mapper;

using System;
using System.Linq;
using System.Linq.Expressions;
using Kitpymes.Core.Mapper.Internal;

/// <summary>
/// Extensiones de <see cref="IProjection{TSource, TDestination}"/>: proyectar un <see cref="IQueryable{T}"/>,
/// usar una proyección dentro de otra y obtener la expresión final.
/// </summary>
public static class ProjectionExtensions
{
    /// <summary>
    /// Proyecta la consulta: <c>source.Select(projection.Expand())</c>.
    /// </summary>
    /// <remarks>
    /// Dentro de <c>EF.CompileQuery</c> o <c>EF.CompileAsyncQuery</c> no se puede llamar a este método (EF no lo conoce):
    /// usar <c>Select(projection.Expand())</c>.
    /// </remarks>
    /// <typeparam name="TSource">Tipo de origen.</typeparam>
    /// <typeparam name="TDestination">Tipo de destino.</typeparam>
    /// <param name="source">Consulta de origen.</param>
    /// <param name="projection">Proyección.</param>
    /// <returns>La consulta proyectada.</returns>
    public static IQueryable<TDestination> ProjectTo<TSource, TDestination>(this IQueryable<TSource> source, IProjection<TSource, TDestination> projection)
    {
        ArgumentNullException.ThrowIfNull(source);

        return source.Select(projection.Expand());
    }

    /// <summary>
    /// Devuelve la expresión de la proyección con las proyecciones anidadas (<see cref="Inline"/>) sustituidas por su cuerpo,
    /// lista para <c>Select</c> de EF Core.
    /// </summary>
    /// <remarks>
    /// El resultado se guarda en caché por instancia de la proyección. Lanza <see cref="InvalidOperationException"/> si
    /// una proyección anidada no se puede evaluar sin los parámetros de la expresión, es nula o forma un ciclo.
    /// </remarks>
    /// <typeparam name="TSource">Tipo de origen.</typeparam>
    /// <typeparam name="TDestination">Tipo de destino.</typeparam>
    /// <param name="projection">Proyección.</param>
    /// <returns>La expresión sin llamadas a <see cref="Inline"/>.</returns>
    public static Expression<Func<TSource, TDestination>> Expand<TSource, TDestination>(this IProjection<TSource, TDestination> projection)
    {
        ArgumentNullException.ThrowIfNull(projection);

        return (Expression<Func<TSource, TDestination>>)ProjectionExpander.ExpandProjection(projection, typeof(TSource), typeof(TDestination));
    }

    /// <summary>
    /// Sustituye en cualquier expresión las llamadas a <see cref="Inline"/> por el cuerpo de su proyección. Para un
    /// <c>Select</c> escrito a mano que usa proyecciones: <c>query.Select(((Expression&lt;Func&lt;User, X&gt;&gt;)(u => ...)).Expand())</c>.
    /// </summary>
    /// <remarks>Sin caché: cada llamada recorre la expresión.</remarks>
    /// <typeparam name="TDelegate">Tipo del delegado de la expresión.</typeparam>
    /// <param name="expression">Expresión.</param>
    /// <returns>La expresión sin llamadas a <see cref="Inline"/>; la misma instancia si no tenía ninguna.</returns>
    public static Expression<TDelegate> Expand<TDelegate>(this Expression<TDelegate> expression)
    {
        ArgumentNullException.ThrowIfNull(expression);

        return (Expression<TDelegate>)ProjectionExpander.ExpandExpression(expression);
    }

    /// <summary>
    /// Marca, dentro de la expresión de una proyección, el uso de otra proyección: <c>Role = roles.Inline(u.Role)</c>.
    /// <c>Expand</c> (y por tanto <c>ProjectTo</c>) la sustituye por el cuerpo de <paramref name="projection"/>.
    /// </summary>
    /// <remarks>
    /// <para>No se ejecuta nunca: si llega a ejecutarse es que la expresión no se expandió, y lanza
    /// <see cref="InvalidOperationException"/>.</para>
    /// <para><paramref name="projection"/> tiene que poder evaluarse sin los parámetros de la expresión (un campo o
    /// parámetro de constructor capturado, una constante). <paramref name="source"/> se copia en cada uso que haga la
    /// proyección anidada de su parámetro: usar una navegación (<c>u.Role</c>) o un parámetro, no una subconsulta
    /// (<c>u.Roles.First()</c>), que EF repetiría por cada propiedad.</para>
    /// <para>Una navegación opcional se escribe <c>u.Role == null ? null : roles.Inline(u.Role)</c> (un árbol de
    /// expresión no admite <c>?.</c>).</para>
    /// </remarks>
    /// <typeparam name="TSource">Tipo de origen.</typeparam>
    /// <typeparam name="TDestination">Tipo de destino.</typeparam>
    /// <param name="projection">Proyección anidada.</param>
    /// <param name="source">Origen de la proyección anidada.</param>
    /// <returns>Nada: siempre lanza si se ejecuta.</returns>
    public static TDestination Inline<TSource, TDestination>(this IProjection<TSource, TDestination> projection, TSource source)
        => throw new InvalidOperationException(MapperMessages.InlineExecuted(typeof(TSource), typeof(TDestination)));
}
