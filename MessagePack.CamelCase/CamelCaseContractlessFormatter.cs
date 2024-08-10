﻿using MessagePack.Formatters;
using System.Linq.Expressions;
using System.Reflection;

namespace MessagePack.CamelCase;

public class CamelCaseContractlessFormatter<T> : IMessagePackFormatter<T>
{
    private static MethodCallExpression IgnoreCaseEqual(Expression one, Expression other) =>
        Expression.Call(
            typeof(string),
            nameof(string.Equals),
            null,
            [one, other, Expression.Constant(StringComparison.OrdinalIgnoreCase)]);

    private sealed class SetterLocal : Local
    {
        public required MethodInfo Setter { get; init; }
    }

    private class Local
    {
        public required Type Type { get; init; }
        public required string Name { get; init; }
        public ParameterExpression ParameterExpression 
            => Expression.Parameter(Type, Name);
    }

    private delegate T DeserializeDelegate(ref MessagePackReader reader, MessagePackSerializerOptions options);

    private static void Test()
    {
        var type = TypeInfo.Create<T>();
        var reader = Expression.Parameter(typeof(MessagePackReader).MakeByRefType(), "reader");
        var options = Expression.Parameter(typeof(MessagePackSerializerOptions), "options");
        var resolver = Expression.Property(options, nameof(MessagePackSerializerOptions.Resolver));
        var nextMessageTypeIsString = Expression.Equal(
            Expression.Property(reader, nameof(MessagePackReader.NextMessagePackType)),
            Expression.Constant(MessagePackType.String));
        var readPropertyName = Expression.Call(reader, nameof(MessagePackReader.ReadString), null, null);

        MethodCallExpression GetFormatter(Type typeParameter)
            => Expression.Call(resolver, "GetFormatter", [typeParameter], null);
        MethodCallExpression Deserialize(Type type)
            => Expression.Call(GetFormatter(type), "Deserialize", null, [reader, options]);

        var ctorLocals = type
            .Constructor
            .GetParameters()
            .Select(param => new Local { Type = param.ParameterType, Name = param.Name })// TODO When is this null?
            .ToArray();
        var setterLocals = type
            .SettersWithNoMatchingConstructorParameter
            .Select(setter => new SetterLocal { Type = setter.PropertyType, Name = setter.Name, Setter = setter.SetMethod! })
            .ToArray();

        Expression CreateResult()
        {
            var ctorCall = Expression.New(
                type.Constructor,
                ctorLocals.Select(local => local.ParameterExpression));
            var sets = setterLocals
                .Select(local => Expression.Bind(local.Setter, local.ParameterExpression));
            return Expression.MemberInit(ctorCall, sets);
        }

        var locals = ctorLocals.Concat(setterLocals);
        Expression ReadAndAssignPropertyToLocal()
        {
            var propertyNameLocal = Expression.Parameter(typeof(string));
            var trySetLocal = locals.Aggregate(
                Expression.Empty() as Expression,
                (otherwise, local) => Expression.IfThenElse(
                    IgnoreCaseEqual(propertyNameLocal, Expression.Constant(local.Name)),
                    Expression.Assign(
                        local.ParameterExpression,
                        Deserialize(local.Type)),
                    otherwise));
            return Expression.Block(
                [propertyNameLocal],
                Expression.Assign(propertyNameLocal, readPropertyName),
                trySetLocal);
        }

        LabelTarget resultLabel = Expression.Label(type.ClrType);
        var body = Expression.Block(
            locals.Select(local => local.ParameterExpression),
            Expression.Loop(
                Expression.IfThenElse(
                    nextMessageTypeIsString,
                    ReadAndAssignPropertyToLocal(),
                    Expression.Break(resultLabel, CreateResult())),
                resultLabel));
        var lambda = Expression.Lambda<DeserializeDelegate>(body, reader, options);
        return lambda.Compile();
    }



    public T Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
    {
        reader.ReadMapHeader();
        options.Security.DepthStep(ref reader);
        reader.Depth--;
        throw new NotImplementedException();
    }

    public void Serialize(ref MessagePackWriter writer, T value, MessagePackSerializerOptions options)
    {
        throw new NotImplementedException();
    }
}
