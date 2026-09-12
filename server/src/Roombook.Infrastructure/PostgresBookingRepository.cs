using Npgsql;
using Roombook.Reservations;
using Roombook.Rooms;

namespace Roombook.Infrastructure;

/// PostgreSQL implementation. The exclusion constraint in PostgresSchema.sql remains the final
/// authority when two transactions attempt the same room interval concurrently.
public sealed class PostgresBookingRepository(NpgsqlDataSource dataSource) : IBookingRepository
{
    public async Task<IReadOnlyList<Booking>> FindOverlapsAsync(RoomId roomId, DateTimeOffset startUtc, DateTimeOffset endUtc, CancellationToken cancellationToken = default)
    {
        await using var command = dataSource.CreateCommand("""
            SELECT id, room_id, member_id, starts_at_utc, ends_at_utc, is_cancelled
            FROM reservations
            WHERE room_id = $1 AND is_cancelled = false AND starts_at_utc < $3 AND ends_at_utc > $2
            ORDER BY starts_at_utc
            """);
        command.Parameters.AddWithValue(roomId.Value);
        command.Parameters.AddWithValue(startUtc.UtcDateTime);
        command.Parameters.AddWithValue(endUtc.UtcDateTime);
        return await ReadAsync(command, cancellationToken);
    }

    public async Task<IReadOnlyList<Booking>> ListForRoomAsync(RoomId roomId, DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken cancellationToken = default)
    {
        await using var command = dataSource.CreateCommand("""
            SELECT id, room_id, member_id, starts_at_utc, ends_at_utc, is_cancelled
            FROM reservations
            WHERE room_id = $1 AND is_cancelled = false AND ends_at_utc > $2 AND starts_at_utc < $3
            ORDER BY starts_at_utc
            """);
        command.Parameters.AddWithValue(roomId.Value);
        command.Parameters.AddWithValue(fromUtc.UtcDateTime);
        command.Parameters.AddWithValue(toUtc.UtcDateTime);
        return await ReadAsync(command, cancellationToken);
    }

    public async Task<Booking> AddIfNoOverlapAsync(Booking booking, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using var command = new NpgsqlCommand("""
            INSERT INTO reservations (id, room_id, member_id, starts_at_utc, ends_at_utc, is_cancelled)
            VALUES ($1, $2, $3, $4, $5, false)
            """, connection, transaction);
        command.Parameters.AddWithValue(booking.Id.Value);
        command.Parameters.AddWithValue(booking.RoomId.Value);
        command.Parameters.AddWithValue(booking.MemberId);
        command.Parameters.AddWithValue(booking.StartsAtUtc.UtcDateTime);
        command.Parameters.AddWithValue(booking.EndsAtUtc.UtcDateTime);
        try
        {
            await command.ExecuteNonQueryAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return booking;
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.ExclusionViolation)
        {
            await transaction.RollbackAsync(cancellationToken);
            throw new BookingOverlapException();
        }
    }

    private static async Task<IReadOnlyList<Booking>> ReadAsync(NpgsqlCommand command, CancellationToken cancellationToken)
    {
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var result = new List<Booking>();
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(new Booking(new BookingId(reader.GetGuid(0)), new RoomId(reader.GetGuid(1)), reader.GetGuid(2),
                new DateTimeOffset(DateTime.SpecifyKind(reader.GetDateTime(3), DateTimeKind.Utc)),
                new DateTimeOffset(DateTime.SpecifyKind(reader.GetDateTime(4), DateTimeKind.Utc)), reader.GetBoolean(5)));
        }
        return result;
    }
}
