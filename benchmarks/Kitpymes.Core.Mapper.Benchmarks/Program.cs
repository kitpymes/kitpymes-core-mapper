// Comparación con AutoMapper 12.0.1: mapear en memoria (rol con 10 permisos) y proyectar con EF Core (Sqlite en
// memoria, 100 usuarios con rol y permisos). La línea base es el mismo código escrito a mano sin librería.
// Se ejecuta en la pipeline (job benchmark) en cada rama; es informativo.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using Kitpymes.Core.Mapper;
using Kitpymes.Core.Mapper.Extensions;
using Kitpymes.Core.Mapper.Tests.Ok;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

BenchmarkSwitcher.FromTypes(new[] { typeof(MapBenchmarks), typeof(ProjectionBenchmarks) }).Run(args);

/// <summary>Configuración de AutoMapper equivalente a los mapeos de Kitpymes.Core.Mapper.Tests.Ok.</summary>
public static class AutoMapperSetup
{
    public static MapperConfiguration Create() => new (c =>
    {
        c.CreateMap<Permission, PermissionDto>();
        c.CreateMap<Role, RoleDto>();
        c.CreateMap<User, UserDto>();
    });
}

/// <summary>Ejemplo de extensión: un mapeo de Kitpymes que delega en AutoMapper (sólo como ejemplo, no se publica).</summary>
public sealed class AutoMapperAdapter<TSource, TDestination>(AutoMapper.IMapper mapper) : Kitpymes.Core.Mapper.IMapper<TSource, TDestination>
{
    public TDestination Map(TSource source) => mapper.Map<TDestination>(source);
}

public static class Data
{
    public static Role CreateRole() => new ()
    {
        Id = Guid.NewGuid(),
        Name = "admin",
        Permissions = Enumerable.Range(0, 10).Select(i => new Permission { Id = Guid.NewGuid(), Name = $"permiso-{i}" }).ToList(),
    };
}

[MemoryDiagnoser]
public class MapBenchmarks
{
    private readonly Role _role = Data.CreateRole();
    private Kitpymes.Core.Mapper.IMapper<Role, RoleDto> _kitpymes = null!;
    private Kitpymes.Core.Mapper.IMapper<Role, RoleDto> _adapter = null!;
    private AutoMapper.IMapper _autoMapper = null!;

    [GlobalSetup]
    public void Setup()
    {
        var provider = new ServiceCollection().LoadMapper(o => o.WithAssemblies(typeof(RoleDtoMapper).Assembly)).BuildServiceProvider();
        _kitpymes = provider.GetRequiredService<Kitpymes.Core.Mapper.IMapper<Role, RoleDto>>();
        _autoMapper = AutoMapperSetup.Create().CreateMapper();
        _adapter = new AutoMapperAdapter<Role, RoleDto>(_autoMapper);
    }

    // El mismo código que RoleDtoMapper y MapAll, sin interfaces ni inyección: lo que cuesta la librería es la diferencia.
    [Benchmark(Baseline = true, Description = "A mano")]
    public RoleDto Manual()
    {
        var permissions = new List<PermissionDto>(_role.Permissions.Count);

        foreach (var p in _role.Permissions)
        {
            permissions.Add(new PermissionDto { Id = p.Id, Name = p.Name });
        }

        return new RoleDto { Id = _role.Id, Name = _role.Name, Permissions = permissions };
    }

    [Benchmark(Description = "Kitpymes IMapper")]
    public RoleDto KitpymesMap() => _kitpymes.Map(_role);

    [Benchmark(Description = "AutoMapper Map")]
    public RoleDto AutoMapperMap() => _autoMapper.Map<RoleDto>(_role);

    [Benchmark(Description = "AutoMapper con el adaptador")]
    public RoleDto Adapter() => _adapter.Map(_role);
}

[MemoryDiagnoser]
public class ProjectionBenchmarks
{
    private SqliteConnection _connection = null!;
    private DbContextOptions<BenchContext> _options = null!;
    private IProjection<User, UserDto> _kitpymes = null!;
    private IConfigurationProvider _autoMapper = null!;

    [GlobalSetup]
    public void Setup()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _options = new DbContextOptionsBuilder<BenchContext>().UseSqlite(_connection).Options;

        using (var context = new BenchContext(_options))
        {
            context.Database.EnsureCreated();
            var permissions = Enumerable.Range(0, 10).Select(i => new Permission { Id = Guid.NewGuid(), Name = $"permiso-{i}" }).ToList();
            var roles = Enumerable.Range(0, 5).Select(i => new Role { Id = Guid.NewGuid(), Name = $"rol-{i}", Permissions = permissions.Take(2 * (i + 1)).ToList() }).ToList();
            context.AddRange(roles);
            context.AddRange(Enumerable.Range(0, 100).Select(i => new User { Id = Guid.NewGuid(), Email = $"u{i}@x.es", Role = i % 10 == 0 ? null : roles[i % 5] }));
            context.SaveChanges();
        }

        var provider = new ServiceCollection().LoadMapper(o => o.WithAssemblies(typeof(UserDtoProjection).Assembly)).BuildServiceProvider();
        _kitpymes = provider.GetRequiredService<IProjection<User, UserDto>>();
        _autoMapper = AutoMapperSetup.Create();
    }

    [GlobalCleanup]
    public void Cleanup() => _connection.Dispose();

    [Benchmark(Baseline = true, Description = "Select a mano")]
    public async Task<List<UserDto>> Manual()
    {
        using var context = new BenchContext(_options);
        return await context.Users.Select(u => new UserDto
        {
            Id = u.Id,
            Email = u.Email,
            Role = u.Role == null ? null : new RoleDto
            {
                Id = u.Role.Id,
                Name = u.Role.Name,
                Permissions = u.Role.Permissions.Select(p => new PermissionDto { Id = p.Id, Name = p.Name }),
            },
        }).ToListAsync();
    }

    [Benchmark(Description = "Kitpymes ProjectTo")]
    public async Task<List<UserDto>> KitpymesProjectTo()
    {
        using var context = new BenchContext(_options);
        return await context.Users.ProjectTo(_kitpymes).ToListAsync();
    }

    [Benchmark(Description = "AutoMapper ProjectTo")]
    public async Task<List<UserDto>> AutoMapperProjectTo()
    {
        using var context = new BenchContext(_options);
        return await context.Users.ProjectTo<UserDto>(_autoMapper).ToListAsync();
    }
}

public sealed class BenchContext(DbContextOptions<BenchContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Ignore<Person>();
        modelBuilder.Entity<User>(b =>
        {
            b.HasKey(x => x.Id);
            b.HasOne(x => x.Role).WithMany().HasForeignKey("RoleId").IsRequired(false);
        });
        modelBuilder.Entity<Role>(b =>
        {
            b.HasKey(x => x.Id);
            b.HasMany(x => x.Permissions).WithMany();
        });
        modelBuilder.Entity<Permission>().HasKey(x => x.Id);
    }
}
