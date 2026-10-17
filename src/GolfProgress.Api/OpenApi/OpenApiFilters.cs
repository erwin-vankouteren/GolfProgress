using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Reflection;

namespace GolfProgress.Api.OpenApi;

/// <summary>
/// Filters that make api/openapi.json complete enough to generate typed clients from
/// (#62). Facts that hold for whole groups of endpoints (authentication, rate limits,
/// server errors, response content types) are applied here, so a new endpoint is
/// documented correctly without anyone remembering to annotate it. Per-endpoint facts
/// (success type, 201/204, 400/404) are [ProducesResponseType] on the action.
/// </summary>
public static class OpenApiConfiguration
{
    public const string BearerScheme = "Bearer";

    /// <summary>Lists an open enum's current values in the spec (#134).</summary>
    public const string KnownValuesExtension = "x-known-values";

    public static void Configure(SwaggerGenOptions options)
    {
        options.SwaggerDoc("v1", new OpenApiInfo
        {
            Title = "GolfProgress API",
            Version = "v1",
            Description = "Golf progress tracking: rounds, practice, fitness scores, micro-wins and reviews."
        });

        // Nullable reference types (string?) become nullable in the spec, and
        // non-nullable ones don't, so clients get accurate optionality.
        options.SupportNonNullableReferenceTypes();

        options.AddSecurityDefinition(BearerScheme, new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            Description = "Access token from /auth/login, /auth/register or /auth/refresh."
        });

        options.OperationFilter<AuthorizationOperationFilter>();
        options.OperationFilter<RateLimitOperationFilter>();
        options.OperationFilter<ServerErrorOperationFilter>();
        options.OperationFilter<ResponseHeaderOperationFilter>();
        options.SchemaFilter<StringEnumSchemaFilter>();
        options.SchemaFilter<ResponseModelRequiredSchemaFilter>();
        options.SchemaFilter<NullableObjectSchemaFilter>();
        options.DocumentFilter<ContentTypeDocumentFilter>();
    }

    internal static OpenApiResponse ProblemResponse(OperationFilterContext context, string description) => new()
    {
        Description = description,
        Content =
        {
            ["application/problem+json"] = new OpenApiMediaType
            {
                Schema = context.SchemaGenerator.GenerateSchema(typeof(ProblemDetails), context.SchemaRepository)
            }
        }
    };
}

/// <summary>Marks operations that need a token, and documents their 401.</summary>
internal sealed class AuthorizationOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var metadata = context.ApiDescription.ActionDescriptor.EndpointMetadata;
        var requiresAuth = metadata.OfType<IAuthorizeData>().Any() && !metadata.OfType<IAllowAnonymous>().Any();
        if (!requiresAuth) return;

        operation.Security.Add(new OpenApiSecurityRequirement
        {
            [new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = OpenApiConfiguration.BearerScheme }
            }] = Array.Empty<string>()
        });

        operation.Responses.TryAdd("401", OpenApiConfiguration.ProblemResponse(
            context, "The access token is missing, invalid or expired. Refresh it and retry once."));
    }
}

/// <summary>Documents 429 and its Retry-After header on rate-limited operations.</summary>
internal sealed class RateLimitOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var limited = context.ApiDescription.ActionDescriptor.EndpointMetadata.OfType<EnableRateLimitingAttribute>().Any();
        if (!limited) return;

        var response = OpenApiConfiguration.ProblemResponse(context, "Too many requests. Wait for Retry-After before retrying.");
        response.Headers["Retry-After"] = new OpenApiHeader
        {
            Description = "Seconds to wait before retrying.",
            Schema = new OpenApiSchema { Type = "integer" }
        };
        operation.Responses.TryAdd("429", response);
    }
}

/// <summary>Every operation can fail unexpectedly; the global handler answers ProblemDetails (#7).</summary>
internal sealed class ServerErrorOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context) =>
        operation.Responses.TryAdd("500", OpenApiConfiguration.ProblemResponse(
            context, "Unexpected error. The traceId matches the server's log entry."));
}

/// <summary>Adds the headers declared with <see cref="ResponseHeaderAttribute"/>.</summary>
internal sealed class ResponseHeaderOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        foreach (var header in context.MethodInfo.GetCustomAttributes<ResponseHeaderAttribute>())
        {
            if (!operation.Responses.TryGetValue(header.StatusCode.ToString(), out var response))
                throw new InvalidOperationException(
                    $"{context.MethodInfo.DeclaringType?.Name}.{context.MethodInfo.Name} documents header " +
                    $"{header.Name} on {header.StatusCode}, but declares no {header.StatusCode} response.");

            response.Headers[header.Name] = new OpenApiHeader
            {
                Description = header.Description,
                Schema = new OpenApiSchema { Type = header.Type }
            };
        }
    }
}

