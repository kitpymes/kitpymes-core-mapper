using Kitpymes.Core.Mapper.Tests.Ok;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace Kitpymes.Core.Mapper.Tests
{
    [TestClass]
    public class ProjectionExtensionsTest
    {
        private static readonly Permission Read = new () { Id = Guid.NewGuid(), Name = "read" };

        private static readonly Permission Write = new () { Id = Guid.NewGuid(), Name = "write" };

        private static readonly Role Admin = new () { Id = Guid.NewGuid(), Name = "admin", Permissions = { Read, Write } };

        private static readonly User Ana = new () { Id = Guid.NewGuid(), Email = "ana@x.es", FullName = "Ana", Role = Admin };

        private static readonly User Luis = new () { Id = Guid.NewGuid(), Email = "luis@x.es", FullName = "Luis", Role = null };

        private static UserDtoProjection Users() => new (new RoleDtoProjection(new PermissionDtoProjection()));

        [TestMethod]
        public void ProjectTo_Passing_FlatProjection_Returns_ProjectedElements()
        {
            var result = new[] { Read, Write }.AsQueryable().ProjectTo(new PermissionDtoProjection()).ToList();

            CollectionAssert.AreEqual(new[] { "read", "write" }, result.Select(x => x.Name).ToArray());
            CollectionAssert.AreEqual(new[] { Read.Id, Write.Id }, result.Select(x => x.Id).ToArray());
        }

        [TestMethod]
        public void ProjectTo_Passing_NestedProjections_Returns_ElementsWithNestedObjectsAndCollections()
        {
            var result = new[] { Ana, Luis }.AsQueryable().ProjectTo(Users()).ToList();

            Assert.AreEqual(Ana.Id, result[0].Id);
            Assert.AreEqual("ana@x.es", result[0].Email);
            Assert.AreEqual(Admin.Id, result[0].Role!.Id);
            Assert.AreEqual("admin", result[0].Role!.Name);
            CollectionAssert.AreEqual(new[] { "read", "write" }, result[0].Role!.Permissions.Select(x => x.Name).ToArray());
            CollectionAssert.AreEqual(new[] { Read.Id, Write.Id }, result[0].Role!.Permissions.Select(x => x.Id).ToArray());
            Assert.AreEqual("luis@x.es", result[1].Email);
            Assert.IsNull(result[1].Role);
        }

        [TestMethod]
        public void ProjectTo_Passing_NestedProjections_Returns_QueryWithoutInline()
        {
            var query = new[] { Ana }.AsQueryable().ProjectTo(Users());

            Assert.AreEqual(0, InlineCalls(query.Expression));
        }

        [TestMethod]
        public void ProjectTo_Passing_NullSource_Throws_ArgumentNullException()
        {
            var exception = Assert.ThrowsExactly<ArgumentNullException>(() => ((IQueryable<Permission>)null!).ProjectTo(new PermissionDtoProjection()));

            Assert.AreEqual("source", exception.ParamName);
        }

        [TestMethod]
        public void ProjectTo_Passing_NullProjection_Throws_ArgumentNullException()
        {
            var exception = Assert.ThrowsExactly<ArgumentNullException>(() => new[] { Read }.AsQueryable().ProjectTo((IProjection<Permission, PermissionDto>)null!));

            Assert.AreEqual("projection", exception.ParamName);
        }

        [TestMethod]
        public void Expand_Passing_ProjectionWithoutInline_Returns_SameSelector()
        {
            var projection = new PermissionDtoProjection();

            Assert.AreSame(projection.Selector, projection.Expand());
        }

        [TestMethod]
        public void Expand_Passing_SameProjectionTwice_Returns_CachedExpression()
        {
            var projection = Users();

            Assert.AreSame(projection.Expand(), projection.Expand());
            Assert.AreNotSame(projection.Selector, projection.Selector, "La propiedad crea una expresión nueva en cada lectura: la caché es lo que la hace igual.");
        }

        [TestMethod]
        public void Expand_Passing_NestedProjections_Returns_ExpressionWithNestedBodies()
        {
            var expanded = Users().Expand();

            Assert.AreEqual(0, InlineCalls(expanded));
            var text = expanded.ToString();
            StringAssert.Contains(text, "u.Role.Permissions.Select(p => new PermissionDto()");
            StringAssert.Contains(text, "Name = u.Role.Name");
        }

        [TestMethod]
        public void Expand_Passing_NullProjection_Throws_ArgumentNullException()
        {
            var exception = Assert.ThrowsExactly<ArgumentNullException>(() => ((IProjection<Permission, PermissionDto>)null!).Expand());

            Assert.AreEqual("projection", exception.ParamName);
        }

        [TestMethod]
        public void Expand_Passing_SourceOfDerivedType_Returns_ProjectionOfBaseType()
        {
            var people = new PersonDtoProjection();
            Expression<Func<User, PersonDto>> selector = u => people.Inline(u);

            var result = new[] { Ana }.AsQueryable().Select(selector.Expand()).Single();

            Assert.AreEqual("Ana", result.FullName);
            Assert.AreEqual(Ana.Id, result.Id);
        }

        [TestMethod]
        public void Expand_Passing_ClassWithTwoProjections_Returns_EachOne()
        {
            var both = new TwoProjections();

            Assert.AreEqual("read", new[] { Read }.AsQueryable().ProjectTo<Permission, string>(both).Single());
            Assert.AreEqual(Read.Id, new[] { Read }.AsQueryable().ProjectTo<Permission, Guid>(both).Single());
            Assert.AreEqual("read", new[] { Read }.AsQueryable().ProjectTo<Permission, string>(both).Single());
        }

        [TestMethod]
        public void Expand_Passing_AdHocExpression_Returns_ExpressionWithNestedBodies()
        {
            var roles = new RoleDtoProjection(new PermissionDtoProjection());
            Expression<Func<User, KeyValuePair<string, RoleDto>>> selector = u => new KeyValuePair<string, RoleDto>(u.Email, roles.Inline(u.Role!));

            var expanded = selector.Expand();
            var result = new[] { Ana }.AsQueryable().Select(expanded).Single();

            Assert.AreEqual(0, InlineCalls(expanded));
            Assert.AreEqual("ana@x.es", result.Key);
            Assert.AreEqual("admin", result.Value.Name);
            Assert.AreEqual(2, result.Value.Permissions.Count());
        }

        [TestMethod]
        public void Expand_Passing_AdHocExpressionWithoutInline_Returns_SameInstance()
        {
            Expression<Func<User, string>> selector = u => u.Email;

            Assert.AreSame(selector, selector.Expand());
        }

        [TestMethod]
        public void Expand_Passing_NullExpression_Throws_ArgumentNullException()
        {
            var exception = Assert.ThrowsExactly<ArgumentNullException>(() => ((Expression<Func<User, string>>)null!).Expand());

            Assert.AreEqual("expression", exception.ParamName);
        }

        [TestMethod]
        public void Expand_Passing_ProjectionCreatedInsideExpression_Returns_ExpandedExpression()
        {
            Expression<Func<Permission, PermissionDto>> selector = p => new PermissionDtoProjection().Inline(p);

            var result = new[] { Write }.AsQueryable().Select(selector.Expand()).Single();

            Assert.AreEqual("write", result.Name);
        }

        [TestMethod]
        public void Expand_Passing_ProjectionInStaticField_Returns_ExpandedExpression()
        {
            Expression<Func<Permission, PermissionDto>> selector = p => StaticHolder.Permissions.Inline(p);

            var result = new[] { Write }.AsQueryable().Select(selector.Expand()).Single();

            Assert.AreEqual("write", result.Name);
        }

        [TestMethod]
        public void Expand_Passing_ProjectionInProperty_Returns_ExpandedExpression()
        {
            var holder = new PropertyHolder();
            Expression<Func<Permission, PermissionDto>> selector = p => holder.Permissions.Inline(p);

            var result = new[] { Write }.AsQueryable().Select(selector.Expand()).Single();

            Assert.AreEqual("write", result.Name);
        }

        [TestMethod]
        public void Expand_Passing_ProjectionChosenWithItsOwnLambda_Returns_ExpandedExpression()
        {
            var candidates = new List<IProjection<Permission, PermissionDto>?> { null, new PermissionDtoProjection() };
            Expression<Func<Permission, PermissionDto>> selector = p => candidates.First(c => c != null)!.Inline(p);

            var result = new[] { Write }.AsQueryable().Select(selector.Expand()).Single();

            Assert.AreEqual("write", result.Name);
        }

        [TestMethod]
        public void Expand_Passing_ProjectionThatDependsOnParameter_Throws_InvalidOperationException()
        {
            Expression<Func<Holder, PermissionDto>> selector = h => h.Projection.Inline(h.Permission);

            var exception = Assert.ThrowsExactly<InvalidOperationException>(() => selector.Expand());

            Assert.AreEqual("La proyección anidada IProjection<Permission, PermissionDto> no se puede evaluar sin los parámetros de la expresión: h.Projection. Usar un campo, un parámetro de constructor o una constante.", exception.Message);
        }

        [TestMethod]
        public void Expand_Passing_NullNestedProjection_Throws_InvalidOperationException()
        {
            IProjection<Permission, PermissionDto>? none = null;
            Expression<Func<Permission, PermissionDto>> selector = p => none!.Inline(p);

            var exception = Assert.ThrowsExactly<InvalidOperationException>(() => selector.Expand());

            Assert.AreEqual("La proyección anidada IProjection<Permission, PermissionDto> es nula.", exception.Message);
        }

        [TestMethod]
        public void Expand_Passing_NullConstantProjection_Throws_InvalidOperationException()
        {
            var parameter = Expression.Parameter(typeof(Permission), "p");
            var inline = typeof(ProjectionExtensions).GetMethod(nameof(ProjectionExtensions.Inline))!.MakeGenericMethod(typeof(Permission), typeof(PermissionDto));
            var selector = Expression.Lambda<Func<Permission, PermissionDto>>(
                Expression.Call(inline, Expression.Constant(null, typeof(IProjection<Permission, PermissionDto>)), parameter), parameter);

            var exception = Assert.ThrowsExactly<InvalidOperationException>(() => selector.Expand());

            Assert.AreEqual("La proyección anidada IProjection<Permission, PermissionDto> es nula.", exception.Message);
        }

        [TestMethod]
        public void Expand_Passing_ProjectionWithNullSelector_Throws_InvalidOperationException()
        {
            var exception = Assert.ThrowsExactly<InvalidOperationException>(() => new NullSelector().Expand());

            Assert.AreEqual($"Selector de {typeof(NullSelector).FullName} es nulo.", exception.Message);
        }

        [TestMethod]
        public void Expand_Passing_CycleBetweenTwoProjections_Throws_InvalidOperationException()
        {
            var a = new CycleA();
            var b = new CycleB { Other = a };
            a.Other = b;

            var exception = Assert.ThrowsExactly<InvalidOperationException>(() => a.Expand());

            Assert.AreEqual("Ciclo de proyecciones: CycleA -> CycleB -> CycleA.", exception.Message);
        }

        [TestMethod]
        public void Expand_Passing_ProjectionThatUsesItself_Throws_InvalidOperationException()
        {
            var a = new CycleA();
            var b = new CycleB();
            a.Other = b;
            b.Other = a;

            _ = Assert.ThrowsExactly<InvalidOperationException>(() => b.Expand());
            var exception = Assert.ThrowsExactly<InvalidOperationException>(() => b.Expand());

            Assert.AreEqual("Ciclo de proyecciones: CycleB -> CycleA -> CycleB.", exception.Message, "Un fallo no se guarda en la caché: se repite igual.");
        }

        [TestMethod]
        public void Expand_Passing_SameProjectionTwiceInsideAnother_Returns_ExpressionWithoutFalseCycle()
        {
            var permissions = new PermissionDtoProjection();
            Expression<Func<Role, PermissionDto[]>> selector = r => new[] { permissions.Inline(r.Permissions[0]), permissions.Inline(r.Permissions[1]) };

            var result = new[] { Admin }.AsQueryable().Select(selector.Expand()).Single();

            CollectionAssert.AreEqual(new[] { "read", "write" }, result.Select(x => x.Name).ToArray());
        }

        [TestMethod]
        public void Inline_Executed_Throws_InvalidOperationException()
        {
            var exception = Assert.ThrowsExactly<InvalidOperationException>(() => new PermissionDtoProjection().Inline(Read));

            Assert.AreEqual("Inline de IProjection<Permission, PermissionDto> se ha ejecutado: la expresión no se ha expandido. Usar ProjectTo o Expand antes de ejecutar la consulta.", exception.Message);
        }

        [TestMethod]
        public void Inline_InsideSelectWithoutExpand_Throws_InvalidOperationException()
        {
            var query = new[] { Ana }.AsQueryable().Select(Users().Selector);

            _ = Assert.ThrowsExactly<InvalidOperationException>(() => query.ToList());
        }

        private static int InlineCalls(Expression expression)
        {
            var counter = new InlineCounter();
            counter.Visit(expression);
            return counter.Count;
        }

        private sealed class InlineCounter : ExpressionVisitor
        {
            public int Count { get; private set; }

            protected override Expression VisitMethodCall(MethodCallExpression node)
            {
                if (node.Method.Name == nameof(ProjectionExtensions.Inline))
                {
                    Count++;
                }

                return base.VisitMethodCall(node);
            }
        }

        private sealed class TwoProjections : IProjection<Permission, string>, IProjection<Permission, Guid>
        {
            Expression<Func<Permission, string>> IProjection<Permission, string>.Selector => p => p.Name;

            Expression<Func<Permission, Guid>> IProjection<Permission, Guid>.Selector => p => p.Id;
        }

        private sealed class NullSelector : IProjection<Permission, PermissionDto>
        {
            public Expression<Func<Permission, PermissionDto>> Selector => null!;
        }

        private sealed class CycleA : IProjection<Permission, string>
        {
            public IProjection<Permission, string>? Other { get; set; }

            public Expression<Func<Permission, string>> Selector
            {
                get
                {
                    var other = Other!;
                    return p => other.Inline(p);
                }
            }
        }

        private sealed class CycleB : IProjection<Permission, string>
        {
            public IProjection<Permission, string>? Other { get; set; }

            public Expression<Func<Permission, string>> Selector
            {
                get
                {
                    var other = Other!;
                    return p => other.Inline(p) + "b";
                }
            }
        }

        private static class StaticHolder
        {
            public static readonly IProjection<Permission, PermissionDto> Permissions = new PermissionDtoProjection();
        }

        private sealed class PropertyHolder
        {
            public IProjection<Permission, PermissionDto> Permissions { get; } = new PermissionDtoProjection();
        }

        private sealed class Holder
        {
            public IProjection<Permission, PermissionDto> Projection { get; } = new PermissionDtoProjection();

            public Permission Permission { get; } = new ();
        }
    }
}
