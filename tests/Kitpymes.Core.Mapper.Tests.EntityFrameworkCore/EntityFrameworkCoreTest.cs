using Kitpymes.Core.Mapper.Extensions;
using Kitpymes.Core.Mapper.Tests.Ok;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Riok.Mapperly.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace Kitpymes.Core.Mapper.Tests.EntityFrameworkCore
{
    /// <summary>Contexto como el del backend: Role con una propiedad sombra IsActive, User con su rol opcional.</summary>
    public sealed class MapperDbContext(DbContextOptions<MapperDbContext> options) : DbContext(options)
    {
        public DbSet<User> Users => Set<User>();

        public DbSet<Role> Roles => Set<Role>();

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
                b.Property<bool>("IsActive");
                b.HasMany(x => x.Permissions).WithMany();
            });
            modelBuilder.Entity<Permission>().HasKey(x => x.Id);
        }
    }

    public sealed class RoleStatusDto
    {
        public required string Name { get; init; }

        public required bool IsActive { get; init; }

        public required int Permissions { get; init; }
    }

    /// <summary>Con EF.Property: sólo vale en una consulta, por eso no hay IMapper para este par.</summary>
    public sealed class RoleStatusProjection : IProjection<Role, RoleStatusDto>
    {
        public Expression<Func<Role, RoleStatusDto>> Selector { get; } = r => new RoleStatusDto
        {
            Name = r.Name,
            IsActive = EF.Property<bool>(r, "IsActive"),
            Permissions = r.Permissions.Count,
        };
    }

    public sealed class PermissionSummary
    {
        public required Guid Id { get; init; }

        public required string Name { get; init; }
    }

    /// <summary>Ejemplo de extensión: Mapperly genera el cuerpo de Map en compilación.</summary>
    [Mapper]
    public sealed partial class PermissionMapperly : IMapper<Permission, PermissionSummary>
    {
        public partial PermissionSummary Map(Permission source);
    }

    [TestClass]
    public sealed class EntityFrameworkCoreTest : IDisposable
    {
        private readonly SqliteConnection connection = new ("DataSource=:memory:");

        private readonly ServiceProvider provider;

        private readonly Guid adminId = Guid.NewGuid();

        public EntityFrameworkCoreTest()
        {
            connection.Open();
            provider = new ServiceCollection()
                .AddDbContext<MapperDbContext>(o => o.UseSqlite(connection))
                .LoadMapper(o => o.WithAssemblies(typeof(User).Assembly).WithMap<RoleStatusProjection>().WithMap<PermissionMapperly>())
                .BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });

            using var scope = provider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<MapperDbContext>();
            context.Database.EnsureCreated();

            var read = new Permission { Id = Guid.NewGuid(), Name = "read" };
            var write = new Permission { Id = Guid.NewGuid(), Name = "write" };
            var admin = new Role { Id = adminId, Name = "admin", Permissions = { read, write } };
            var guest = new Role { Id = Guid.NewGuid(), Name = "guest", Permissions = { read } };
            context.AddRange(admin, guest);
            context.Entry(admin).Property("IsActive").CurrentValue = true;
            context.AddRange(
                new User { Id = Guid.NewGuid(), Email = "ana@x.es", Role = admin },
                new User { Id = Guid.NewGuid(), Email = "bea@x.es", Role = guest },
                new User { Id = Guid.NewGuid(), Email = "carlos@x.es", Role = null });
            context.SaveChanges();
        }

        public void Dispose()
        {
            provider.Dispose();
            connection.Dispose();
        }

        [TestMethod]
        public void Verify_Passing_RealConfiguration_Returns_WithoutThrowing()
        {
            MapperVerifier.Verify(provider);
        }

        [TestMethod]
        public async Task ProjectTo_Passing_NestedProjections_Returns_RowsTranslatedBySqlite()
        {
            var (context, scope) = Context();
            using (scope)
            {
                var users = await context.Users.OrderBy(u => u.Email).ProjectTo(Get<IProjection<User, UserDto>>()).ToListAsync();

                CollectionAssert.AreEqual(new[] { "ana@x.es", "bea@x.es", "carlos@x.es" }, users.Select(u => u.Email).ToArray());
                Assert.AreEqual("admin", users[0].Role!.Name);
                CollectionAssert.AreEquivalent(new[] { "read", "write" }, users[0].Role!.Permissions.Select(p => p.Name).ToArray());
                CollectionAssert.AreEqual(new[] { "read" }, users[1].Role!.Permissions.Select(p => p.Name).ToArray());
                Assert.IsNull(users[2].Role);
            }
        }

        [TestMethod]
        public void ProjectTo_Passing_NestedProjections_Returns_SqlWithoutClientEvaluation()
        {
            var (context, scope) = Context();
            using (scope)
            {
                var sql = context.Users.ProjectTo(Get<IProjection<User, UserDto>>()).ToQueryString();

                StringAssert.Contains(sql, "SELECT");
                Assert.IsFalse(sql.Contains("Inline", StringComparison.Ordinal), sql);
            }
        }

        [TestMethod]
        public async Task ProjectTo_Passing_PagingAfterProjection_Returns_RequestedPage()
        {
            var (context, scope) = Context();
            using (scope)
            {
                // El filtro sobre un objeto construido por la proyección (u.Role != null del DTO) EF no lo traduce:
                // se filtra antes de proyectar; ordenar y paginar sobre columnas del DTO, sí.
                var page = await context.Users.Where(u => u.Role != null).ProjectTo(Get<IProjection<User, UserDto>>())
                    .OrderBy(u => u.Email).Skip(1).Take(1).ToListAsync();

                Assert.AreEqual("bea@x.es", page.Single().Email);
                Assert.AreEqual("guest", page.Single().Role!.Name);
            }
        }

        [TestMethod]
        public async Task ProjectTo_Passing_ShadowProperty_Returns_ValueFromDatabase()
        {
            var (context, scope) = Context();
            using (scope)
            {
                var roles = await context.Roles.OrderBy(r => r.Name).ProjectTo(Get<IProjection<Role, RoleStatusDto>>()).ToListAsync();

                Assert.AreEqual("admin", roles[0].Name);
                Assert.IsTrue(roles[0].IsActive);
                Assert.AreEqual(2, roles[0].Permissions);
                Assert.IsFalse(roles[1].IsActive);
            }
        }

        [TestMethod]
        public async Task MapperAndProjection_OfSamePair_Return_SameData()
        {
            var (context, scope) = Context();
            using (scope)
            {
                var projected = await context.Roles.Where(r => r.Id == adminId).ProjectTo(Get<IProjection<Role, RoleDto>>()).SingleAsync();
                var entity = await context.Roles.Include(r => r.Permissions).SingleAsync(r => r.Id == adminId);
                var mapped = Get<IMapper<Role, RoleDto>>().Map(entity);

                Assert.AreEqual(mapped.Id, projected.Id);
                Assert.AreEqual(mapped.Name, projected.Name);
                CollectionAssert.AreEquivalent(
                    mapped.Permissions.Select(p => (p.Id, p.Name)).ToList(),
                    projected.Permissions.Select(p => (p.Id, p.Name)).ToList());
            }
        }

        [TestMethod]
        public void Mapperly_AsExtension_Returns_MappedObject()
        {
            var permission = new Permission { Id = Guid.NewGuid(), Name = "read" };

            var dto = Get<IMapper<Permission, PermissionSummary>>().MapAll(new List<Permission> { permission }).Single();

            Assert.IsInstanceOfType<PermissionMapperly>(Get<IMapper<Permission, PermissionSummary>>());
            Assert.AreEqual(permission.Id, dto.Id);
            Assert.AreEqual("read", dto.Name);
        }

        private T Get<T>()
            where T : notnull
            => provider.GetRequiredService<T>();

        private (MapperDbContext Context, IServiceScope Scope) Context()
        {
            var scope = provider.CreateScope();
            return (scope.ServiceProvider.GetRequiredService<MapperDbContext>(), scope);
        }
    }
}
