// -----------------------------------------------------------------------
// <copyright file="MapperExtensions.cs" company="Kitpymes">
// Copyright (c) Kitpymes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project docs folder for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace Kitpymes.Core.Mapper;

using System;
using System.Collections.Generic;

/// <summary>
/// Extensiones de <see cref="IMapper{TSource, TDestination}"/>.
/// </summary>
public static class MapperExtensions
{
    /// <summary>
    /// Mapea cada elemento, en orden, a una lista nueva.
    /// </summary>
    /// <typeparam name="TSource">Tipo de origen.</typeparam>
    /// <typeparam name="TDestination">Tipo de destino.</typeparam>
    /// <param name="mapper">Mapeo que se aplica a cada elemento.</param>
    /// <param name="sources">Elementos de origen. Cada elemento se pasa tal cual, también si es nulo.</param>
    /// <returns>Una lista nueva con los elementos mapeados.</returns>
    public static IReadOnlyList<TDestination> MapAll<TSource, TDestination>(this IMapper<TSource, TDestination> mapper, IEnumerable<TSource> sources)
    {
        ArgumentNullException.ThrowIfNull(mapper);
        ArgumentNullException.ThrowIfNull(sources);

        var result = sources is ICollection<TSource> collection ? new List<TDestination>(collection.Count) : new List<TDestination>();

        foreach (var source in sources)
        {
            result.Add(mapper.Map(source));
        }

        return result;
    }
}
