using MessagePack.Resolvers;

namespace MessagePack.CamelCase.Tests;

public sealed record SomeRecord(string Text);

public class Tests
{
    [Test]
    public void Deserialize_Works()
    {
        var expectation = new SomeRecord("aaa");
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