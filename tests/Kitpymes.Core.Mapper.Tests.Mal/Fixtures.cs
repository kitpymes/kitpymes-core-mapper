// Configuración incorrecta a propósito: dos clases para el mismo par. Escanear este ensamblado tiene que fallar.
using Kitpymes.Core.Mapper.Tests.Ok;

namespace Kitpymes.Core.Mapper.Tests.Mal
{
    public sealed class PermissionNameA : IMapper<Permission, string>
    {
        public string Map(Permission source) => source.Name;
    }

    public sealed class PermissionNameB : IMapper<Permission, string>
    {
        public string Map(Permission source) => source.Name.ToUpperInvariant();
    }
}
