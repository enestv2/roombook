using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Roombook.Api.Controllers;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Roombook.Api;

public static class SwaggerConfiguration
{
    public static void AddRoombookSwagger(this IServiceCollection services)
    {
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "Roombook API",
                Version = "v1",
                Description = """
                    Room discovery and conflict-free meeting-space reservations.

                    Production authentication uses the bearer token issued by `/api/auth`.
                    Development hosts use the local-only `X-Development-Member-Id` header
                    for member booking requests; that header is never registered as a
                    production authentication mechanism.
                    """
            });
            options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Name = "Authorization",
                In = ParameterLocation.Header,
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                Description = "Production ASP.NET Core Identity opaque bearer token. Send an Authorization bearer token obtained from /api/auth/login or /api/auth/refresh. Tokens are not JWTs."
            });
            options.SupportNonNullableReferenceTypes();
            options.TagActionsBy(api => new[] { api.GroupName ?? api.ActionDescriptor.RouteValues["controller"] ?? "API" });
            options.SchemaFilter<RoombookSchemaFilter>();
            options.OperationFilter<RoombookOperationFilter>();
            options.DocumentFilter<RoombookDocumentFilter>();
        });
    }
}

internal sealed class RoombookSchemaFilter : ISchemaFilter
{
    public void Apply(OpenApiSchema schema, SchemaFilterContext context)
    {
        if (context.Type == typeof(DateTimeOffset))
        {
            schema.Format = "date-time";
            schema.Description = "Offset-bearing timestamp; send UTC (`Z`) or an explicit offset.";
            schema.Example = new OpenApiString("2030-01-12T09:00:00Z");
        }
        else if (context.Type == typeof(TimeOnly))
        {
            schema.Format = "time";
            schema.Description = "Time of day in HH:mm:ss format.";
            schema.Example = new OpenApiString("09:00:00");
        }
        else if (context.Type == typeof(CreateBookingRequest))
        {
            schema.Description = "A booking interval. Timestamps must include an explicit UTC offset and use 15-minute boundaries.";
            schema.Required = new HashSet<string>(new[] { "roomId", "startsAt", "endsAt" });
            SetProperty(schema, "roomId", "Room to reserve.", "00000000-0000-0000-0000-000000000001", "uuid");
            SetProperty(schema, "startsAt", "Start timestamp with `Z` or an explicit offset.", "2030-01-12T09:00:00Z", "date-time");
            SetProperty(schema, "endsAt", "End timestamp with `Z` or an explicit offset. Duration must be 15 minutes to 4 hours.", "2030-01-12T10:00:00Z", "date-time");
        }
        else if (context.Type == typeof(ConflictResponse))
        {
            schema.Description = "Conflict details intentionally omit the identity of the member holding the conflicting booking.";
        }
        else if (context.Type == typeof(ValidationProblemDetails))
        {
            schema.Description = "Field-level validation errors. Unexpected failures use the sanitized ProblemDetails shape instead.";
            AddCorrelationId(schema);
            AddErrorCode(schema);
            schema.Example = new OpenApiObject
            {
                ["title"] = new OpenApiString("Validation failed."),
                ["status"] = new OpenApiInteger(400),
                ["code"] = new OpenApiString("validation.failed"),
                ["errors"] = new OpenApiObject
                {
                    ["field"] = new OpenApiArray { new OpenApiString("The value is invalid.") }
                },
                ["errorCodes"] = new OpenApiObject
                {
                    ["field"] = new OpenApiArray { new OpenApiString("validation.invalid") }
                },
                ["correlationId"] = new OpenApiString("00000000-0000-0000-0000-000000000099")
            };
        }
        else if (context.Type == typeof(ProblemDetails))
        {
            schema.Description = "Sanitized error response. Internal exception details are never exposed; use correlationId when contacting support.";
            AddCorrelationId(schema);
            AddErrorCode(schema);
            schema.Example = new OpenApiObject
            {
                ["title"] = new OpenApiString("The request could not be completed."),
                ["status"] = new OpenApiInteger(500),
                ["code"] = new OpenApiString("request.unexpected"),
                ["correlationId"] = new OpenApiString("00000000-0000-0000-0000-000000000099")
            };
        }

        switch (context.Type.Name)
        {
            case nameof(RoomResponse):
                schema.Required = Required("id", "name", "timeZone", "workingPeriods");
                break;
            case nameof(WorkingPeriodResponse):
                schema.Required = Required("start", "end");
                break;
            case nameof(BookingResponse):
                schema.Required = Required("id", "roomId", "memberId", "startsAtUtc", "endsAtUtc",
                    "startsAtLocal", "endsAtLocal", "timeZone");
                break;
            case nameof(AlternativeResponse):
                schema.Required = Required("startsAtUtc", "endsAtUtc");
                break;
            case nameof(ConflictResponse):
                schema.Required = Required("roomId", "requestedStartUtc", "requestedEndUtc",
                    "conflictingStartUtc", "conflictingEndUtc", "alternatives", "timeZone", "code", "message");
                break;
            case nameof(ValidationProblemDetails):
                schema.Required = Required("errors", "code", "errorCodes");
                break;
        }
    }