/// <summary>Lists the allowed values of string properties marked with <see cref="StringEnumAttribute"/>.</summary>
internal sealed class StringEnumSchemaFilter : ISchemaFilter
{
    public void Apply(OpenApiSchema schema, SchemaFilterContext context)
    {
        var attribute = context.MemberInfo?.GetCustomAttribute<StringEnumAttribute>();
        if (attribute is null) return;

        // On a list of strings, the values apply to its items.
        if (schema.Type == "array" && schema.Items is not null) schema = schema.Items;

        var names = Enum.GetNames(attribute.EnumType);

        if (!attribute.Open)
        {
            schema.Enum = names.Select(name => (IOpenApiAny)new OpenApiString(name)).ToList();
            return;
        }

        // Open (#134): no `enum`, so generated clients keep a plain string and don't fail
        // on a value added later. The current values stay discoverable for clients to map.
        var known = new OpenApiArray();
        known.AddRange(names.Select(name => (IOpenApiAny)new OpenApiString(name)));
        schema.Extensions[OpenApiConfiguration.KnownValuesExtension] = known;
        schema.Description =
            $"Known values: {string.Join(", ", names)}. New values may be added at any time; " +
            "treat any other value as unknown.";
    }
}

/// <summary>
/// Response models always carry every property (null where there's no value), so all
/// their properties are required. Without this, generators make every field optional
/// and clients need casts or null checks for fields that are always present. Request
/// models are left alone: their required fields are marked with [Required].
/// </summary>
internal sealed class ResponseModelRequiredSchemaFilter : ISchemaFilter
{
    public void Apply(OpenApiSchema schema, SchemaFilterContext context)
    {
        var type = context.Type;
        var isResponseModel = type.IsClass
            && type.Namespace?.StartsWith("GolfProgress.Api.Contracts", StringComparison.Ordinal) == true
            && !type.Name.EndsWith("Request", StringComparison.Ordinal);

        if (isResponseModel && schema.Properties.Count > 0)
            schema.Required = new SortedSet<string>(schema.Properties.Keys, StringComparer.Ordinal);
    }
}

/// <summary>
/// A nullable property of an object type (DashboardResponse.LastRound is null before the
/// first round) becomes nullable in the spec. SupportNonNullableReferenceTypes can't say
/// so for these: OpenAPI 3.0 ignores anything next to a $ref, so the spec claimed they
/// were always present, and the generated Kotlin and Swift clients failed to decode a
/// new user's dashboard. The reference is wrapped in a one-item allOf, which can be
/// nullable; generators read it as the referenced type, optional.
/// </summary>
internal sealed class NullableObjectSchemaFilter : ISchemaFilter
{
    private static readonly NullabilityInfoContext Nullability = new();

    public void Apply(OpenApiSchema schema, SchemaFilterContext context)
    {
        if (schema.Properties.Count == 0) return;

        foreach (var property in context.Type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            var key = schema.Properties.Keys.FirstOrDefault(k => string.Equals(k, property.Name, StringComparison.OrdinalIgnoreCase));
            if (key is null || schema.Properties[key].Reference is not { } reference) continue;
            if (Nullability.Create(property).ReadState != NullabilityState.Nullable) continue;

            schema.Properties[key] = new OpenApiSchema
            {
                AllOf = { new OpenApiSchema { Reference = reference } },
                Nullable = true
            };
        }
    }
}

/// <summary>
/// The API reads and writes JSON only, and errors are ProblemDetails. Without this,
/// every body is listed under several content types (text/plain, text/json, ...), which
/// generators turn into a choice clients would have to handle.
/// </summary>
internal sealed class ContentTypeDocumentFilter : IDocumentFilter
{
    public void Apply(OpenApiDocument document, DocumentFilterContext context)
    {
        foreach (var operation in document.Paths.Values.SelectMany(p => p.Operations.Values))
        {
            if (operation.RequestBody is { } body)
                KeepOnly(body.Content, "application/json", "application/json");

            foreach (var (status, response) in operation.Responses)
            {
                var isError = int.TryParse(status, out var code) && code >= 400;
                KeepOnly(response.Content, "application/json", isError ? "application/problem+json" : "application/json");
            }
        }
    }

    private static void KeepOnly(IDictionary<string, OpenApiMediaType> content, string source, string target)
    {
        var media = content.TryGetValue(target, out var existing) ? existing
            : content.TryGetValue(source, out var json) ? json
            : null;

        content.Clear();
        if (media is not null) content[target] = media;
    }
}
