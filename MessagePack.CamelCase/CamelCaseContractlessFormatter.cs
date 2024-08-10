using MessagePack.Formatters;
using MessagePack.Resolvers;
using System.Linq.Expressions;
using System.Reflection;

namespace MessagePack.CamelCase;

public sealed class CamelCaseContractlessFormatter<T> : IMessagePackFormatter<T>
{
    public static readonly CamelCaseContractlessFormatter<T>? Instance;
    private static readonly DeserializeDelegate deserialize;

    static CamelCaseContractlessFormatter()
    {
        if (typeof(T) == typeof(object) || BuiltinResolver.Instance.GetFormatter<T>() is null)
        {
            deserialize = CreateDeserializeDelegate();
            Instance = new();
        }
        else
        {
            deserialize = null!;
        }
    }

    private CamelCaseContractlessFormatter()
    {
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

    private delegate T DeserializeDelegate(ref MessagePackReader reader, MessagePackSerializerOptions options);

    private static MethodCallExpression IgnoreCaseEqual(Expression one, Expression other)
        => Expression.Call(
            typeof(string),
            nameof(string.Equals),
            null,
            [one, other, Expression.Constant(StringComparison.OrdinalIgnoreCase)]);

    private static DeserializeDelegate CreateDeserializeDelegate()
    {
        var type = TypeInfo.Create<T>();
        var reader = Expression.Parameter(typeof(MessagePackReader).MakeByRefType(), "reader");
        var options = Expression.Parameter(typeof(MessagePackSerializerOptions), "options");

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

        var resolver = Expression.Property(options, nameof(MessagePackSerializerOptions.Resolver));
        var readPropertyName = Expression.Call(reader, nameof(MessagePackReader.ReadString), null, null);
        MethodCallExpression GetFormatter(Type typeParameter)
            => Expression.Call(resolver, "GetFormatter", [typeParameter], null);
        MethodCallExpression Deserialize(Type type)
            => Expression.Call(GetFormatter(type), "Deserialize", null, [reader, options]);
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
        var mapLengthLocal = Expression.Variable(typeof(int), "length");
        var mapIndexLocal = Expression.Variable(typeof(int), "index");
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
        var lambda = Expression.Lambda<DeserializeDelegate>(body, reader, options);
        return lambda.Compile();
    }

    public T Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
    {
        options.Security.DepthStep(ref reader);
        var result = deserialize(ref reader, options);
        reader.Depth--;
        return result;
    }

    public void Serialize(ref MessagePackWriter writer, T value, MessagePackSerializerOptions options)
    {
        throw new NotImplementedException();
    }
}