    private static HashSet<string> Required(params string[] names) => new(names);

    private static void AddCorrelationId(OpenApiSchema schema)
    {
        schema.Properties["correlationId"] = new OpenApiSchema
        {
            Type = "string",
            Description = "Correlation identifier returned in the X-Correlation-Id response header.",
            Example = new OpenApiString("00000000-0000-0000-0000-000000000099")
        };
    }

    private static void AddErrorCode(OpenApiSchema schema)
    {
        schema.Properties["code"] = new OpenApiSchema
        {
            Type = "string",
            Description = "Stable machine-readable error code. Human-readable messages vary by Accept-Language.",
            Example = new OpenApiString("request.unexpected")
        };
        schema.Properties["errorCodes"] = new OpenApiSchema
        {
            Type = "object",
            Description = "Stable field-level error codes parallel to the errors property.",
            AdditionalPropertiesAllowed = true,
            AdditionalProperties = new OpenApiSchema
            {
                Type = "array",
                Items = new OpenApiSchema { Type = "string" }
            }
        };
    }

    private static void SetProperty(OpenApiSchema schema, string name, string description, string example, string format)
    {
        if (!schema.Properties.TryGetValue(name, out var property))
            return;

        property.Description = description;
        property.Format = format;
        property.Example = new OpenApiString(example);
    }
}

