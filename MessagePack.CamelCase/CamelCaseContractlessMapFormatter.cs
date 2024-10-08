﻿using MessagePack.Formatters;
using MessagePack.Resolvers;
using System.Collections;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;

namespace MessagePack.CamelCase;

public sealed class CamelCaseContractlessMapFormatter<T> : IMessagePackFormatter<T>
{
    public static readonly CamelCaseContractlessMapFormatter<T>? Instance =
        typeof(T).GetInterface(nameof(IEnumerable)) is null &&
        typeof(T).IsEnum is false &&
        BuiltinResolver.Instance.GetFormatter<T>() is null ?
           new() : null;

    private delegate void SerializeDelegate(ref MessagePackWriter writer, T value, MessagePackSerializerOptions options);
    private delegate T DeserializeDelegate(ref MessagePackReader reader, MessagePackSerializerOptions options);

    private readonly SerializeDelegate serialize;
    private readonly DeserializeDelegate deserialize;

    private CamelCaseContractlessMapFormatter()
    {
        deserialize = CreateDeserializeDelegate();
        serialize = CreateSerializeDelegate();
    }

    private sealed class SetterLocal : Local
    {
        public required MethodInfo Setter { get; init; }
    }

    private class Local
    {
        public required Type Type { get; init; }
        public required string Name { get; init; }
        private ParameterExpression? expression;
        public ParameterExpression Expression
            => expression ??= System.Linq.Expressions.Expression.Variable(Type, Name);
    }

    private static MethodCallExpression IgnoreCaseEqual(Expression one, Expression other)
        => Expression.Call(
            typeof(string),
            nameof(string.Equals),
            null,
            [one, other, Expression.Constant(StringComparison.OrdinalIgnoreCase)]);

    private static class OptionsExpressions
    {
        public static readonly ParameterExpression Options
            = Expression.Parameter(typeof(MessagePackSerializerOptions), "options");
        public static readonly MemberExpression Resolver
            = Expression.Property(Options, nameof(MessagePackSerializerOptions.Resolver));
        public static MethodCallExpression GetFormatter(Type typeParameter)
            => Expression.Call(Resolver, "GetFormatter", [typeParameter], null);
    }

    private static DeserializeDelegate CreateDeserializeDelegate()
    {
        var type = DeserializeTypeInfo.Create<T>();
        var reader = Expression.Parameter(typeof(MessagePackReader).MakeByRefType(), "reader");

        var ctorLocals = type
            .Constructor
            .GetParameters()
            .Select(param => new Local { Type = param.ParameterType, Name = param.Name! })// TODO When is this null?
            .ToArray();
        var setterLocals = type
            .SettersWithNoMatchingConstructorParameter
            .Select(setter => new SetterLocal { Type = setter.PropertyType, Name = setter.Name, Setter = setter.SetMethod! })
            .ToArray();
        Expression CreateResult()
        {
            var ctorCall = Expression.New(
                type.Constructor,
                ctorLocals.Select(local => local.Expression));
            var sets = setterLocals
                .Select(local => Expression.Bind(local.Setter, local.Expression));
            return Expression.MemberInit(ctorCall, sets);
        }

        var readPropertyName = Expression.Call(reader, nameof(MessagePackReader.ReadString), null, null);
        MethodCallExpression Deserialize(Type type)
            => Expression.Call(OptionsExpressions.GetFormatter(type), "Deserialize", null, [reader, OptionsExpressions.Options]);
        var locals = ctorLocals.Concat(setterLocals);
        Expression ReadAndAssignPropertyToLocal()
        {
            var propertyNameLocal = Expression.Variable(typeof(string));
            var trySetLocal = locals.Reverse().Aggregate(
                Expression.Empty() as Expression,
                (otherwise, local) => Expression.IfThenElse(
                    IgnoreCaseEqual(propertyNameLocal, Expression.Constant(local.Name)),
                    Expression.Assign(
                        local.Expression,
                        Deserialize(local.Type)),
                    otherwise));
            return Expression.Block(
                [propertyNameLocal],
                Expression.Assign(propertyNameLocal, readPropertyName),
                trySetLocal);
        }

        var readMapHeader = Expression.Call(reader, nameof(MessagePackReader.ReadMapHeader), null, null);
        var mapLengthLocal = Expression.Variable(typeof(int));
        var mapIndexLocal = Expression.Variable(typeof(int));
        var incrementIndex_anythingLeft = Expression.Block(
            Expression.Assign(mapIndexLocal, Expression.Increment(mapIndexLocal)),
            Expression.LessThanOrEqual(mapIndexLocal, mapLengthLocal));
        var resultLabel = Expression.Label(type.ClrType);
        var body = Expression.Block(
            locals
                .Select(local => local.Expression)
                .Append(mapLengthLocal)
                .Append(mapIndexLocal),
            Expression.Assign(mapLengthLocal, readMapHeader),
            Expression.Assign(mapIndexLocal, Expression.Constant(0)),
            Expression.Loop(
                Expression.IfThenElse(
                    incrementIndex_anythingLeft,
                    ReadAndAssignPropertyToLocal(),
                    Expression.Break(resultLabel, CreateResult())),
                resultLabel));
        var lambda = Expression.Lambda<DeserializeDelegate>(body, reader, OptionsExpressions.Options);
        return lambda.Compile();
    }

    public T Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
    {
        options.Security.DepthStep(ref reader);
        var result = deserialize(ref reader, options);
        reader.Depth--;
        return result;
    }

    private static SerializeDelegate CreateSerializeDelegate()
    {
        var type = SerializeTypeInfo.Create<T>();
        var writer = Expression.Parameter(typeof(MessagePackWriter).MakeByRefType(), "writer");

        var writeMapHeader = Expression.Call(
            writer,
            nameof(MessagePackWriter.WriteMapHeader),
            null,
            Expression.Constant(type.Getters.Length));

        var spanCtor = typeof(ReadOnlySpan<byte>).GetConstructor([typeof(byte[])])!;
        Expression WriteName(PropertyInfo getter)
        {
            var nameCamel = char.ToLowerInvariant(getter.Name[0]) + getter.Name[1..];
            var bytesCamel = Encoding.UTF8.GetBytes(nameCamel);
            var bytesCamelSpan = Expression.New(spanCtor, Expression.Constant(bytesCamel));
            return Expression.Call(writer, nameof(MessagePackWriter.WriteString), null, bytesCamelSpan);
        }
        MethodCallExpression Serialize(Type type, Expression value) => Expression.Call(
            OptionsExpressions.GetFormatter(type),
            "Serialize",
            null,
            [writer, value, OptionsExpressions.Options]);
        var value = Expression.Parameter(typeof(T), "value");
        var writes = type
            .Getters
            .SelectMany(getter => new[]
            {
                WriteName(getter),
                Serialize(getter.PropertyType, Expression.Property(value, getter))
            });

        var body = Expression.Block(writes.Prepend(writeMapHeader));
        var lambda = Expression.Lambda<SerializeDelegate>(body, writer, value, OptionsExpressions.Options);
        return lambda.Compile();
    }

    public void Serialize(ref MessagePackWriter writer, T value, MessagePackSerializerOptions options)
        => serialize(ref writer, value, options);
}
