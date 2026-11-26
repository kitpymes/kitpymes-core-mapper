// -----------------------------------------------------------------------
// <copyright file="MapperVerifier.cs" company="Kitpymes">
// Copyright (c) Kitpymes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project docs folder for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace Kitpymes.Core.Mapper;

using System;
using System.Collections.Generic;
using Kitpymes.Core.Mapper.Internal;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Comprueba lo registrado con <c>LoadMapper</c>, para llamarlo al arrancar o en un test de arquitectura.
/// </summary>
public static class MapperVerifier
{
    /// <summary>
    /// Resuelve cada mapeo y proyección registrados y expande cada proyección (<c>Expand</c>). Así salen antes de la
    /// primera consulta los errores que, si no, aparecerían en ella: una dependencia que falta, una proyección anidada
    /// nula o no evaluable, un ciclo, o un contrato que alguien registró a mano después de <c>LoadMapper</c> con otra clase.
    /// </summary>
    /// <remarks>
    /// No ejecuta la proyección ni comprueba que EF Core la traduzca: eso lo prueba un test contra la base de datos.
    /// Lanza <see cref="InvalidOperationException"/> con todos los errores juntos en el mensaje y, si alguno vino de una
    /// excepción, éstas en un <see cref="AggregateException"/> como excepción interna. Un registro a mano posterior con la
    /// misma clase no lo detecta (el contrato sigue resolviéndose a esa clase).
    /// </remarks>
    /// <param name="provider">Proveedor de servicios ya construido.</param>
    public static void Verify(IServiceProvider provider)
    {
        // Sin ThrowIfNull propio: GetService ya lanza ArgumentNullException con el mismo nombre de parámetro.
        var registry = provider.GetService<MapperRegistry>() ?? throw new InvalidOperationException(MapperMessages.NotLoaded());
        var errors = new List<string>();
        var exceptions = new List<Exception>();

        foreach (var registered in registry.Contracts)
        {
            var contract = registered.Key;
            var type = registered.Value;

            try
            {
                var instance = provider.GetRequiredService(contract);

                if (instance.GetType() != type)
                {
                    errors.Add(MapperMessages.Replaced(contract, type, instance.GetType()));
                    continue;
                }

                if (contract.GetGenericTypeDefinition() == typeof(IProjection<,>))
                {
                    _ = ProjectionExpander.ExpandProjection(instance, contract);
                }
            }
            catch (Exception exception)
            {
                errors.Add(MapperMessages.Failed(contract, exception));
                exceptions.Add(exception);
            }
        }

        if (errors.Count > 0)
        {
            throw new InvalidOperationException(MapperMessages.VerifyFailed(errors), exceptions.Count == 0 ? null : new AggregateException(exceptions));
        }
    }
}
