using Roombook.Reservations;
using Roombook.Rooms;

namespace Roombook.Infrastructure;

/// In-memory substitute used by tests and local development. Production uses the PostgreSQL schema.
public sealed class InMemoryBookingRepository : IBookingRepository
{
    private readonly object gate = new();
    private readonly List<Booking> items = new();

    public Task<IReadOnlyList<Booking>> FindOverlapsAsync(RoomId roomId, DateTimeOffset startUtc, DateTimeOffset endUtc, CancellationToken cancellationToken = default)
    {
        lock (gate) return Task.FromResult<IReadOnlyList<Booking>>(items.Where(x => x.RoomId == roomId && x.Overlaps(startUtc, endUtc)).ToArray());
    }

    public Task<IReadOnlyList<Booking>> ListForRoomAsync(RoomId roomId, DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken cancellationToken = default)
    {
        lock (gate) return Task.FromResult<IReadOnlyList<Booking>>(items.Where(x => x.RoomId == roomId && !x.IsCancelled && x.EndsAtUtc > fromUtc && x.StartsAtUtc < toUtc).ToArray());
    }

    public Task<Booking> AddIfNoOverlapAsync(Booking booking, CancellationToken cancellationToken = default)
    {
        lock (gate)
        {
            if (items.Any(x => x.RoomId == booking.RoomId && x.Overlaps(booking.StartsAtUtc, booking.EndsAtUtc))) throw new BookingOverlapException();
            items.Add(booking);
            return Task.FromResult(booking);
        }
    }
}