internal sealed class RoombookOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var route = context.ApiDescription.RelativePath ?? string.Empty;
        if (route.Equals("api/rooms", StringComparison.OrdinalIgnoreCase))
        {
            operation.Summary = "List active rooms";
            operation.Description = "Returns active rooms and their configured working periods. Room times are local to each room's time zone.";
        }
        else if (route.Equals("api/bookings", StringComparison.OrdinalIgnoreCase)
            && context.ApiDescription.HttpMethod?.Equals("POST", StringComparison.OrdinalIgnoreCase) == true)
        {
            operation.Summary = "Create a booking";
            operation.Description = "Creates a member booking when the room is available. Timestamps must include `Z` or an explicit UTC offset, align to 15-minute boundaries, and span between 15 minutes and 4 hours. Conflict details never identify the other member.";
        }
        else if (route.StartsWith("api/bookings/", StringComparison.OrdinalIgnoreCase))
        {
            operation.Summary = "Get a booking";
            operation.Description = "Returns a booking by identifier. This endpoint currently returns not found when the booking is unavailable.";
            var idParameter = operation.Parameters.FirstOrDefault(x => x.Name.Equals("id", StringComparison.OrdinalIgnoreCase));
            if (idParameter is not null)
                idParameter.Description = "Booking identifier.";
        }

        if (context.ApiDescription.ActionDescriptor.EndpointMetadata.OfType<IAuthorizeData>().Any())
        {
            operation.Security = new List<OpenApiSecurityRequirement>
            {
                new()
                {
                    [new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference
                        {
                            Type = ReferenceType.SecurityScheme,
                            Id = "Bearer"
                        }
                    }] = Array.Empty<string>()
                }
            };
        }

        AddExamples(operation, context);
    }

    private static void AddExamples(OpenApiOperation operation, OperationFilterContext context)
    {
        if (context.ApiDescription.HttpMethod?.Equals("POST", StringComparison.OrdinalIgnoreCase) == true
            && context.ApiDescription.RelativePath?.StartsWith("api/bookings", StringComparison.OrdinalIgnoreCase) == true)
        {
            if (operation.RequestBody?.Content.TryGetValue("application/json", out var request) == true)
                request.Example = BookingRequestExample();

            SetExample(operation, "201", BookingResponseExample());
            SetExample(operation, "400", ValidationProblemExample());
            SetExample(operation, "409", ConflictExample());
            SetExample(operation, "401", ProblemExample("Authentication is required."));
            SetExample(operation, "403", ProblemExample("You are not authorized to perform this action."));
            SetExample(operation, "500", ProblemExample("An unexpected error occurred."));
        }
        else if (context.ApiDescription.HttpMethod?.Equals("GET", StringComparison.OrdinalIgnoreCase) == true
            && context.ApiDescription.RelativePath?.Equals("api/rooms", StringComparison.OrdinalIgnoreCase) == true)
        {
            SetExample(operation, "200", RoomsExample());
            SetExample(operation, "500", ProblemExample("An unexpected error occurred."));
        }
        else if (context.ApiDescription.HttpMethod?.Equals("GET", StringComparison.OrdinalIgnoreCase) == true)
        {
            SetExample(operation, "401", ProblemExample("Authentication is required."));
            SetExample(operation, "403", ProblemExample("You are not authorized to perform this action."));
            SetExample(operation, "404", ProblemExample("The requested resource was not found."));
            SetExample(operation, "500", ProblemExample("An unexpected error occurred."));
        }
    }

    private static void SetExample(OpenApiOperation operation, string statusCode, IOpenApiAny example)
    {
        if (!operation.Responses.TryGetValue(statusCode, out var response))
            return;

        foreach (var content in response.Content.Values)
            if (content.Schema is not null)
                content.Example = example;
    }

    private static OpenApiObject BookingRequestExample() => new()
    {
        ["roomId"] = new OpenApiString("00000000-0000-0000-0000-000000000001"),
        ["startsAt"] = new OpenApiString("2030-01-12T09:00:00Z"),
        ["endsAt"] = new OpenApiString("2030-01-12T10:00:00Z")
    };

    private static OpenApiObject BookingResponseExample() => new()
    {
        ["id"] = new OpenApiString("00000000-0000-0000-0000-000000000010"),
        ["roomId"] = new OpenApiString("00000000-0000-0000-0000-000000000001"),
        ["memberId"] = new OpenApiString("00000000-0000-0000-0000-000000000020"),
        ["startsAtUtc"] = new OpenApiString("2030-01-12T09:00:00Z"),
        ["endsAtUtc"] = new OpenApiString("2030-01-12T10:00:00Z"),
        ["startsAtLocal"] = new OpenApiString("2030-01-12T09:00:00+00:00"),
        ["endsAtLocal"] = new OpenApiString("2030-01-12T10:00:00+00:00"),
        ["timeZone"] = new OpenApiString("UTC")
    };

    private static OpenApiArray RoomsExample() => new()
    {
        new OpenApiObject
        {
            ["id"] = new OpenApiString("00000000-0000-0000-0000-000000000001"),
            ["name"] = new OpenApiString("Default room"),
            ["timeZone"] = new OpenApiString("UTC"),
            ["workingPeriods"] = new OpenApiArray
            {
                new OpenApiObject
                {
                    ["start"] = new OpenApiString("08:00:00"),
                    ["end"] = new OpenApiString("18:00:00")
                }
            }
        }
    };

    private static OpenApiObject ConflictExample() => new()
    {
        ["roomId"] = new OpenApiString("00000000-0000-0000-0000-000000000001"),
        ["requestedStartUtc"] = new OpenApiString("2030-01-12T09:00:00Z"),
        ["requestedEndUtc"] = new OpenApiString("2030-01-12T10:00:00Z"),
        ["conflictingStartUtc"] = new OpenApiString("2030-01-12T09:00:00Z"),
        ["conflictingEndUtc"] = new OpenApiString("2030-01-12T09:30:00Z"),
        ["alternatives"] = new OpenApiArray
        {
            new OpenApiObject
            {
                ["startsAtUtc"] = new OpenApiString("2030-01-12T10:00:00Z"),
                ["endsAtUtc"] = new OpenApiString("2030-01-12T11:00:00Z")
            }
        },
        ["timeZone"] = new OpenApiString("UTC"),
        ["message"] = new OpenApiString("The requested interval is already booked."),
        ["code"] = new OpenApiString("booking.conflict")
    };

    private static OpenApiObject ValidationProblemExample() => new()
    {
        ["type"] = new OpenApiString("https://tools.ietf.org/html/rfc7231#section-6.5.1"),
        ["title"] = new OpenApiString("Booking validation failed."),
        ["status"] = new OpenApiInteger(400),
        ["code"] = new OpenApiString("validation.failed"),
        ["errors"] = new OpenApiObject
        {
            ["startsAt"] = new OpenApiArray { new OpenApiString("Start must be on a 15-minute boundary.") }
        },
        ["errorCodes"] = new OpenApiObject
        {
            ["startsAt"] = new OpenApiArray { new OpenApiString("booking.start_not_aligned") }
        },
        ["correlationId"] = new OpenApiString("00000000-0000-0000-0000-000000000099")
    };

    private static OpenApiObject ProblemExample(string title) => new()
    {
        ["title"] = new OpenApiString(title),
        ["status"] = new OpenApiInteger(title.Contains("Authentication", StringComparison.Ordinal) ? 401
            : title.Contains("authorized", StringComparison.Ordinal) ? 403
            : title.Contains("found", StringComparison.Ordinal) ? 404 : 500),
        ["detail"] = new OpenApiString("The request could not be completed."),
        ["code"] = new OpenApiString(title.Contains("Authentication", StringComparison.Ordinal) ? "authorization.required"
            : title.Contains("authorized", StringComparison.Ordinal) ? "authorization.forbidden"
            : title.Contains("found", StringComparison.Ordinal) ? "resource.not_found" : "request.unexpected"),
        ["correlationId"] = new OpenApiString("00000000-0000-0000-0000-000000000099")
    };
}

