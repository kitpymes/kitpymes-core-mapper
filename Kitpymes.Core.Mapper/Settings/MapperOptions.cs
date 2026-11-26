// -----------------------------------------------------------------------
// <copyright file="MapperOptions.cs" company="Kitpymes">
// Copyright (c) Kitpymes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project docs folder for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace Kitpymes.Core.Mapper.Settings;

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

/// <summary>
/// Opciones para registrar los mapeos y proyecciones.
/// </summary>
public class MapperOptions
{
    internal const string ScanMessage = "Busca en los ensamblados, por reflexión, las clases que implementan IMapper o IProjection: no es compatible con trimming. Usar WithMap para cada clase.";

    /// <summary>
    /// Obtiene los ensamblados donde buscar.
    /// </summary>
    internal List<Assembly> Assemblies { get; } = new ();

    /// <summary>
    /// Obtiene las clases que se registran una a una.
    /// </summary>
    internal List<Type> Types { get; } = new ();

    /// <summary>
    /// Agrega ensamblados donde buscar las clases que implementan <see cref="IMapper{TSource, TDestination}"/> o
    /// <see cref="IProjection{TSource, TDestination}"/>. Las clases abstractas y las genéricas abiertas se ignoran.
    /// </summary>
    /// <param name="assemblies">Ensamblados.</param>
    /// <returns>MapperOptions.</returns>
    [RequiresUnreferencedCode(ScanMessage)]
    public MapperOptions WithAssemblies(params Assembly[] assemblies)
    {
        ArgumentNullException.ThrowIfNull(assemblies);

        foreach (var assembly in assemblies)
        {
            ArgumentNullException.ThrowIfNull(assembly, nameof(assemblies));

            if (!Assemblies.Contains(assembly))
            {
                Assemblies.Add(assembly);
            }
        }

        return this;
    }

    /// <summary>
    /// Agrega una clase concreta que implementa al menos un <see cref="IMapper{TSource, TDestination}"/> o
    /// <see cref="IProjection{TSource, TDestination}"/>, sin buscar en ensamblados.
    /// </summary>
    /// <typeparam name="TMap">Clase del mapeo o de la proyección.</typeparam>
    /// <returns>MapperOptions.</returns>
    public MapperOptions WithMap<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces | DynamicallyAccessedMemberTypes.PublicConstructors)] TMap>()
        where TMap : class
    {
        if (!Types.Contains(typeof(TMap)))
        {
            Types.Add(typeof(TMap));
        }

        return this;
    }
}
