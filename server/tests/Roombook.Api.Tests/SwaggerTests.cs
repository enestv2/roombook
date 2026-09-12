using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Roombook.Api.Tests;

public sealed class SwaggerTests
{
    private static readonly IReadOnlyDictionary<string, IReadOnlySet<string>> ExpectedRoutes =
        new Dictionary<string, IReadOnlySet<string>>
        {
            ["/api/rooms"] = new HashSet<string> { "get" },
            ["/api/bookings"] = new HashSet<string> { "post" },
            ["/api/bookings/{id}"] = new HashSet<string> { "get" },
            ["/api/auth/register"] = new HashSet<string> { "post" },
            ["/api/auth/login"] = new HashSet<string> { "post" },
            ["/api/auth/refresh"] = new HashSet<string> { "post" },
            ["/api/auth/confirmEmail"] = new HashSet<string> { "get" },
            ["/api/auth/resendConfirmationEmail"] = new HashSet<string> { "post" },
            ["/api/auth/forgotPassword"] = new HashSet<string> { "post" },
            ["/api/auth/resetPassword"] = new HashSet<string> { "post" },
            ["/api/auth/manage/info"] = new HashSet<string> { "get", "post" },
            ["/api/auth/manage/2fa"] = new HashSet<string> { "post" }
        };

    [Fact]
    public async Task DevelopmentDocumentListsCompletePublicRouteAndMethodSet()
    {
        using var document = await GetDocument();
        var actual = document.RootElement.GetProperty("paths").EnumerateObject()
            .ToDictionary(path => path.Name, path => path.Value.EnumerateObject()
                .Where(operation => IsHttpMethod(operation.Name))
                .Select(operation => operation.Name)
                .ToHashSet());

        Assert.Equal(ExpectedRoutes.Keys.OrderBy(x => x), actual.Keys.OrderBy(x => x));
        foreach (var (route, methods) in ExpectedRoutes)
        {
            Assert.Equal(methods.OrderBy(x => x), actual[route].OrderBy(x => x));
            foreach (var method in methods)
            {
                var operation = document.RootElement.GetProperty("paths").GetProperty(route).GetProperty(method);
                Assert.False(string.IsNullOrWhiteSpace(operation.GetProperty("summary").GetString()));
                Assert.False(string.IsNullOrWhiteSpace(operation.GetProperty("description").GetString()));
            }
        }
    }

    [Fact]
    public async Task DevelopmentSwaggerUiLoadsAndReferencesDocument()
    {
        using var factory = CreateFactory("Development");
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/swagger/index.html");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("/swagger/v1/swagger.json", body);
    }