internal sealed class RoombookDocumentFilter : IDocumentFilter
{
    private readonly IConfiguration configuration;

    public RoombookDocumentFilter(IConfiguration configuration)
    {
        this.configuration = configuration;
    }

    public void Apply(OpenApiDocument document, DocumentFilterContext context)
    {
        if (string.IsNullOrWhiteSpace(configuration.GetConnectionString("Roombook")))
            return;

        AddAuthenticationSchemas(document);
        AddRegister(document);
        AddLogin(document);
        AddRefresh(document);
        AddEmailEndpoints(document);
        AddPasswordEndpoints(document);
        AddManageEndpoints(document);
    }

    private static void AddAuthenticationSchemas(OpenApiDocument document)
    {
        AddSchema(document, "RegisterRequest", "Credentials used to create a production member account.",
            ("email", StringSchema("Email address.", "member@example.com")),
            ("password", StringSchema("Password; production policy requires at least 12 characters, upper/lowercase, a digit, and a symbol.", "P@ssword1234!")));
        AddSchema(document, "LoginRequest", "Production member credentials.",
            ("email", StringSchema("Email address.", "member@example.com")),
            ("password", StringSchema("Member password.", "P@ssword1234!")),
            ("twoFactorCode", StringSchema("Optional authenticator code.", "123456")),
            ("twoFactorRecoveryCode", StringSchema("Optional one-time recovery code.", "recovery-code")));
        document.Components.Schemas["LoginRequest"].Required = new HashSet<string> { "email", "password" };
        AddSchema(document, "RefreshRequest", "Opaque refresh token returned by the login endpoint.",
            ("refreshToken", StringSchema("Opaque refresh token; not a JWT.", "opaque-refresh-token-example")));
        AddSchema(document, "AuthTokenResponse", "Opaque bearer and refresh tokens issued by ASP.NET Core Identity.",
            ("tokenType", StringSchema("Authorization token type.", "Bearer")),
            ("accessToken", StringSchema("Opaque access token. It is not a JWT.", "opaque-access-token-example")),
            ("expiresIn", new OpenApiSchema { Type = "integer", Format = "int32", Description = "Access-token lifetime in seconds.", Example = new OpenApiInteger(3600) }),
            ("refreshToken", StringSchema("Opaque refresh token used to obtain a replacement access token.", "opaque-refresh-token-example")));
        AddSchema(document, "EmailRequest", "Member email address.",
            ("email", StringSchema("Email address.", "member@example.com")));
        AddSchema(document, "ResetPasswordRequest", "Password reset values.",
            ("email", StringSchema("Member email address.", "member@example.com")),
            ("resetCode", StringSchema("One-time reset code.", "reset-code-example")),
            ("newPassword", StringSchema("New password; production policy applies.", "N3wP@ssword1234!")));
        AddSchema(document, "ManageInfoResponse", "Authenticated member account information.",
            ("email", StringSchema("Member email address.", "member@example.com")),
            ("isEmailConfirmed", new OpenApiSchema { Type = "boolean", Example = new OpenApiBoolean(true) }));
        AddSchema(document, "UpdateInfoRequest", "Changes to authenticated member account information.",
            ("newEmail", StringSchema("Optional replacement email address.", "new-member@example.com")),
            ("newPassword", StringSchema("Optional replacement password.", "N3wP@ssword1234!")),
            ("oldPassword", StringSchema("Current password required when changing the password.", "P@ssword1234!")));
        document.Components.Schemas["UpdateInfoRequest"].Required = new HashSet<string>();
        AddSchema(document, "TwoFactorInfo", "Authenticated member two-factor authentication state.",
            ("sharedKey", StringSchema("Authenticator shared key.", "JBSWY3DPEHPK3PXP")),
            ("recoveryCodes", new OpenApiSchema { Type = "array", Items = StringSchema("Recovery code.", "11111111") }),
            ("recoveryCodesLeft", new OpenApiSchema { Type = "integer", Format = "int32", Example = new OpenApiInteger(10) }),
            ("isTwoFactorEnabled", new OpenApiSchema { Type = "boolean", Example = new OpenApiBoolean(false) }),
            ("isMachineRemembered", new OpenApiSchema { Type = "boolean", Example = new OpenApiBoolean(false) }));
        document.Components.Schemas["TwoFactorInfo"].Required = new HashSet<string>();
        AddSchema(document, "TwoFactorRequest", "Two-factor authentication changes.",
            ("enable", new OpenApiSchema { Type = "boolean" }),
            ("resetSharedKey", new OpenApiSchema { Type = "boolean" }),
            ("resetRecoveryCodes", new OpenApiSchema { Type = "boolean" }),
            ("forgetMachine", new OpenApiSchema { Type = "boolean" }),
            ("twoFactorCode", StringSchema("Authenticator code used to enable two-factor authentication.", "123456")));
        document.Components.Schemas["TwoFactorRequest"].Required = new HashSet<string>();
    }

