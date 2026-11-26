// -----------------------------------------------------------------------
// <copyright file="IMapper.cs" company="Kitpymes">
// Copyright (c) Kitpymes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project docs folder for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace Kitpymes.Core.Mapper;

/// <summary>
/// Mapeo en memoria de <typeparamref name="TSource"/> a <typeparamref name="TDestination"/>, escrito a mano.
/// </summary>
/// <remarks>
/// Se inyecta tal cual (<c>IMapper&lt;Role, RoleDto&gt;</c>) y se registra como singleton con <c>LoadMapper</c>:
/// la implementación no debe guardar estado ni depender de servicios con ámbito (un <c>DbContext</c>).
/// Para consultas de EF Core se usa <see cref="IProjection{TSource, TDestination}"/>: una expresión con
/// <c>EF.Property</c> no vale en memoria. Las anotaciones de nulos (<c>IMapper&lt;Role?, RoleDto?&gt;</c>) sólo las mira el
/// compilador: en ejecución es el mismo contrato que <c>IMapper&lt;Role, RoleDto&gt;</c>, así que no pueden convivir dos clases.
/// </remarks>
/// <typeparam name="TSource">Tipo de origen.</typeparam>
/// <typeparam name="TDestination">Tipo de destino.</typeparam>
public interface IMapper<in TSource, out TDestination>
{
    /// <summary>
    /// Crea un <typeparamref name="TDestination"/> a partir de <paramref name="source"/>.
    /// </summary>
    /// <param name="source">Objeto de origen.</param>
    /// <returns>El objeto de destino.</returns>
    TDestination Map(TSource source);
}
