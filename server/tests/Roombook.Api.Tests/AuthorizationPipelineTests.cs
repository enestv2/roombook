using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Roombook.Api.Tests;

public sealed class AuthorizationPipelineTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient client;

    public AuthorizationPipelineTests(WebApplicationFactory<Program> factory)
    {
        client = factory.WithWebHostBuilder(builder =>
            builder.UseSetting(WebHostDefaults.EnvironmentKey, "Test")).CreateClient();
    }

    [Fact]
    public async Task UnauthenticatedBookingRequestIsRejectedByAuthorizationPipeline()
    {
        var response = await client.PostAsJsonAsync("/api/bookings", new
        {
            roomId = Guid.NewGuid(),
            startsAt = "2030-01-11T09:00:00Z",
            endsAt = "2030-01-11T10:00:00Z"
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.True(response.Headers.TryGetValues("X-Correlation-Id", out var values));
        Assert.True(Guid.TryParse(Assert.Single(values), out _));
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task TimezoneLessBookingTimestampsAreRejectedByApi()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/bookings")
        {
            Content = JsonContent.Create(new
            {
                roomId = Guid.Parse("00000000-0000-0000-0000-000000000001"),
                startsAt = "2030-01-11T09:00:00",
                endsAt = "2030-01-11T10:00:00"
            })
        };
        request.Headers.Add("X-Development-Member-Id", Guid.NewGuid().ToString());

        var response = await client.SendAsync(request);

        var responseBody = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("explicit UTC offset", responseBody);
        using var document = JsonDocument.Parse(responseBody);
        Assert.Equal(Assert.Single(response.Headers.GetValues("X-Correlation-Id")),
            document.RootElement.GetProperty("correlationId").GetString());
    }

    [Fact]
    public async Task InvalidBookingReturnsValidationCorrelationId()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/bookings")
        {
            Content = JsonContent.Create(new
            {
                roomId = Guid.NewGuid(),
                startsAt = "2030-01-11T09:00:00Z",
                endsAt = "2030-01-11T10:00:00Z"
            })
        };
        request.Headers.Add("X-Development-Member-Id", Guid.NewGuid().ToString());

        var response = await client.SendAsync(request);

        var responseBody = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var document = JsonDocument.Parse(responseBody);
        Assert.Equal(Assert.Single(response.Headers.GetValues("X-Correlation-Id")),
            document.RootElement.GetProperty("correlationId").GetString());
    }

    [Fact]
    public async Task ExplicitUtcBookingTimestampsRemainAccepted()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/bookings")
        {
            Content = JsonContent.Create(new
            {
                roomId = Guid.Parse("00000000-0000-0000-0000-000000000001"),
                startsAt = "2030-01-12T09:00:00Z",
                endsAt = "2030-01-12T10:00:00Z"
            })
        };
        request.Headers.Add("X-Development-Member-Id", Guid.NewGuid().ToString());

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }
}
