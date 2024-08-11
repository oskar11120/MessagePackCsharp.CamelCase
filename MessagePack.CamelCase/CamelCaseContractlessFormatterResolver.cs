using MessagePack.Formatters;

namespace MessagePack.CamelCase;

public sealed class CamelCaseContractlessFormatterResolver : IFormatterResolver
{
    public static readonly CamelCaseContractlessFormatterResolver Instance = new();

    private CamelCaseContractlessFormatterResolver()
    {
    }

    public IMessagePackFormatter<T>? GetFormatter<T>() => 
        CamelCaseContractlessMapFormatter<T>.Instance ??
        CamelCaseAllowingDynamicEnumAsStringFormatter<T>.Instance 
        as IMessagePackFormatter<T>;
}
