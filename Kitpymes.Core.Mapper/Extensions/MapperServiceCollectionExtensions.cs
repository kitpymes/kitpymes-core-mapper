// -----------------------------------------------------------------------
// <copyright file="MapperServiceCollectionExtensions.cs" company="Kitpymes">
// Copyright (c) Kitpymes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project docs folder for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace Kitpymes.Core.Mapper.Extensions;

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using Kitpymes.Core.Mapper.Internal;
using Kitpymes.Core.Mapper.Settings;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Clase de extensión <c>MapperServiceCollectionExtensions</c>.
/// Contiene las extensiones de los servicios del mapper.
/// </summary>
public static class MapperServiceCollectionExtensions
{
    /// <summary>
    /// Registra los mapeos y proyecciones.
    /// </summary>
    /// <remarks>
    /// <para>Cada clase se registra como singleton una sola vez, y cada <see cref="IMapper{TSource, TDestination}"/> e
    /// <see cref="IProjection{TSource, TDestination}"/> que implementa apunta a esa instancia. Orden estable: por nombre
    /// completo dentro de cada ensamblado. Llamarlo dos veces con lo mismo no duplica nada.</para>
    /// <para>Lanza <see cref="InvalidOperationException"/> si dos clases implementan el mismo contrato, o si el contrato ya
    /// estaba registrado fuera de <c>LoadMapper</c>. Un registro a mano hecho después gana sin aviso (es como funciona la
    /// inyección de dependencias): lo detecta <see cref="MapperVerifier.Verify"/>.</para>
    /// </remarks>
    /// <param name="services">Colección de servicios.</param>
    /// <param name="options">Configuración.</param>
    /// <returns>IServiceCollection.</returns>
    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Sólo busca en los ensamblados pasados a WithAssemblies, que ya avisa.")]
    [UnconditionalSuppressMessage("Trimming", "IL2072", Justification = "Los tipos salen de WithMap (anotado) o de WithAssemblies, que ya avisa.")]
    public static IServiceCollection LoadMapper(this IServiceCollection services, Action<MapperOptions> options)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(options);

        var configured = new MapperOptions();
        options(configured);

        var registry = MapperRegistry.From(services);

        foreach (var type in configured.Types)
        {
            var contracts = Contracts(type);

            if (type.IsAbstract || type.ContainsGenericParameters || contracts.Count == 0)
            {
                throw new ArgumentException(MapperMessages.NoContracts(type), nameof(options));
            }

            registry.Add(services, type, contracts);
        }

        foreach (var assembly in configured.Assemblies)
        {
            foreach (var type in LoadableTypes(assembly).Where(IsCandidate).OrderBy(t => t.FullName, StringComparer.Ordinal))
            {
                var contracts = Contracts(type);

                if (contracts.Count > 0)
                {
                    registry.Add(services, type, contracts);
                }
            }
        }

        return services;
    }

    private static bool IsCandidate(Type type)
        => type.IsClass && !type.IsAbstract && !type.ContainsGenericParameters;

    private static List<Type> Contracts([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)] Type type)
        => type.GetInterfaces()
            .Where(i => i.IsGenericType)
            .Where(i => i.GetGenericTypeDefinition() == typeof(IMapper<,>) || i.GetGenericTypeDefinition() == typeof(IProjection<,>))
            .OrderBy(i => i.ToString(), StringComparer.Ordinal)
            .ToList();

    [RequiresUnreferencedCode(MapperOptions.ScanMessage)]
    private static IEnumerable<Type> LoadableTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException exception)
        {
            return exception.Types.OfType<Type>();
        }
    }
}
