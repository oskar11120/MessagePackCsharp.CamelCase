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

    public enum Enum
    {
        First,
        Second
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

public class Enum : Tests<Models.Enum>
{
    protected override Models.Enum Expectation => Models.Enum.Second;

    [Test]
    public void DeserializationFromCamelCaseText()
    {
        var serialized = MessagePackSerializer.ConvertFromJson("\"second\"");
        var result = MessagePackSerializer.Deserialize<Models.Enum>(serialized.AsMemory(), camelCaseOptions);
        Assert.That(result, Is.EqualTo(Models.Enum.Second));
    }
}

public class BuiltInMixed : Tests<Models.BuiltInMixed>
{
    protected override Models.BuiltInMixed Expectation => new("aaa", 5) { StringNonCtor = "bbb", IntNonCtor = 6 };
}

public abstract class Tests<TExpectation>
{
    protected static readonly MessagePackSerializerOptions camelCaseOptions = ContractlessStandardResolver
        .Options
        .WithResolver(CompositeResolver.Create(
            CamelCaseContractlessFormatterResolver.Instance,
            ContractlessStandardResolver.Instance));
    protected abstract TExpectation Expectation { get; }

    [Test]
    public void SerializationAndDeserialization() => Test(camelCaseOptions);

    [Test]
    public void JustDeserialization() => Test(
        ContractlessStandardResolver.Options.WithResolver(CompositeResolver.Create(
            DynamicEnumAsStringResolver.Instance,
            ContractlessStandardResolver.Instance)), 
        camelCaseOptions);

    [Test]
    public void Compression() => Test(camelCaseOptions.WithCompression(MessagePackCompression.Lz4BlockArray));

    private void Test(
        MessagePackSerializerOptions serializeOptions,
        MessagePackSerializerOptions? deserializeOptions = null)
    {
        deserializeOptions ??= serializeOptions;
        var serialized = MessagePackSerializer.Serialize(Expectation, serializeOptions);
        TestContext.WriteLine(MessagePackSerializer.ConvertToJson(serialized));
        var result = MessagePackSerializer.Deserialize<TExpectation>(serialized.AsMemory(), deserializeOptions);
        Assert.That(result, Is.EqualTo(Expectation));
    }
}