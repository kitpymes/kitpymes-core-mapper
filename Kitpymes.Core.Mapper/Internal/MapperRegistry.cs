// -----------------------------------------------------------------------
// <copyright file="MapperRegistry.cs" company="Kitpymes">
// Copyright (c) Kitpymes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project docs folder for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace Kitpymes.Core.Mapper.Internal;

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Lo que ha registrado LoadMapper: cada contrato (IMapper o IProjection cerrado) con su clase. Se guarda como singleton
/// en los propios servicios, para que varias llamadas a LoadMapper lo compartan y MapperVerifier lo lea.
/// </summary>
internal sealed class MapperRegistry
{
    private readonly Dictionary<Type, Type> contracts = new ();

    /// <summary>
    /// Obtiene los contratos registrados, en orden de registro, con su clase.
    /// </summary>
    internal IReadOnlyList<KeyValuePair<Type, Type>> Contracts => contracts.ToList();

    /// <summary>
    /// Devuelve el registro de los servicios, creándolo si no existe.
    /// </summary>
    /// <param name="services">Servicios.</param>
    /// <returns>El registro.</returns>
    internal static MapperRegistry From(IServiceCollection services)
    {
        var existing = services.FirstOrDefault(d => d.ServiceType == typeof(MapperRegistry))?.ImplementationInstance;

        if (existing is MapperRegistry registry)
        {
            return registry;
        }

        registry = new MapperRegistry();
        services.AddSingleton(registry);
        return registry;
    }

    /// <summary>
    /// Registra una clase con sus contratos: un singleton por clase, y cada contrato apunta a esa instancia.
    /// </summary>
    /// <param name="services">Servicios.</param>
    /// <param name="type">Clase concreta.</param>
    /// <param name="interfaces">Contratos que implementa (al menos uno).</param>
    internal void Add(IServiceCollection services, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type type, IReadOnlyList<Type> interfaces)
    {
        var pending = new List<Type>();

        foreach (var contract in interfaces)
        {
            if (contracts.TryGetValue(contract, out var registered))
            {
                if (registered != type)
                {
                    throw new InvalidOperationException(MapperMessages.Duplicated(contract, registered, type));
                }

                continue;
            }

            if (services.Any(d => d.ServiceType == contract))
            {
                throw new InvalidOperationException(MapperMessages.RegisteredOutside(contract, type));
            }

            pending.Add(contract);
        }

        if (pending.Count == 0)
        {
            return;
        }

        if (!services.Any(d => d.ServiceType == type))
        {
            services.AddSingleton(type);
        }

        foreach (var contract in pending)
        {
            services.AddSingleton(contract, provider => provider.GetRequiredService(type));
            contracts.Add(contract, type);
        }
    }
}