    [Fact]
    public async Task DocumentContainsAccurateSchemasRequiredFieldsExamplesAndContentTypes()
    {
        using var document = await GetDocument();
        var root = document.RootElement;
        var schemas = root.GetProperty("components").GetProperty("schemas");

        foreach (var schema in new[] { "RoomResponse", "WorkingPeriodResponse", "CreateBookingRequest",
                      "BookingResponse", "AlternativeResponse", "ConflictResponse",
                      "ValidationProblemDetails", "ProblemDetails", "AuthTokenResponse",
                      "RegisterRequest", "LoginRequest", "RefreshRequest" })
            Assert.True(schemas.TryGetProperty(schema, out _), $"Missing schema {schema}.");

        AssertRequired(schemas, "RoomResponse", "id", "name", "timeZone", "workingPeriods");
        AssertRequired(schemas, "WorkingPeriodResponse", "start", "end");
        AssertRequired(schemas, "CreateBookingRequest", "roomId", "startsAt", "endsAt");
        AssertRequired(schemas, "BookingResponse", "id", "roomId", "memberId", "startsAtUtc",
            "endsAtUtc", "startsAtLocal", "endsAtLocal", "timeZone");
        AssertRequired(schemas, "AlternativeResponse", "startsAtUtc", "endsAtUtc");
        AssertRequired(schemas, "ConflictResponse", "roomId", "requestedStartUtc", "requestedEndUtc",
            "conflictingStartUtc", "conflictingEndUtc", "alternatives", "timeZone", "message");
        AssertRequired(schemas, "ValidationProblemDetails", "errors");
        Assert.Contains("correlationId", schemas.GetProperty("ProblemDetails").GetProperty("properties")
            .EnumerateObject().Select(property => property.Name));
        Assert.Contains("correlationId", schemas.GetProperty("ValidationProblemDetails").GetProperty("properties")
            .EnumerateObject().Select(property => property.Name));
        Assert.Equal("00000000-0000-0000-0000-000000000099",
            schemas.GetProperty("ProblemDetails").GetProperty("example").GetProperty("correlationId").GetString());
        Assert.Equal("00000000-0000-0000-0000-000000000099",
            schemas.GetProperty("ValidationProblemDetails").GetProperty("example")
                .GetProperty("correlationId").GetString());

        var request = schemas.GetProperty("CreateBookingRequest");
        Assert.Contains("15-minute", request.GetProperty("description").GetString());
        Assert.Contains("explicit offset", request.GetProperty("properties").GetProperty("startsAt")
            .GetProperty("description").GetString());
        Assert.Contains("4 hours", request.GetProperty("properties").GetProperty("endsAt")
            .GetProperty("description").GetString());
        Assert.Equal("date-time", request.GetProperty("properties").GetProperty("startsAt").GetProperty("format").GetString());
        Assert.EndsWith("Z", request.GetProperty("properties").GetProperty("startsAt").GetProperty("example").GetString());

        var bookingPost = root.GetProperty("paths").GetProperty("/api/bookings").GetProperty("post");
        Assert.Equal("application/json", bookingPost.GetProperty("requestBody").GetProperty("content")
            .EnumerateObject().Single().Name);
        AssertResponse(bookingPost, "201", "application/json", "BookingResponse", true);
        AssertResponse(bookingPost, "409", "application/json", "ConflictResponse", true);
        AssertResponse(bookingPost, "400", "application/problem+json", "ValidationProblemDetails", true);
        AssertResponse(bookingPost, "401", "application/problem+json", "ProblemDetails", true);
        AssertResponse(bookingPost, "403", "application/problem+json", "ProblemDetails", true);
        AssertResponse(bookingPost, "500", "application/problem+json", "ProblemDetails", true);
        Assert.Equal("00000000-0000-0000-0000-000000000099",
            bookingPost.GetProperty("responses").GetProperty("400").GetProperty("content")
                .GetProperty("application/problem+json").GetProperty("example").GetProperty("correlationId").GetString());
        Assert.Equal("00000000-0000-0000-0000-000000000099",
            bookingPost.GetProperty("responses").GetProperty("401").GetProperty("content")
                .GetProperty("application/problem+json").GetProperty("example").GetProperty("correlationId").GetString());

        var conflictExample = bookingPost.GetProperty("responses").GetProperty("409").GetProperty("content")
            .GetProperty("application/json").GetProperty("example");
        Assert.True(conflictExample.TryGetProperty("alternatives", out _));
        Assert.False(conflictExample.TryGetProperty("memberId", out _));
        Assert.True(bookingPost.GetProperty("requestBody").GetProperty("content").GetProperty("application/json")
            .TryGetProperty("example", out _));

        var rooms = root.GetProperty("paths").GetProperty("/api/rooms").GetProperty("get");
        AssertResponse(rooms, "200", "application/json", "RoomResponse", true, isArray: true);
        AssertResponse(rooms, "500", "application/problem+json", "ProblemDetails", true);

        var getBooking = root.GetProperty("paths").GetProperty("/api/bookings/{id}").GetProperty("get");
        Assert.Equal(new[] { "application/problem+json" }, ResponseContentTypes(getBooking, "404"));
        Assert.Equal("ProblemDetails", SchemaId(getBooking, "404", "application/problem+json"));
        Assert.True(getBooking.GetProperty("responses").GetProperty("404").GetProperty("content")
            .GetProperty("application/problem+json").TryGetProperty("example", out _));
    }

    [Fact]
    public async Task IdentityValidationResponsesUseValidationProblemDetailsWithErrors()
    {
        using var document = await GetDocument();
        var paths = document.RootElement.GetProperty("paths");

        foreach (var route in new[] { "/api/auth/register", "/api/auth/login", "/api/auth/refresh",
                     "/api/auth/confirmEmail", "/api/auth/resendConfirmationEmail",
                     "/api/auth/forgotPassword", "/api/auth/resetPassword", "/api/auth/manage/info",
                     "/api/auth/manage/2fa" })
        {
            foreach (var operation in paths.GetProperty(route).EnumerateObject()
                         .Where(property => IsHttpMethod(property.Name))
                         .Select(property => property.Value))
            {
                if (!operation.GetProperty("responses").TryGetProperty("400", out var response))
                    continue;

                var media = response.GetProperty("content").GetProperty("application/problem+json");
                Assert.Equal("ValidationProblemDetails", SchemaId(media, "schema"));
                Assert.Equal(JsonValueKind.Object, media.GetProperty("example")
                    .GetProperty("errors").ValueKind);
            }
        }
    }

    [Fact]
    public async Task OptionalIdentityFieldsAreNotRequired()
    {
        using var document = await GetDocument();
        var schemas = document.RootElement.GetProperty("components").GetProperty("schemas");

        AssertOptional(schemas, "UpdateInfoRequest");
        AssertOptional(schemas, "TwoFactorInfo");
    }

