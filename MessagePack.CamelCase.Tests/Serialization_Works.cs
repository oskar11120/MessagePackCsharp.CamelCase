using MessagePack.Resolvers;

namespace MessagePack.CamelCase.Tests;

public sealed record Empty();
public sealed record WithBuiltins(string Text, int Number);
public sealed record WithBuiltinsAndNestedRecord(WithBuiltins SomeRecord, double OtherNumber);

public class Test_Empty : Test<Empty>
{
    protected override Empty Expectation => new();
}

public class Test_WithBultins : Test<WithBuiltins>
{
    protected override WithBuiltins Expectation => new("aaa", 5);
}

public class Test_WithBuiltinsAndNestedRecord : Test<WithBuiltinsAndNestedRecord>
{
    protected override WithBuiltinsAndNestedRecord Expectation => new(new("aaa", 5), 10.5);
}

public abstract class Test<TExpectation>
{
    private static readonly MessagePackSerializerOptions options = ContractlessStandardResolver
        .Options
        .WithResolver(CompositeResolver.Create(
            CamelCaseContractlessFormatterResolver.Instance,
            ContractlessStandardResolver.Instance));
    protected abstract TExpectation Expectation { get; }

    [Test]
    public void Serialization_Works()
    {
        var serialized = MessagePackSerializer.Serialize(Expectation, options);
        var result = MessagePackSerializer.Deserialize<TExpectation>(serialized.AsMemory(), options);
        Assert.That(result, Is.EqualTo(Expectation));
    }
}