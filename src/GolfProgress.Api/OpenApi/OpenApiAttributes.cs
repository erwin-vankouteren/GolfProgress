namespace GolfProgress.Api.OpenApi;

/// <summary>
/// Documents a string property as taking one of an enum's names (#62). The contracts
/// carry these as strings, parsed case-insensitively; this lists the values in the
/// OpenAPI spec so generated clients get a typed set instead of a free string. It has
/// no effect at runtime.
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