    [Fact]
    public async Task ProtectedOperationsUseOnlyTheBearerSecurityReference()
    {
        using var document = await GetDocument();
        var paths = document.RootElement.GetProperty("paths");

        foreach (var route in new[] { "/api/bookings", "/api/bookings/{id}" })
            foreach (var method in ExpectedRoutes[route])
                AssertBearerSecurity(paths.GetProperty(route).GetProperty(method));

        foreach (var route in new[] { "/api/auth/manage/info", "/api/auth/manage/2fa" })
            foreach (var method in ExpectedRoutes[route])
                AssertBearerSecurity(paths.GetProperty(route).GetProperty(method));

        foreach (var route in new[] { "/api/rooms", "/api/auth/register", "/api/auth/login",
                     "/api/auth/refresh", "/api/auth/confirmEmail", "/api/auth/resendConfirmationEmail",
                     "/api/auth/forgotPassword", "/api/auth/resetPassword" })
            foreach (var method in ExpectedRoutes[route])
                Assert.False(paths.GetProperty(route).GetProperty(method).TryGetProperty("security", out _));

        var bearer = document.RootElement.GetProperty("components").GetProperty("securitySchemes").GetProperty("Bearer");
        Assert.Equal("http", bearer.GetProperty("type").GetString());
        Assert.Equal("bearer", bearer.GetProperty("scheme").GetString());
        Assert.False(bearer.TryGetProperty("bearerFormat", out _));
        Assert.Contains("not JWT", bearer.GetProperty("description").GetString());
    }

