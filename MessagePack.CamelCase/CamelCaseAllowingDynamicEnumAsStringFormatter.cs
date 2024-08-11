using MessagePack.Formatters;
using MessagePack.Resolvers;

namespace MessagePack.CamelCase;
public sealed class CamelCaseAllowingDynamicEnumAsStringFormatter<TEnum> : IMessagePackFormatter<TEnum>
{
    public static readonly CamelCaseAllowingDynamicEnumAsStringFormatter<TEnum>? Instance =
        typeof(TEnum) is { IsValueType: true, IsEnum: true } ? new() : null;

    private CamelCaseAllowingDynamicEnumAsStringFormatter()
    {
    }

    private readonly IMessagePackFormatter<TEnum> @base =
        DynamicEnumAsStringResolver.Instance.GetFormatter<TEnum>()!;

    private readonly Dictionary<string, TEnum> deserializeMap = Enum
        .GetValues(typeof(TEnum))
        .Cast<TEnum>()
        .ToDictionary(value => value!.ToString()!, StringComparer.OrdinalIgnoreCase)!;

    public TEnum Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
    {
        var text = reader.ReadString();
        return text is not null && deserializeMap.TryGetValue(text, out var found) ?
            found :
            throw new InvalidOperationException($"Could not deserialize string literal '{text}' to {typeof(TEnum)}.");
    }

    public void Serialize(ref MessagePackWriter writer, TEnum value, MessagePackSerializerOptions options)
        => @base.Serialize(ref writer, value, options);
}
