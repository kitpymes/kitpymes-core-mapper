# Kitpymes.Core.Mapper

**Mapeo explícito para .NET 10, sin reflexión.** Cada mapeo es una clase escrita a mano y registrada en la inyección de dependencias. Para EF Core, la proyección es una expresión que se traduce a SQL. Una proyección puede usar otra dentro.

- `IMapper<TSource, TDestination>`: mapea en memoria (`Map`).
- `IProjection<TSource, TDestination>`: proyecta un `IQueryable` (`Selector`, una expresión).
- Depende sólo de `Microsoft.Extensions.DependencyInjection.Abstractions`. No trae ASP.NET Core.

## Instalación

```
dotnet add package Kitpymes.Core.Mapper
```

## Uso

### Mapear en memoria

```csharp
public sealed class RoleDtoMapper(IMapper<Permission, PermissionDto> permissions) : IMapper<Role, RoleDto>
{
    public RoleDto Map(Role source) => new ()
    {
        Id = source.Id,
        Name = source.Name,
        Permissions = permissions.MapAll(source.Permissions),
    };
}

// En el manejador: se inyecta el par que hace falta.
public sealed class CreateRoleHandler(IMapper<Role, RoleDto> mapper) { /* ... */ var dto = mapper.Map(role); }
```

### Proyectar con EF Core

```csharp
public sealed class RoleDtoProjection(IProjection<Permission, PermissionDto> permissions) : IProjection<Role, RoleDto>
{
    public Expression<Func<Role, RoleDto>> Selector => r => new RoleDto
    {
        Id = r.Id,
        Name = r.Name,
        IsActive = EF.Property<bool>(r, "IsActive"),          // propiedad sombra: sólo vale en una consulta
        Permissions = r.Permissions.Select(p => permissions.Inline(p)),
    };
}

public sealed class UserDtoProjection(IProjection<Role, RoleDto> roles) : IProjection<User, UserDto>
{
    public Expression<Func<User, UserDto>> Selector => u => new UserDto
    {
        Id = u.Id,
        Email = u.Email,
        Role = u.Role == null ? null : roles.Inline(u.Role),   // navegación opcional: un árbol de expresión no admite ?.
    };
}

var page = await context.Users.ProjectTo(userDtoProjection)
    .OrderBy(u => u.Email).Skip(20).Take(10).ToListAsync(ct);
```

`ProjectTo(p)` es `Select(p.Expand())`. `Expand` cambia cada `Inline` por el cuerpo de su proyección y guarda el resultado por instancia. Un `Select` escrito a mano que use `Inline` se expande con `Expand()` sobre la propia expresión.

### Registro y comprobación al arrancar

```csharp
builder.Services.LoadMapper(o => o.WithAssemblies(typeof(RoleDtoMapper).Assembly));
// o una a una, sin buscar en ensamblados: o.WithMap<RoleDtoMapper>().WithMap<RoleDtoProjection>()

var app = builder.Build();
MapperVerifier.Verify(app.Services);   // y lo mismo en un test de arquitectura
```

- Cada clase se registra como **singleton** una sola vez. Cada `IMapper`/`IProjection` que implementa apunta a esa instancia. Un mapeo no puede depender de servicios con ámbito (un `DbContext`).
- `LoadMapper` lanza si dos clases implementan el mismo par, o si el par ya estaba registrado fuera de `LoadMapper`.
- `MapperVerifier.Verify` resuelve todo lo registrado y expande cada proyección. Así salen al arrancar una dependencia que falta, una proyección anidada nula o un ciclo, y un par que alguien registró a mano después. No comprueba que EF traduzca la consulta: eso lo prueba un test contra la base de datos.
- Si falta un mapeo que alguien inyecta, lo detecta el contenedor al construirse con `ValidateOnBuild` (activo por defecto en Development).

## Errores al compilar

El mapeo es código C#: un tipo o un nombre equivocado no compila. Para que tampoco compile **olvidar un miembro**, declara los del destino como `required`:

```csharp
public sealed class RoleDto
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
}
```

Con eso, un `new RoleDto { Id = r.Id }` da el error CS9035, también dentro de la expresión de una proyección. La librería no puede imponerlo: depende de cómo se escriban los DTO.

## Lo que hay que saber

- **`IMapper` e `IProjection` van separados a propósito.** Una expresión con `EF.Property` lanza fuera de una consulta. Si un par se usa en los dos sitios, hay dos clases, y un test debe comprobar que dan lo mismo.
- **El argumento de `Inline` se copia** en cada uso que la proyección anidada hace de su parámetro. Usa una navegación (`u.Role`) o un parámetro, no una subconsulta (`u.Roles.First()`), que EF repetiría una vez por propiedad.
- **La proyección anidada se evalúa al expandir:** tiene que ser un campo, un parámetro de constructor o una constante, no algo que dependa del parámetro de la expresión.
- **`Selector` tiene que devolver lo mismo en cada lectura**, porque la expansión se guarda por instancia.
- **`EF.CompileQuery` no conoce `ProjectTo`:** dentro, usa `Select(projection.Expand())`.
- **Trimming y NativeAOT:** `WithAssemblies` busca por reflexión y no es compatible. `WithMap<T>()` no busca en ensamblados.

## Desde AutoMapper

| AutoMapper | Kitpymes.Core.Mapper |
|---|---|
| `Profile` con `CreateMap<A, B>()` | una clase `IMapper<A, B>` y, si se proyecta, otra `IProjection<A, B>` |
| `.ForMember(d => d.X, m => m.MapFrom(s => ...))` | `X = ...` en el inicializador |
| `IMapper.Map<B>(a)` | `IMapper<A, B>` inyectado: `mapper.Map(a)` |
| `ProjectTo<B>(configuration)` | `ProjectTo(projection)` con `IProjection<A, B>` inyectada |
| mapeo anidado automático | `Inline` en la proyección; en memoria, el `IMapper` anidado inyectado |
| `services.AddAutoMapper(assembly)` | `services.LoadMapper(o => o.WithAssemblies(assembly))` |
| `AssertConfigurationIsValid()` | `MapperVerifier.Verify(provider)` y `required` en los DTO |

## Extensión

Un `IMapper` puede generarlo otra herramienta. Con [Mapperly](https://github.com/riok/mapperly) (MIT), el generador escribe el cuerpo de `Map` al compilar:

```csharp
[Mapper]
public sealed partial class PermissionMapperly : IMapper<Permission, PermissionSummary>
{
    public partial PermissionSummary Map(Permission source);
}
```

## Rendimiento

Pendiente: los números del benchmark contra AutoMapper se añaden antes de publicar.

## Licencia

MIT.
