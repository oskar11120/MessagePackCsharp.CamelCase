using System.Reflection;

namespace MessagePack.CamelCase;
internal sealed record SerializeTypeInfo(
    Type ClrType,
    PropertyInfo[] Getters)
{
    public static SerializeTypeInfo Create<T>()
    {
        var type = typeof(T);
        var getters = type
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => property.CanRead)
            .ToArray();
        return new(type, getters);
    }
}
