# (Not only) camel case support for [MessagePack](https://github.com/MessagePack-CSharp/MessagePack-CSharp).
1. Contractless camel case serialization of properties to MessagePack map keys.
2. Dynamic serialization of enum values to string literals using [MessagePack](https://github.com/MessagePack-CSharp/MessagePack-CSharp)'s `DynamicEnumAsStringResolver`.
3. Dynamic case insensitive deserialization of MessagePack map keys and of string literals to Enum values.

## Api
Use:
- `CamelCaseContractlessFormatterResolver.Instance` for all the features, 
- `CamelCaseAllowingDynamicEnumAsStringFormatter<T>.Instance` for just enums, 
- `CamelCaseContractlessMapFormatter<T>.Instance` for just maps.

`Formatter<T>.Instance` are `null` where `T` is not suitable for use with given `Formatter`.