    private static void AddRegister(OpenApiDocument document) =>
        AddOperation(document, "/api/auth/register", OperationType.Post, "Register a member",
            "Creates a production member account. Authentication endpoints are available when PostgreSQL-backed Identity is configured.",
            "RegisterRequest", "200", null, false);

    private static void AddLogin(OpenApiDocument document)
    {
        AddOperation(document, "/api/auth/login", OperationType.Post, "Sign in",
            "When both flags are false (the default), returns opaque ASP.NET Core Identity bearer and refresh tokens. When either useCookies=true or useSessionCookies=true, authenticates with an HTTP-only cookie instead; useSessionCookies=true makes that cookie session-scoped. Cookie mode does not return tokens in the response body.",
            "LoginRequest", "200", "AuthTokenResponse", false,
            QueryBoolean("useCookies", "When true, authenticate with a cookie instead of returning bearer and refresh tokens. useSessionCookies=true also selects cookie mode.", false),
            QueryBoolean("useSessionCookies", "When true, authenticate with an HTTP-only session cookie instead of returning bearer and refresh tokens, even if useCookies is false; the cookie expires when the browser session ends.", false));

        var response = document.Paths["/api/auth/login"].Operations[OperationType.Post].Responses["200"];
        response.Description = "Token mode returns application/json with AuthTokenResponse. Cookie mode returns HTTP 200 with an empty body (no Content-Type) and sets the authentication cookie.";
        response.Headers["Set-Cookie"] = new OpenApiHeader
        {
            Description = "Authentication cookie returned when cookie mode is selected with useCookies=true or useSessionCookies=true.",
            Schema = new OpenApiSchema { Type = "string", Example = new OpenApiString(".AspNetCore.Identity.Application=...") }
        };
        response.Extensions["x-cookie-response"] = new OpenApiObject
        {
            ["statusCode"] = new OpenApiInteger(StatusCodes.Status200OK),
            ["description"] = new OpenApiString("Empty response body; the authentication state is carried by Set-Cookie."),
            ["content"] = new OpenApiObject(),
            ["headers"] = new OpenApiObject
            {
                ["Set-Cookie"] = new OpenApiObject
                {
                    ["description"] = new OpenApiString("Authentication cookie returned when cookie mode is selected with useCookies=true or useSessionCookies=true."),
                    ["schema"] = new OpenApiObject
                    {
                        ["type"] = new OpenApiString("string")
                    }
                }
            }
        };
    }

