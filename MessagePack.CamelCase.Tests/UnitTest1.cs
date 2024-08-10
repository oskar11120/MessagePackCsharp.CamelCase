using MessagePack.Resolvers;

namespace MessagePack.CamelCase.Tests;

public sealed record SomeRecord(int Integer);

public class Tests
{
    [Test]
    public void Deserialize_Works()
    {
        var expectation = new SomeRecord(1);
        var serialized = MessagePackSerializer.Serialize(expectation, ContractlessStandardResolver.Options);
        var deserializeOptions = ContractlessStandardResolver
            .Options
            .WithResolver(CompositeResolver.Create(
                CamelCaseContractlessFormatterResolver.Instance,
                ContractlessStandardResolver.Instance));
        var result = MessagePackSerializer.Deserialize<SomeRecord>(serialized.AsMemory(), deserializeOptions);
        Assert.That(result, Is.EqualTo(expectation));
    }
}