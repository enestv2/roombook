using System.Reflection;
using Npgsql;

namespace Roombook.Infrastructure;

public static class PostgresSchemaInitializer
{
    public static async Task InitializeAsync(NpgsqlDataSource dataSource, CancellationToken cancellationToken = default)
    {
        await using var command = dataSource.CreateCommand(await ReadSchemaAsync());
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<string> ReadSchemaAsync()
    {
        await using var stream = Assembly.GetExecutingAssembly()
            .GetManifestResourceStream("Roombook.Infrastructure.PostgresSchema.sql")
            ?? throw new InvalidOperationException("The PostgreSQL schema resource is missing.");
        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync();
    }
}
