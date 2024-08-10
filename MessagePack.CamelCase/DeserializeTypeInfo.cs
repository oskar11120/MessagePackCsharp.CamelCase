using System.Reflection;

namespace MessagePack.CamelCase;
internal sealed record DeserializeTypeInfo(
    Type ClrType,
    ConstructorInfo Constructor,
    PropertyInfo[] SettersWithNoMatchingConstructorParameter)
{
    public static DeserializeTypeInfo Create<T>()
    {
        var type = typeof(T);
        ConstructorInfo GetCtor()
        {
            var ctors = type.GetConstructors();
            if (ctors.Length is 1)
                return ctors[0];
            var matching = ctors
                .Where(ctor => ctor.GetCustomAttribute<SerializationConstructorAttribute>() is not null)
                .ToArray();
            InvalidOperationException MultipleCtors(string andThatsWrongBecause)
                => new($"{type} has no constructor suitable for serialization. Type has mulitple constructors{andThatsWrongBecause}.");
            return matching.Length switch
            {
                0 => throw MultipleCtors($", but no marked with {typeof(SerializationConstructorAttribute)}"),
                1 => matching[0],
                _ => throw MultipleCtors($"marked with {typeof(SerializationConstructorAttribute)}")
            };
        }

        var ctor = GetCtor();
        PropertyInfo[] GetSettersWithNoMatchingConstructorParameter()
        {
            var ctorParams = ctor.GetParameters();
            var setters = type
                .GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Where(property => property.CanWrite);
            return setters
                .ExceptBy(
                    ctorParams.Select(param => param.Name),
                    param => param.Name,
                    StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        var setters = GetSettersWithNoMatchingConstructorParameter();
        return new(type, ctor, setters);
    }
}
