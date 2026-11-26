using Kitpymes.Core.Mapper.Extensions;
using Kitpymes.Core.Mapper.Settings;
using Kitpymes.Core.Mapper.Tests.Mal;
using Kitpymes.Core.Mapper.Tests.Ok;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace Kitpymes.Core.Mapper.Tests
{
    [TestClass]
    public class RegistrationTest
    {
        private static readonly Assembly Ok = typeof(User).Assembly;

        private static readonly Assembly Mal = typeof(PermissionNameA).Assembly;

        private static readonly Type[] OkTypes =
        {
            typeof(PermissionDtoMapper), typeof(PermissionDtoProjection), typeof(PersonDtoProjection),
            typeof(RoleDtoMapper), typeof(RoleDtoProjection), typeof(UserDtoProjection), typeof(UserSessionMap),
        };

        [TestMethod]
        public void LoadMapper_Passing_Assembly_Returns_EveryContractAsSingleton()
        {
            var services = new ServiceCollection().LoadMapper(o => o.WithAssemblies(Ok));

            foreach (var contract in new[]
            {
                typeof(IMapper<Permission, PermissionDto>), typeof(IMapper<Role, RoleDto>), typeof(IMapper<User, UserSession>),
                typeof(IProjection<Permission, PermissionDto>), typeof(IProjection<Role, RoleDto>), typeof(IProjection<User, UserDto>),
                typeof(IProjection<Person, PersonDto>), typeof(IProjection<User, UserSession>),
            })
            {
                var descriptor = services.Single(d => d.ServiceType == contract);
                Assert.AreEqual(ServiceLifetime.Singleton, descriptor.Lifetime, contract.Name);
            }
        }

        [TestMethod]
        public void LoadMapper_Passing_Assembly_Returns_ClassesInNameOrder()
        {
            var services = new ServiceCollection().LoadMapper(o => o.WithAssemblies(Ok));

            var registered = services.Where(d => d.ServiceType == d.ImplementationType).Select(d => d.ServiceType).ToArray();

            CollectionAssert.AreEqual(OkTypes.OrderBy(t => t.FullName, StringComparer.Ordinal).ToArray(), registered);
        }

        [TestMethod]
        public void LoadMapper_Passing_Assembly_Returns_ResolvedMappersAndProjectionsThatWork()
        {
            using var provider = new ServiceCollection().LoadMapper(o => o.WithAssemblies(Ok)).BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });

            var role = new Role { Id = Guid.NewGuid(), Name = "admin", Permissions = { new Permission { Name = "read" } } };
            var dto = provider.GetRequiredService<IMapper<Role, RoleDto>>().Map(role);
            Assert.AreEqual("admin", dto.Name);
            Assert.AreEqual("read", dto.Permissions.Single().Name);

            var user = new User { Email = "ana@x.es", Role = role };
            var projected = new[] { user }.AsQueryable().ProjectTo(provider.GetRequiredService<IProjection<User, UserDto>>()).Single();
            Assert.AreEqual("read", projected.Role!.Permissions.Single().Name);
        }

        [TestMethod]
        public void LoadMapper_Passing_ClassWithBothContracts_Returns_OneInstance()
        {
            using var provider = new ServiceCollection().LoadMapper(o => o.WithAssemblies(Ok)).BuildServiceProvider();

            var mapper = provider.GetRequiredService<IMapper<User, UserSession>>();

            Assert.AreSame<object>(mapper, provider.GetRequiredService<IProjection<User, UserSession>>());
            Assert.AreSame<object>(mapper, provider.GetRequiredService<UserSessionMap>());
            Assert.AreSame(mapper, provider.GetRequiredService<IMapper<User, UserSession>>());
        }

        [TestMethod]
        public void LoadMapper_Passing_Assembly_Returns_AbstractGenericAndPlainClassesIgnored()
        {
            var services = new ServiceCollection().LoadMapper(o => o.WithAssemblies(Ok));

            Assert.IsFalse(services.Any(d => d.ServiceType == typeof(IMapper<Role, string>)));
            Assert.IsFalse(services.Any(d => d.ServiceType == typeof(AbstractMap) || d.ServiceType == typeof(NotAMap)));
            Assert.IsFalse(services.Any(d => d.ServiceType.IsGenericType && d.ServiceType.GetGenericTypeDefinition() == typeof(IdentityMap<>)));
        }

        [TestMethod]
        public void LoadMapper_Passing_SameAssemblyTwice_Returns_NoDuplicates()
        {
            var services = new ServiceCollection().LoadMapper(o => o.WithAssemblies(Ok, Ok));
            var count = services.Count;

            services.LoadMapper(o => o.WithAssemblies(Ok).WithMap<PermissionDtoMapper>());

            Assert.AreEqual(count, services.Count);
            Assert.AreEqual(1, services.Count(d => d.ServiceType == typeof(IMapper<Permission, PermissionDto>)));
        }

        [TestMethod]
        public void LoadMapper_Passing_WithMap_Returns_OnlyThatClass()
        {
            var services = new ServiceCollection().LoadMapper(o => o.WithMap<PermissionDtoMapper>().WithMap<PermissionDtoMapper>());

            Assert.AreEqual(1, services.Count(d => d.ServiceType == typeof(IMapper<Permission, PermissionDto>)));
            Assert.AreEqual(1, services.Count(d => d.ServiceType == typeof(PermissionDtoMapper)));
            Assert.IsFalse(services.Any(d => d.ServiceType == typeof(IProjection<Permission, PermissionDto>)));
        }

        [TestMethod]
        public void LoadMapper_Passing_ClassAlreadyRegisteredAsItself_Returns_ThatRegistrationKept()
        {
            var services = new ServiceCollection();
            services.AddSingleton<PermissionDtoMapper>();

            services.LoadMapper(o => o.WithMap<PermissionDtoMapper>());

            Assert.AreEqual(1, services.Count(d => d.ServiceType == typeof(PermissionDtoMapper)));
            Assert.AreEqual(1, services.Count(d => d.ServiceType == typeof(IMapper<Permission, PermissionDto>)));
        }

        [TestMethod]
        public void LoadMapper_Passing_TwoClassesForSamePair_Throws_InvalidOperationException()
        {
            var exception = Assert.ThrowsExactly<InvalidOperationException>(() => new ServiceCollection().LoadMapper(o => o.WithMap<PermissionNameA>().WithMap<PermissionNameB>()));

            Assert.AreEqual($"IMapper<Permission, String> ya lo implementa {typeof(PermissionNameA).FullName}; {typeof(PermissionNameB).FullName} también. Sólo puede haber un mapeo por par de tipos.", exception.Message);
        }

        [TestMethod]
        public void LoadMapper_Passing_AssemblyWithTwoClassesForSamePair_Throws_InvalidOperationException()
        {
            var exception = Assert.ThrowsExactly<InvalidOperationException>(() => new ServiceCollection().LoadMapper(o => o.WithAssemblies(Mal)));

            StringAssert.StartsWith(exception.Message, "IMapper<Permission, String> ya lo implementa ");
        }

        [TestMethod]
        public void LoadMapper_Passing_DuplicateInSecondCall_Throws_InvalidOperationException()
        {
            var services = new ServiceCollection().LoadMapper(o => o.WithMap<PermissionNameA>());

            var exception = Assert.ThrowsExactly<InvalidOperationException>(() => services.LoadMapper(o => o.WithMap<PermissionNameB>()));

            StringAssert.StartsWith(exception.Message, $"IMapper<Permission, String> ya lo implementa {typeof(PermissionNameA).FullName}");
        }

        [TestMethod]
        public void LoadMapper_Passing_DuplicatedClass_Returns_NothingOfThatClassRegistered()
        {
            var services = new ServiceCollection().LoadMapper(o => o.WithMap<PermissionNameA>());

            _ = Assert.ThrowsExactly<InvalidOperationException>(() => services.LoadMapper(o => o.WithMap<NameAndUpper>()));

            Assert.IsFalse(services.Any(d => d.ServiceType == typeof(NameAndUpper) || d.ServiceType == typeof(IMapper<Permission, Guid>)));
        }

        [TestMethod]
        public void LoadMapper_Passing_ContractRegisteredOutside_Throws_InvalidOperationException()
        {
            var services = new ServiceCollection();
            services.AddSingleton<IMapper<Permission, PermissionDto>, PermissionDtoMapper>();

            var exception = Assert.ThrowsExactly<InvalidOperationException>(() => services.LoadMapper(o => o.WithMap<PermissionDtoMapper>()));

            Assert.AreEqual($"IMapper<Permission, PermissionDto> ya está registrado en los servicios fuera de LoadMapper; {typeof(PermissionDtoMapper).FullName} también lo implementa. Quitar uno de los dos.", exception.Message);
        }

        [TestMethod]
        public void LoadMapper_Passing_WithMapOfClassWithoutContracts_Throws_ArgumentException()
        {
            var exception = Assert.ThrowsExactly<ArgumentException>(() => new ServiceCollection().LoadMapper(o => o.WithMap<NotAMap>()));

            Assert.AreEqual("options", exception.ParamName);
            StringAssert.StartsWith(exception.Message, $"{typeof(NotAMap).FullName} no implementa IMapper<,> ni IProjection<,>.");
        }

        [TestMethod]
        public void LoadMapper_Passing_WithMapOfAbstractClass_Throws_ArgumentException()
        {
            _ = Assert.ThrowsExactly<ArgumentException>(() => new ServiceCollection().LoadMapper(o => o.WithMap<AbstractMap>()));
        }

        [TestMethod]
        public void LoadMapper_Passing_NullServices_Throws_ArgumentNullException()
        {
            var exception = Assert.ThrowsExactly<ArgumentNullException>(() => ((IServiceCollection)null!).LoadMapper(o => o.WithAssemblies(Ok)));

            Assert.AreEqual("services", exception.ParamName);
        }

        [TestMethod]
        public void LoadMapper_Passing_NullOptions_Throws_ArgumentNullException()
        {
            var exception = Assert.ThrowsExactly<ArgumentNullException>(() => new ServiceCollection().LoadMapper(null!));

            Assert.AreEqual("options", exception.ParamName);
        }

        [TestMethod]
        public void WithAssemblies_Passing_Null_Throws_ArgumentNullException()
        {
            var exception = Assert.ThrowsExactly<ArgumentNullException>(() => new MapperOptions().WithAssemblies(null!));

            Assert.AreEqual("assemblies", exception.ParamName);
        }

        [TestMethod]
        public void WithAssemblies_Passing_NullElement_Throws_ArgumentNullException()
        {
            var exception = Assert.ThrowsExactly<ArgumentNullException>(() => new MapperOptions().WithAssemblies(Ok, null!));

            Assert.AreEqual("assemblies", exception.ParamName);
        }

        [TestMethod]
        public void With_Methods_Return_SameOptions()
        {
            var options = new MapperOptions();

            Assert.AreSame(options, options.WithAssemblies(Ok));
            Assert.AreSame(options, options.WithMap<PermissionDtoMapper>());
        }

        /// <summary>Implementa un contrato libre y otro ya ocupado: no puede quedar nada registrado a medias.</summary>
        private sealed class NameAndUpper : IMapper<Permission, Guid>, IMapper<Permission, string>
        {
            public Guid Map(Permission source) => source.Id;

            string IMapper<Permission, string>.Map(Permission source) => source.Name;
        }
    }
}
