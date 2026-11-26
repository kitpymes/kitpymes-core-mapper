// Modelo y mapeos correctos: los escanean los tests. Imitan el uso real del backend (usuarios, roles y permisos).
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace Kitpymes.Core.Mapper.Tests.Ok
{
    public abstract class Person
    {
        public Guid Id { get; set; }

        public string FullName { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;
    }

    public sealed class User : Person
    {
        public string Password { get; set; } = string.Empty;

        public Role? Role { get; set; }
    }

    public sealed class Role
    {
        public Guid Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public List<Permission> Permissions { get; set; } = new ();
    }

    public sealed class Permission
    {
        public Guid Id { get; set; }

        public string Name { get; set; } = string.Empty;
    }

    public sealed class PermissionDto
    {
        public required Guid Id { get; init; }

        public required string Name { get; init; }
    }

    public sealed class RoleDto
    {
        public required Guid Id { get; init; }

        public required string Name { get; init; }

        public required IEnumerable<PermissionDto> Permissions { get; init; }
    }

    public sealed class UserDto
    {
        public required Guid Id { get; init; }

        public required string Email { get; init; }

        public required RoleDto? Role { get; init; }
    }

    public sealed class PersonDto
    {
        public required Guid Id { get; init; }

        public required string FullName { get; init; }
    }

    public sealed class UserSession
    {
        public required string Email { get; init; }

        public required string? Role { get; init; }
    }

    public sealed class PermissionDtoProjection : IProjection<Permission, PermissionDto>
    {
        public Expression<Func<Permission, PermissionDto>> Selector { get; } = p => new PermissionDto { Id = p.Id, Name = p.Name };
    }

    public sealed class RoleDtoProjection(IProjection<Permission, PermissionDto> permissions) : IProjection<Role, RoleDto>
    {
        public Expression<Func<Role, RoleDto>> Selector => r => new RoleDto
        {
            Id = r.Id,
            Name = r.Name,
            Permissions = r.Permissions.Select(p => permissions.Inline(p)),
        };
    }

    public sealed class UserDtoProjection(IProjection<Role, RoleDto> roles) : IProjection<User, UserDto>
    {
        public Expression<Func<User, UserDto>> Selector => u => new UserDto
        {
            Id = u.Id,
            Email = u.Email,
            Role = u.Role == null ? null : roles.Inline(u.Role),
        };
    }

    public sealed class PersonDtoProjection : IProjection<Person, PersonDto>
    {
        public Expression<Func<Person, PersonDto>> Selector => p => new PersonDto { Id = p.Id, FullName = p.FullName };
    }

    public sealed class PermissionDtoMapper : IMapper<Permission, PermissionDto>
    {
        public PermissionDto Map(Permission source) => new () { Id = source.Id, Name = source.Name };
    }

    public sealed class RoleDtoMapper(IMapper<Permission, PermissionDto> permissions) : IMapper<Role, RoleDto>
    {
        public RoleDto Map(Role source) => new () { Id = source.Id, Name = source.Name, Permissions = permissions.MapAll(source.Permissions) };
    }

    /// <summary>Implementa las dos interfaces para el mismo par: tiene que ser una sola instancia.</summary>
    public sealed class UserSessionMap : IMapper<User, UserSession>, IProjection<User, UserSession>
    {
        public Expression<Func<User, UserSession>> Selector { get; } = u => new UserSession { Email = u.Email, Role = u.Role == null ? null : u.Role.Name };

        public UserSession Map(User source) => new () { Email = source.Email, Role = source.Role?.Name };
    }

    /// <summary>Se ignora al escanear: abstracta.</summary>
    public abstract class AbstractMap : IMapper<Role, string>
    {
        public abstract string Map(Role source);
    }

    /// <summary>Se ignora al escanear: genérica abierta.</summary>
    public sealed class IdentityMap<T> : IMapper<T, T>
    {
        public T Map(T source) => source;
    }

    /// <summary>Se ignora al escanear: no implementa nada.</summary>
    public sealed class NotAMap
    {
    }
}
