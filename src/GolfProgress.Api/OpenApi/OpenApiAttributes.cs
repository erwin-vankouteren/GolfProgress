namespace GolfProgress.Api.OpenApi;

/// <summary>
/// Documents a string property, or each item of a list of strings, as taking one of
/// an enum's names (#62). The contracts
/// carry these as strings, parsed case-insensitively. It has no effect at runtime.
///
/// A <b>closed</b> enum (the default) lists its values as an OpenAPI <c>enum</c>, so
/// generated clients get a typed set. Installed apps then fail to read a response with
/// a value they don't know, so adding a value to a closed enum in a response is a
/// breaking change: raise the minimum app versions (GET /client-config) first.
///
/// An <b>open</b> enum (<see cref="Open"/>) is for response fields expected to gain
/// values (#134). It's published as a plain string, with the current values in its
/// description and in <c>x-known-values</c>; clients map the known ones and treat any
/// other value as unknown, so a new value can be added at any time.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class StringEnumAttribute : Attribute
{
    public StringEnumAttribute(Type enumType)
    {
        if (!enumType.IsEnum) throw new ArgumentException($"{enumType} is not an enum.", nameof(enumType));
        EnumType = enumType;
    }

    public Type EnumType { get; }

    /// <summary>Whether new values may be added without breaking installed apps (#134).</summary>
    public bool Open { get; init; }
}

/// <summary>
/// Documents a header an action sets on a response (#62), e.g. X-Total-Count on a
/// paged list or Location on 201 Created. It has no effect at runtime.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public sealed class ResponseHeaderAttribute : Attribute
{
    public ResponseHeaderAttribute(int statusCode, string name, string type, string description)
    {
        StatusCode = statusCode;
        Name = name;
        Type = type;
        Description = description;
    }

    public int StatusCode { get; }
    public string Name { get; }

    /// <summary>The OpenAPI schema type: "integer" or "string".</summary>
    public string Type { get; }

    public string Description { get; }
}
