using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Roombook.Api.Tests;

public sealed class LocalizationApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> factory;

    public LocalizationApiTests(WebApplicationFactory<Program> factory) =>
        this.factory = factory.WithWebHostBuilder(builder => builder.UseSetting(WebHostDefaults.EnvironmentKey, "Test"));

    [Fact]
    public async Task ValidationUsesTurkishMessagesAndStableCodes()
    {
        using var client = factory.CreateClient();
        using var request = BookingRequest("2030-01-11T09:01:00Z", "2030-01-11T10:00:00Z");
        request.Headers.Add("Accept-Language", "tr");
        request.Headers.Add("X-Development-Member-Id", Guid.NewGuid().ToString());

        var response = await client.SendAsync(request);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("15 dakikalık", document.RootElement.GetProperty("errors").GetProperty("startsAt")[0].GetString());
        Assert.Equal("validation.failed", document.RootElement.GetProperty("code").GetString());
        Assert.Equal("booking.start_not_aligned", document.RootElement.GetProperty("errorCodes")
            .GetProperty("startsAt")[0].GetString());
    }

    [Fact]
    public async Task UnsupportedLanguageFallsBackToEnglish()
    {
        using var client = factory.CreateClient();
        using var request = BookingRequest("2030-01-11T09:01:00Z", "2030-01-11T10:00:00Z");
        request.Headers.Add("Accept-Language", "de-DE");
        request.Headers.Add("X-Development-Member-Id", Guid.NewGuid().ToString());

        var response = await client.SendAsync(request);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Contains("15-minute", document.RootElement.GetProperty("errors").GetProperty("startsAt")[0].GetString());
    }

    [Fact]
    public async Task UnauthorizedResponseUsesRequestedLanguageAndStableCode()
    {
        using var client = factory.CreateClient();
        using var request = BookingRequest("2030-01-11T09:00:00Z", "2030-01-11T10:00:00Z");
        request.Headers.Add("Accept-Language", "tr");

        var response = await client.SendAsync(request);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("authorization.required", document.RootElement.GetProperty("code").GetString());
        Assert.Contains("Kimlik doğrulaması", document.RootElement.GetProperty("title").GetString());
    }

    [Fact]
    public async Task ConflictUsesRequestedLanguageWithoutMemberIdentity()
    {
        using var client = factory.CreateClient();
        using var first = BookingRequest("2030-01-13T09:00:00Z", "2030-01-13T10:00:00Z");
        first.Headers.Add("Accept-Language", "en");
        first.Headers.Add("X-Development-Member-Id", Guid.NewGuid().ToString());
        Assert.Equal(HttpStatusCode.Created, (await client.SendAsync(first)).StatusCode);

        using var second = BookingRequest("2030-01-13T09:00:00Z", "2030-01-13T10:00:00Z");
        second.Headers.Add("Accept-Language", "tr");
        second.Headers.Add("X-Development-Member-Id", Guid.NewGuid().ToString());
        var response = await client.SendAsync(second);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.True(body.Contains("Uygun alternatif", StringComparison.OrdinalIgnoreCase), body);
        Assert.Contains("\"code\":\"booking.no_alternatives\"", body);
        Assert.DoesNotContain("memberId", body, StringComparison.OrdinalIgnoreCase);
    }

    private static HttpRequestMessage BookingRequest(string startsAt, string endsAt) => new(HttpMethod.Post, "/api/bookings")
    {
        Content = JsonContent.Create(new
        {
            roomId = Guid.Parse("00000000-0000-0000-0000-000000000001"),
            startsAt,
            endsAt
        })
    };
}
