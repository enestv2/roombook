using System.Reflection;

namespace Roombook.Reservations.Tests;

public sealed class SchemaProvisioningTests
{
    [Fact]
    public async Task PostgresSchemaProvisionsIdentityAndReservationSafeguardsTogether()
    {
        await using var stream = Assembly
            .GetAssembly(typeof(Roombook.Infrastructure.PostgresSchemaInitializer))!
            .GetManifestResourceStream("Roombook.Infrastructure.PostgresSchema.sql");
        Assert.NotNull(stream);

        using var reader = new StreamReader(stream!);
        var schema = await reader.ReadToEndAsync();

        Assert.Contains("CREATE TABLE IF NOT EXISTS members", schema);
        Assert.Contains("CREATE TABLE IF NOT EXISTS roles", schema);
        Assert.Contains("CREATE TABLE IF NOT EXISTS member_claims", schema);
        Assert.Contains("CREATE TABLE IF NOT EXISTS member_logins", schema);
        Assert.Contains("CREATE TABLE IF NOT EXISTS member_roles", schema);
        Assert.Contains("CREATE TABLE IF NOT EXISTS role_claims", schema);
        Assert.Contains("CREATE TABLE IF NOT EXISTS member_tokens", schema);
        Assert.Contains("reservations_no_active_overlap", schema);
        Assert.Contains("EXCLUDE USING gist", schema);
    }
}
