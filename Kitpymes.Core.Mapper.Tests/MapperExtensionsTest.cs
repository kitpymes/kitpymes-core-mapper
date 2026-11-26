using Kitpymes.Core.Mapper.Tests.Ok;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Kitpymes.Core.Mapper.Tests
{
    [TestClass]
    public class MapperExtensionsTest
    {
        private static readonly Permission Read = new () { Id = Guid.NewGuid(), Name = "read" };

        private static readonly Permission Write = new () { Id = Guid.NewGuid(), Name = "write" };

        [TestMethod]
        public void MapAll_Passing_List_Returns_EachElementMappedInOrder()
        {
            var result = new PermissionDtoMapper().MapAll(new List<Permission> { Read, Write });

            CollectionAssert.AreEqual(new[] { "read", "write" }, result.Select(x => x.Name).ToArray());
            CollectionAssert.AreEqual(new[] { Read.Id, Write.Id }, result.Select(x => x.Id).ToArray());
            Assert.AreEqual(2, ((List<PermissionDto>)result).Capacity, "Con una colección, la lista se crea con su tamaño.");
        }

        [TestMethod]
        public void MapAll_Passing_LazySequence_Returns_EachElementMappedInOrder()
        {
            var result = new PermissionDtoMapper().MapAll(Sequence());

            CollectionAssert.AreEqual(new[] { "read", "write" }, result.Select(x => x.Name).ToArray());
        }

        [TestMethod]
        public void MapAll_Passing_Empty_Returns_EmptyList()
        {
            Assert.AreEqual(0, new PermissionDtoMapper().MapAll(Array.Empty<Permission>()).Count);
        }

        [TestMethod]
        public void MapAll_Passing_Sequence_Returns_NewListEachCall()
        {
            var mapper = new PermissionDtoMapper();
            var sources = new List<Permission> { Read };

            var first = mapper.MapAll(sources);
            var second = mapper.MapAll(sources);

            Assert.AreNotSame(first, second);
            Assert.IsInstanceOfType<List<PermissionDto>>(first);
        }

        [TestMethod]
        public void MapAll_Passing_NullElement_Returns_ElementPassedToMapper()
        {
            var result = new NullableMapper().MapAll(new string?[] { "a", null });

            CollectionAssert.AreEqual(new[] { "A", "(nulo)" }, result.ToArray());
        }

        [TestMethod]
        public void MapAll_Passing_NullMapper_Throws_ArgumentNullException()
        {
            var exception = Assert.ThrowsExactly<ArgumentNullException>(() => ((IMapper<Permission, PermissionDto>)null!).MapAll(new[] { Read }));

            Assert.AreEqual("mapper", exception.ParamName);
        }

        [TestMethod]
        public void MapAll_Passing_NullSources_Throws_ArgumentNullException()
        {
            var exception = Assert.ThrowsExactly<ArgumentNullException>(() => new PermissionDtoMapper().MapAll(null!));

            Assert.AreEqual("sources", exception.ParamName);
        }

        [TestMethod]
        public void IMapper_Is_ContravariantInSourceAndCovariantInDestination()
        {
            IMapper<Person, PersonDto> people = new PersonMapper();

            IMapper<User, object> users = people;

            var user = new User { Id = Guid.NewGuid(), FullName = "Ana" };
            Assert.AreEqual("Ana", ((PersonDto)users.Map(user)).FullName);
        }

        private static IEnumerable<Permission> Sequence()
        {
            yield return Read;
            yield return Write;
        }

        private sealed class NullableMapper : IMapper<string?, string>
        {
            public string Map(string? source) => source?.ToUpperInvariant() ?? "(nulo)";
        }

        private sealed class PersonMapper : IMapper<Person, PersonDto>
        {
            public PersonDto Map(Person source) => new () { Id = source.Id, FullName = source.FullName };
        }
    }
}