    private static void AddRefresh(OpenApiDocument document) =>
        AddOperation(document, "/api/auth/refresh", OperationType.Post, "Refresh access",
            "Exchanges an opaque refresh token for a new opaque bearer token.",
            "RefreshRequest", "200", "AuthTokenResponse", false);

    private static void AddEmailEndpoints(OpenApiDocument document)
    {
        AddOperation(document, "/api/auth/confirmEmail", OperationType.Get, "Confirm email",
            "Confirms a member email address using the Identity confirmation code.", null, "200", null, false,
            Query("userId", "Identity user identifier.", "00000000-0000-0000-0000-000000000020"),
            Query("code", "URL-safe confirmation code.", "confirmation-code-example"),
            Query("changedEmail", "Optional changed email address.", "member@example.com"));
        AddOperation(document, "/api/auth/resendConfirmationEmail", OperationType.Post, "Resend confirmation email",
            "Sends a new confirmation email.", "EmailRequest", "200", null, false);
    }

    private static void AddPasswordEndpoints(OpenApiDocument document)
    {
        AddOperation(document, "/api/auth/forgotPassword", OperationType.Post, "Request password reset",
            "Requests a password reset email.", "EmailRequest", "200", null, false);
        AddOperation(document, "/api/auth/resetPassword", OperationType.Post, "Reset password",
            "Sets a new password using a one-time reset code.", "ResetPasswordRequest", "200", null, false);
    }

    private static void AddManageEndpoints(OpenApiDocument document)
    {
        AddOperation(document, "/api/auth/manage/info", OperationType.Get, "Get account information",
            "Returns information for the authenticated member.", null, "200", "ManageInfoResponse", true);
        AddOperation(document, "/api/auth/manage/info", OperationType.Post, "Update account information",
            "Updates email and/or password for the authenticated member.", "UpdateInfoRequest", "200", "ManageInfoResponse", true);
        AddOperation(document, "/api/auth/manage/2fa", OperationType.Post, "Configure two-factor authentication",
            "Configures two-factor authentication for the authenticated member.", "TwoFactorRequest", "200", "TwoFactorInfo", true);
    }