    [Fact]
    public async Task AuthenticationContractDocumentsOpaqueTokensAndExamples()
    {
        using var document = await GetDocument();
        var root = document.RootElement;
        var token = root.GetProperty("paths").GetProperty("/api/auth/login").GetProperty("post")
            .GetProperty("responses").GetProperty("200").GetProperty("content").GetProperty("application/json");
        Assert.Equal("AuthTokenResponse", SchemaId(token, "schema"));
        var example = token.GetProperty("example");
        Assert.Equal("Bearer", example.GetProperty("tokenType").GetString());
        Assert.Contains("opaque", example.GetProperty("accessToken").GetString());
        Assert.Contains("Opaque", root.GetProperty("components").GetProperty("schemas")
            .GetProperty("AuthTokenResponse").GetProperty("description").GetString());

        var manage = root.GetProperty("paths").GetProperty("/api/auth/manage/info").GetProperty("get");
        AssertResponse(manage, "200", "application/json", "ManageInfoResponse", false);
        AssertResponse(manage, "401", "application/problem+json", "ProblemDetails", false);
        AssertResponse(manage, "404", "application/problem+json", "ProblemDetails", true);

        var confirm = root.GetProperty("paths").GetProperty("/api/auth/confirmEmail").GetProperty("get");
        Assert.Equal(new[] { "text/plain" }, ResponseContentTypes(confirm, "200"));
        Assert.Equal(new[] { "application/problem+json" }, ResponseContentTypes(confirm, "401"));

        var twoFactor = root.GetProperty("paths").GetProperty("/api/auth/manage/2fa").GetProperty("post");
        Assert.Equal("TwoFactorRequest", SchemaId(twoFactor.GetProperty("requestBody")
            .GetProperty("content").GetProperty("application/json"), "schema"));
        AssertResponse(twoFactor, "200", "application/json", "TwoFactorInfo", true);

        var login = root.GetProperty("paths").GetProperty("/api/auth/login").GetProperty("post");
        Assert.Contains("either useCookies=true or useSessionCookies=true",
            login.GetProperty("description").GetString(), StringComparison.Ordinal);
        var query = login.GetProperty("parameters").EnumerateArray()
            .ToDictionary(parameter => parameter.GetProperty("name").GetString()!);
        foreach (var name in new[] { "useCookies", "useSessionCookies" })
        {
            Assert.Equal("query", query[name].GetProperty("in").GetString());
            Assert.True(!query[name].TryGetProperty("required", out var required) || !required.GetBoolean());
            Assert.Equal("boolean", query[name].GetProperty("schema").GetProperty("type").GetString());
        }
        Assert.Contains("useSessionCookies=true", query["useCookies"].GetProperty("description").GetString(),
            StringComparison.Ordinal);
        Assert.Contains("even if useCookies is false",
            query["useSessionCookies"].GetProperty("description").GetString(), StringComparison.Ordinal);

        var loginResponse = login.GetProperty("responses").GetProperty("200");
        Assert.Contains("empty body", loginResponse.GetProperty("description").GetString(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("token", loginResponse.GetProperty("description").GetString(), StringComparison.OrdinalIgnoreCase);
        Assert.True(loginResponse.GetProperty("headers").TryGetProperty("Set-Cookie", out _));
        var cookieMode = loginResponse.GetProperty("x-cookie-response");
        Assert.Equal(200, cookieMode.GetProperty("statusCode").GetInt32());
        Assert.Empty(cookieMode.GetProperty("content").EnumerateObject());
        Assert.True(cookieMode.GetProperty("headers").TryGetProperty("Set-Cookie", out _));
        Assert.Equal("ValidationProblemDetails", SchemaId(login, "400", "application/problem+json"));
    }

    [Fact]
    public async Task NonDevelopmentDocumentationRoutesAreUnavailable()
    {
        using var factory = CreateFactory("Test");
        using var client = factory.CreateClient();

        var json = await client.GetAsync("/swagger/v1/swagger.json");
        var ui = await client.GetAsync("/swagger/index.html");

        Assert.Equal(HttpStatusCode.NotFound, json.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, ui.StatusCode);
    }

    [Fact]
    public async Task DevelopmentDocumentOmitsAuthenticationRoutesWhenIdentityIsNotMapped()
    {
        using var document = await GetDocument(identityEnabled: false);
        var paths = document.RootElement.GetProperty("paths");

        Assert.DoesNotContain(paths.EnumerateObject(),
            path => path.Name.StartsWith("/api/auth/", StringComparison.Ordinal));
        Assert.False(document.RootElement.GetProperty("components").GetProperty("schemas")
            .TryGetProperty("AuthTokenResponse", out _));
    }

    private static async Task<JsonDocument> GetDocument(bool identityEnabled = true)
    {
        using var factory = CreateFactory("Development", identityEnabled);
        using var client = factory.CreateClient();
        var response = await client.GetAsync("/swagger/v1/swagger.json");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    }

    private static void AssertRequired(JsonElement schemas, string name, params string[] required)
    {
        var actual = schemas.GetProperty(name).GetProperty("required").EnumerateArray()
            .Select(value => value.GetString()).ToHashSet();
        Assert.Equal(required.OrderBy(x => x), actual.OrderBy(x => x));
    }

    private static void AssertOptional(JsonElement schemas, string name)
    {
        var schema = schemas.GetProperty(name);
        if (!schema.TryGetProperty("required", out var required))
            return;

        Assert.Empty(required.EnumerateArray());
    }

    private static void AssertResponse(JsonElement operation, string status, string contentType,
        string schema, bool requiresExample, bool isArray = false)
    {
        var response = operation.GetProperty("responses").GetProperty(status);
        Assert.Equal(new[] { contentType }, ResponseContentTypes(operation, status));
        var media = response.GetProperty("content").GetProperty(contentType);
        if (isArray)
            Assert.EndsWith(schema, media.GetProperty("schema").GetProperty("items").GetProperty("$ref").GetString());
        else
            Assert.Equal(schema, SchemaId(media, "schema"));
        if (requiresExample)
            Assert.True(media.TryGetProperty("example", out _), $"{status} should include an example.");
    }

    private static string[] ResponseContentTypes(JsonElement operation, string status) =>
        operation.GetProperty("responses").GetProperty(status).GetProperty("content")
            .EnumerateObject().Select(property => property.Name).ToArray();

    private static string SchemaId(JsonElement value, string property)
    {
        var reference = value.GetProperty(property).GetProperty("$ref").GetString();
        return reference![(reference!.LastIndexOf('/') + 1)..];
    }

    private static string SchemaId(JsonElement media, string property, string contentType) =>
        SchemaId(media.GetProperty("responses").GetProperty(property).GetProperty("content")
            .GetProperty(contentType), "schema");

    private static void AssertBearerSecurity(JsonElement operation)
    {
        var security = operation.GetProperty("security");
        Assert.Equal(1, security.GetArrayLength());
        var requirement = security[0];
        Assert.Equal(new[] { "Bearer" }, requirement.EnumerateObject().Select(property => property.Name));
        Assert.Equal(0, requirement.GetProperty("Bearer").GetArrayLength());
    }

    private static bool IsHttpMethod(string name) =>
        name is "get" or "post" or "put" or "patch" or "delete" or "head" or "options" or "trace";

    private static WebApplicationFactory<Program> CreateFactory(string environment, bool identityEnabled = true) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting(WebHostDefaults.EnvironmentKey, environment);
            if (identityEnabled)
            {
                builder.UseSetting("ConnectionStrings:Roombook",
                    "Host=localhost;Database=roombook-tests;Username=test;Password=test");
                builder.UseSetting("Roombook:SkipPostgresInitialization", "true");
            }
        });
}
