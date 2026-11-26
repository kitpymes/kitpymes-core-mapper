// -----------------------------------------------------------------------
// <copyright file="IProjection.cs" company="Kitpymes">
// Copyright (c) Kitpymes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project docs folder for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace Kitpymes.Core.Mapper;

using System;
using System.Linq.Expressions;

/// <summary>
/// Proyección de <typeparamref name="TSource"/> a <typeparamref name="TDestination"/> como expresión, para
/// <c>Select</c> de un <see cref="System.Linq.IQueryable{T}"/> (EF Core la traduce a SQL).
/// </summary>
/// <remarks>
/// Se registra como singleton con <c>LoadMapper</c>. <see cref="Selector"/> tiene que devolver la misma expresión en
/// cada lectura (no puede depender de estado que cambie): <c>Expand</c> la guarda en caché por instancia.
/// Dentro de la expresión, otra proyección se usa con <c>Inline</c>.
/// </remarks>
/// <typeparam name="TSource">Tipo de origen.</typeparam>
/// <typeparam name="TDestination">Tipo de destino.</typeparam>
public interface IProjection<TSource, TDestination>
{
    /// <summary>
    /// Obtiene la expresión que construye el destino a partir del origen.
    /// </summary>
    Expression<Func<TSource, TDestination>> Selector { get; }
}
