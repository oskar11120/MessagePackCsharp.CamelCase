using MessagePack.Resolvers;

namespace MessagePack.CamelCase.Tests;

public static class Models
{
    public sealed record Empty();
    public sealed record BuiltInCtorProperties(string String, int Integer);
    public sealed record CtorPropertiesNested(BuiltInCtorProperties Nested, double Double);

    public sealed record BuiltInNonCtorProperties
    {
        public required string String { get; init; }
        public required int Integer { get; init; }
    }

    public sealed record NonCtorPropertiesNested
    {
        public required double Double { get; init; }
        public required BuiltInNonCtorProperties Nested { get; init; }
    }

    public sealed record BuiltInMixed(string StringCtor, int IntCtor)
    {
        public required string StringNonCtor { get; init; }
        public required int IntNonCtor { get; init; }
    }

    public sealed record MixedNested(BuiltInMixed NestedCtor, double DoubleCtor)
    {
        public required BuiltInMixed NestedNonCtor { get; init; }
        public required double DoubleNonCtor { get; init; }
    }
}

public class Empty : Tests<Models.Empty>
{
    protected override Models.Empty Expectation => new();
}

public class BuiltInCtorProperties : Tests<Models.BuiltInCtorProperties>
{
    protected override Models.BuiltInCtorProperties Expectation => new("aaa", 5);
}

public class CtorPropertiesNested : Tests<Models.CtorPropertiesNested>
{
    protected override Models.CtorPropertiesNested Expectation => new(new("aaa", 5), 10.5);
}

public class BuiltInNonCtorProperties : Tests<Models.BuiltInNonCtorProperties>
{
    protected override Models.BuiltInNonCtorProperties Expectation => new() { String = "aaa", Integer = 5 };
}

public class NonCtorPropertiesNested : Tests<Models.NonCtorPropertiesNested>
{
    protected override Models.NonCtorPropertiesNested Expectation =>
        new() { Nested = new() { String = "aaa", Integer = 5 }, Double = 10.5 };
}

public class BuiltInMixed : Tests<Models.BuiltInMixed>
{
    protected override Models.BuiltInMixed Expectation => new("aaa", 5) { StringNonCtor = "bbb", IntNonCtor = 6 };
}

public class MixedNested : Tests<Models.MixedNested>
{
    protected override Models.MixedNested Expectation
        => new(new("aaa", 5) { StringNonCtor = "bbb", IntNonCtor = 6 }, 10.5)
        {
            NestedNonCtor = new("ccc", 7) { StringNonCtor = "ddd", IntNonCtor = 8 },
            DoubleNonCtor = 10.6
        };
}

public abstract class Tests<TExpectation>
{
    private static readonly MessagePackSerializerOptions options = ContractlessStandardResolver
        .Options
        .WithResolver(CompositeResolver.Create(
            CamelCaseContractlessFormatterResolver.Instance,
            ContractlessStandardResolver.Instance));
    private static readonly MessagePackSerializerOptions optionWithCompression =
        options.WithCompression(MessagePackCompression.Lz4BlockArray);
    protected abstract TExpectation Expectation { get; }

    [Test]
    public void SerializationAndDeserialization()
    {
        var serialized = MessagePackSerializer.Serialize(Expectation, options);
        TestContext.WriteLine(MessagePackSerializer.ConvertToJson(serialized));
        var result = MessagePackSerializer.Deserialize<TExpectation>(serialized.AsMemory(), options);
        Assert.That(result, Is.EqualTo(Expectation));
    }

    [Test]
    public void JustDeserialization()
    {
        var serialized = MessagePackSerializer.Serialize(Expectation, ContractlessStandardResolver.Options);
        var result = MessagePackSerializer.Deserialize<TExpectation>(serialized.AsMemory(), options);
        Assert.That(result, Is.EqualTo(Expectation));
    }

    [Test]
    public void Compression()
    {
        var serialized = MessagePackSerializer.Serialize(Expectation, optionWithCompression);
        TestContext.WriteLine(MessagePackSerializer.ConvertToJson(serialized));
        var result = MessagePackSerializer.Deserialize<TExpectation>(serialized.AsMemory(), optionWithCompression);
        Assert.That(result, Is.EqualTo(Expectation));
    }
}