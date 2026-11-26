using Kitpymes.Core.Mapper.Extensions;
using Kitpymes.Core.Mapper.Tests.Ok;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq.Expressions;

namespace Kitpymes.Core.Mapper.Tests
{
    [TestClass]
    public class VerifierTest
    {
        [TestMethod]
        public void Verify_Passing_CorrectConfiguration_Returns_WithoutThrowing()
        {
            using var provider = new ServiceCollection().LoadMapper(o => o.WithAssemblies(typeof(User).Assembly)).BuildServiceProvider();

            MapperVerifier.Verify(provider);
        }

        [TestMethod]
        public void Verify_Passing_CorrectConfiguration_Returns_ProjectionsAlreadyExpanded()
        {
            using var provider = new ServiceCollection().LoadMapper(o => o.WithAssemblies(typeof(User).Assembly)).BuildServiceProvider();
            var projection = provider.GetRequiredService<IProjection<User, UserDto>>();

            MapperVerifier.Verify(provider);

            Assert.AreSame(projection.Expand(), projection.Expand());
        }

        [TestMethod]
        public void Verify_Passing_ProviderWithoutLoadMapper_Throws_InvalidOperationException()
        {
            using var provider = new ServiceCollection().BuildServiceProvider();

            var exception = Assert.ThrowsExactly<InvalidOperationException>(() => MapperVerifier.Verify(provider));

            Assert.AreEqual("No se ha llamado a LoadMapper en estos servicios.", exception.Message);
        }

        [TestMethod]
        public void Verify_Passing_NullProvider_Throws_ArgumentNullException()
        {
            var exception = Assert.ThrowsExactly<ArgumentNullException>(() => MapperVerifier.Verify(null!));

            Assert.AreEqual("provider", exception.ParamName);
        }

        [TestMethod]
        public void Verify_Passing_SeveralErrors_Throws_AllOfThemTogether()
        {
            var services = new ServiceCollection().LoadMapper(o => o.WithMap<NeedsMissingService>().WithMap<BrokenProjection>().WithMap<PermissionDtoMapper>());
            using var provider = services.BuildServiceProvider();

            var exception = Assert.ThrowsExactly<InvalidOperationException>(() => MapperVerifier.Verify(provider));

            var lines = exception.Message.Split(Environment.NewLine);
            Assert.AreEqual(3, lines.Length, exception.Message);
            Assert.AreEqual("La configuración del mapper tiene errores:", lines[0]);
            StringAssert.StartsWith(lines[1], "- IMapper<Permission, String>: InvalidOperationException: Unable to resolve service for type 'System.IFormatProvider'");
            Assert.AreEqual("- IProjection<Permission, String>: InvalidOperationException: La proyección anidada IProjection<Permission, PermissionDto> es nula.", lines[2]);
        }

        [TestMethod]
        public void Verify_Passing_ContractReplacedAfterLoadMapper_Throws_InvalidOperationException()
        {
            var services = new ServiceCollection().LoadMapper(o => o.WithMap<PermissionDtoMapper>());
            services.AddSingleton<IMapper<Permission, PermissionDto>, OtherPermissionMapper>();
            using var provider = services.BuildServiceProvider();

            var exception = Assert.ThrowsExactly<InvalidOperationException>(() => MapperVerifier.Verify(provider));

            Assert.AreEqual(
                "La configuración del mapper tiene errores:" + Environment.NewLine
                + $"- IMapper<Permission, PermissionDto> se resuelve a {typeof(OtherPermissionMapper).FullName} y no a {typeof(PermissionDtoMapper).FullName}: alguien lo ha registrado a mano después de LoadMapper.",
                exception.Message);
        }

        private sealed class NeedsMissingService(IFormatProvider format) : IMapper<Permission, string>
        {
            public string Map(Permission source) => source.Id.ToString(null, format);
        }

        private sealed class BrokenProjection : IProjection<Permission, string>
        {
            private readonly IProjection<Permission, PermissionDto>? nested = null;

            public Expression<Func<Permission, string>> Selector => p => nested!.Inline(p).Name;
        }

        private sealed class OtherPermissionMapper : IMapper<Permission, PermissionDto>
        {
            public PermissionDto Map(Permission source) => new () { Id = source.Id, Name = "otro" };
        }
    }
}
