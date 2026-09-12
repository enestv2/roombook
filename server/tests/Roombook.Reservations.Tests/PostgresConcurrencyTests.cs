using Npgsql;
using Roombook.Infrastructure;
using Roombook.Reservations;
using Roombook.Rooms;

namespace Roombook.Reservations.Tests;

public sealed class PostgresConcurrencyTests
{
    [PostgresFact]
    public async Task ConcurrentRequestsAllowAtMostOneOverlappingCommitInPostgres()
    {
        var connectionString = Environment.GetEnvironmentVariable("ROOMBOOK_TEST_CONNECTION_STRING")
            ?? Environment.GetEnvironmentVariable("ConnectionStrings__Roombook");
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        await using var dataSource = NpgsqlDataSource.Create(connectionString);
        await PostgresSchemaInitializer.InitializeAsync(dataSource);

        var roomId = new RoomId(Guid.NewGuid());
        var room = new Room(roomId, "PostgreSQL concurrency room", TimeZoneInfo.Utc,
            new[] { new WorkingPeriod(new TimeOnly(8, 0), new TimeOnly(18, 0)) });
        var service = new BookingService(new InMemoryRoomAvailability(new[] { room }),
            new PostgresBookingRepository(dataSource), new FixedTimeProvider(Now));
        var command = new BookingCommand(roomId, At(11), At(12));

        try
        {
            var results = await Task.WhenAll(
                service.CreateAsync(Guid.NewGuid(), command),
                service.CreateAsync(Guid.NewGuid(), command));

            Assert.Single(results.OfType<BookingSuccess>());
            Assert.Single(results.OfType<BookingConflictFailure>());
        }
        finally
        {
            await using var cleanup = dataSource.CreateCommand("DELETE FROM reservations WHERE room_id = $1");
            cleanup.Parameters.AddWithValue(roomId.Value);
            await cleanup.ExecuteNonQueryAsync();
        }
    }

    private static readonly DateTimeOffset Now = new(2030, 1, 10, 8, 0, 0, TimeSpan.Zero);

    private static DateTimeOffset At(int hour) => new(2030, 1, 11, hour, 0, 0, TimeSpan.Zero);
}

internal sealed class PostgresFactAttribute : FactAttribute
{
    public PostgresFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ROOMBOOK_TEST_CONNECTION_STRING"))
            && string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ConnectionStrings__Roombook")))
            Skip = "Set ROOMBOOK_TEST_CONNECTION_STRING or ConnectionStrings__Roombook to run PostgreSQL integration tests.";
    }
}
