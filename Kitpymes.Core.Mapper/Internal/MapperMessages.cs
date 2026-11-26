// -----------------------------------------------------------------------
// <copyright file="MapperMessages.cs" company="Kitpymes">
// Copyright (c) Kitpymes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project docs folder for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace Kitpymes.Core.Mapper.Internal;

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Mensajes de error de la librería.
/// </summary>
internal static class MapperMessages
{
    internal static string InlineExecuted(Type source, Type destination)
        => $"Inline de IProjection<{source.Name}, {destination.Name}> se ha ejecutado: la expresión no se ha expandido. Usar ProjectTo o Expand antes de ejecutar la consulta.";

    internal static string NotEvaluable(Type contract, string expression)
        => $"La proyección anidada {Name(contract)} no se puede evaluar sin los parámetros de la expresión: {expression}. Usar un campo, un parámetro de constructor o una constante.";

    internal static string NullProjection(Type contract)
        => $"La proyección anidada {Name(contract)} es nula.";

    internal static string InlineAsMethodGroup(Type contract)
        => $"Inline de {Name(contract)} se ha usado sin lambda (Select(proyeccion.Inline)): no se puede sustituir. Escribir Select(x => proyeccion.Inline(x)).";

    internal static string NullSelector(Type projection)
        => $"Selector de {projection.FullName} es nulo.";

    internal static string Cycle(IEnumerable<KeyValuePair<object, Type>> chain, KeyValuePair<object, Type> repeated)
        => $"Ciclo de proyecciones: {string.Join(" -> ", chain.Append(repeated).Select(p => $"{p.Key.GetType().Name} ({Name(p.Value)})"))}.";

    internal static string NotConcrete(Type type)
        => $"{type.FullName} es abstracta o genérica abierta: WithMap necesita una clase concreta.";

    internal static string NotSingleton(Type type, ServiceLifetime lifetime)
        => $"{type.FullName} ya está registrado como {lifetime}: los mapeos y proyecciones son singleton. Quitar ese registro.";

    internal static string TypesNotLoaded(string assembly)
        => $"No se pueden cargar todos los tipos del ensamblado {assembly}: un mapeo o proyección podría quedarse sin registrar. Ver las excepciones internas.";

    internal static string NoContracts(Type type)
        => $"{type.FullName} no implementa IMapper<,> ni IProjection<,>.";

    internal static string Duplicated(Type service, Type registered, Type other)
        => $"{Name(service)} ya lo implementa {registered.FullName}; {other.FullName} también. Sólo puede haber un mapeo por par de tipos.";

    internal static string RegisteredOutside(Type service, Type type)
        => $"{Name(service)} ya está registrado en los servicios fuera de LoadMapper; {type.FullName} también lo implementa. Quitar uno de los dos.";

    internal static string NotLoaded()
        => "No se ha llamado a LoadMapper en estos servicios.";

    internal static string Replaced(Type service, Type expected, Type actual)
        => $"{Name(service)} se resuelve a {actual.FullName} y no a {expected.FullName}: alguien lo ha registrado a mano después de LoadMapper.";

    internal static string Failed(Type service, Exception exception)
        => $"{Name(service)}: {exception.GetType().Name}: {exception.Message}";

    internal static string VerifyFailed(IEnumerable<string> errors)
        => "La configuración del mapper tiene errores:" + Environment.NewLine + string.Join(Environment.NewLine, errors.Select(e => "- " + e));

    internal static string Name(Type type)
        => type.IsGenericType
            ? $"{type.Name.Substring(0, type.Name.IndexOf('`', StringComparison.Ordinal))}<{string.Join(", ", type.GetGenericArguments().Select(Name))}>"
            : type.Name;
}