    private static void AddOperation(OpenApiDocument document, string path, OperationType method,
        string summary, string description, string? requestSchema, string successStatus, string? successSchema,
        bool requiresBearer, params OpenApiParameter[] parameters)
    {
        var operation = new OpenApiOperation
        {
            Summary = summary,
            Description = description,
            Tags = new List<OpenApiTag> { new() { Name = "Authentication" } },
            Responses = new OpenApiResponses()
        };
        foreach (var parameter in parameters)
            operation.Parameters.Add(parameter);

        if (requestSchema is not null)
        {
            operation.RequestBody = new OpenApiRequestBody
            {
                Required = true,
                Content = new Dictionary<string, OpenApiMediaType>
                {
                    ["application/json"] = new()
                    {
                        Schema = Ref(requestSchema),
                        Example = RequestExample(requestSchema)
                    }
                }
            };
        }

        var success = new OpenApiResponse { Description = "Success." };
        if (successSchema is not null)
            success.Content["application/json"] =
                new OpenApiMediaType { Schema = Ref(successSchema), Example = SuccessExample(successSchema) };
        else if (path == "/api/auth/confirmEmail")
            success.Content["text/plain"] = new OpenApiMediaType
            {
                Schema = new OpenApiSchema { Type = "string", Example = new OpenApiString("Thank you for confirming your email.") }
            };
        operation.Responses[successStatus] = success;
        if (!(requiresBearer && method == OperationType.Get))
            AddValidationProblem(operation, "400", "The request is invalid.");
        if (requiresBearer || path is "/api/auth/login" or "/api/auth/refresh" or "/api/auth/confirmEmail")
            AddProblem(operation, "401", "Authentication is required.");
        if (path is "/api/auth/manage/info" or "/api/auth/manage/2fa")
            AddProblem(operation, "404", "The authenticated member was not found.");
        if (requiresBearer)
            operation.Security = new List<OpenApiSecurityRequirement> { BearerRequirement() };

        if (!document.Paths.TryGetValue(path, out var item))
            item = new OpenApiPathItem();
        item.Operations[method] = operation;
        document.Paths[path] = item;
    }

    private static void AddProblem(OpenApiOperation operation, string status, string description) =>
        operation.Responses[status] = new OpenApiResponse
        {
            Description = description,
            Content = new Dictionary<string, OpenApiMediaType>
            {
                ["application/problem+json"] = new()
                {
                    Schema = Ref(nameof(ProblemDetails)),
                    Example = new OpenApiObject
                    {
                        ["title"] = new OpenApiString(description),
                        ["status"] = new OpenApiInteger(int.Parse(status, CultureInfo.InvariantCulture)),
                        ["correlationId"] = new OpenApiString("00000000-0000-0000-0000-000000000099")
                    }
                }
            }
        };

    private static void AddValidationProblem(OpenApiOperation operation, string status, string description) =>
        operation.Responses[status] = new OpenApiResponse
        {
            Description = description,
            Content = new Dictionary<string, OpenApiMediaType>
            {
                ["application/problem+json"] = new()
                {
                    Schema = Ref(nameof(ValidationProblemDetails)),
                    Example = new OpenApiObject
                    {
                        ["title"] = new OpenApiString(description),
                        ["status"] = new OpenApiInteger(400),
                        ["errors"] = new OpenApiObject
                        {
                            ["email"] = new OpenApiArray { new OpenApiString("The email field is required.") }
                        },
                        ["correlationId"] = new OpenApiString("00000000-0000-0000-0000-000000000099")
                    }
                }
            }
        };

    private static OpenApiParameter Query(string name, string description, string example) => new()
    {
        Name = name,
        In = ParameterLocation.Query,
        Required = name is "userId" or "code",
        Description = description,
        Schema = new OpenApiSchema { Type = "string", Example = new OpenApiString(example) }
    };

    private static OpenApiParameter QueryBoolean(string name, string description, bool example) => new()
    {
        Name = name,
        In = ParameterLocation.Query,
        Required = false,
        Description = description,
        Schema = new OpenApiSchema { Type = "boolean", Example = new OpenApiBoolean(example) }
    };

    private static OpenApiSecurityRequirement BearerRequirement() => new()
    {
        [new OpenApiSecurityScheme
        {
            Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
        }] = Array.Empty<string>()
    };

    private static void AddSchema(OpenApiDocument document, string name, string description,
        params (string Name, OpenApiSchema Schema)[] properties)
    {
        document.Components.Schemas[name] = new OpenApiSchema
        {
            Type = "object",
            Description = description,
            Properties = properties.ToDictionary(x => x.Name, x => x.Schema),
            Required = properties.Select(x => x.Name).ToHashSet()
        };
    }

    private static OpenApiSchema StringSchema(string description, string example) => new()
    {
        Type = "string",
        Description = description,
        Example = new OpenApiString(example)
    };

    private static OpenApiSchema Ref(string name) => new()
    {
        Reference = new OpenApiReference { Type = ReferenceType.Schema, Id = name }
    };

    private static OpenApiObject? RequestExample(string schema) => schema switch
    {
        "RegisterRequest" or "LoginRequest" => new OpenApiObject
        {
            ["email"] = new OpenApiString("member@example.com"),
            ["password"] = new OpenApiString("P@ssword1234!")
        },
        "RefreshRequest" => new OpenApiObject { ["refreshToken"] = new OpenApiString("opaque-refresh-token-example") },
        "EmailRequest" => new OpenApiObject { ["email"] = new OpenApiString("member@example.com") },
        "ResetPasswordRequest" => new OpenApiObject
        {
            ["email"] = new OpenApiString("member@example.com"),
            ["resetCode"] = new OpenApiString("reset-code-example"),
            ["newPassword"] = new OpenApiString("N3wP@ssword1234!")
        },
        "UpdateInfoRequest" => new OpenApiObject
        {
            ["newEmail"] = new OpenApiString("new-member@example.com"),
            ["newPassword"] = new OpenApiString("N3wP@ssword1234!"),
            ["oldPassword"] = new OpenApiString("P@ssword1234!")
        },
        "TwoFactorRequest" => new OpenApiObject
        {
            ["enable"] = new OpenApiBoolean(true),
            ["resetSharedKey"] = new OpenApiBoolean(false),
            ["resetRecoveryCodes"] = new OpenApiBoolean(false),
            ["forgetMachine"] = new OpenApiBoolean(false),
            ["twoFactorCode"] = new OpenApiString("123456")
        },
        _ => null
    };

    private static IOpenApiAny? SuccessExample(string schema) => schema switch
    {
        "AuthTokenResponse" => new OpenApiObject
            {
                ["tokenType"] = new OpenApiString("Bearer"),
                ["accessToken"] = new OpenApiString("opaque-access-token-example"),
                ["expiresIn"] = new OpenApiInteger(3600),
                ["refreshToken"] = new OpenApiString("opaque-refresh-token-example")
            },
        "ManageInfoResponse" => new OpenApiObject
        {
            ["email"] = new OpenApiString("member@example.com"),
            ["isEmailConfirmed"] = new OpenApiBoolean(true)
        },
        "TwoFactorInfo" => new OpenApiObject
        {
            ["sharedKey"] = new OpenApiString("JBSWY3DPEHPK3PXP"),
            ["recoveryCodes"] = new OpenApiArray { new OpenApiString("11111111") },
            ["recoveryCodesLeft"] = new OpenApiInteger(10),
            ["isTwoFactorEnabled"] = new OpenApiBoolean(false),
            ["isMachineRemembered"] = new OpenApiBoolean(false)
        },
        _ => null
    };
}
