using System.Collections.Concurrent;

namespace ContraForce.Samples.HaloInbound.Webhook;

/// <summary>
/// Tracks the last processed ticket state per Halo ticket id so that repeated
/// webhook deliveries don't generate duplicate comments on the ContraForce
/// side. Halo fires a webhook on every meaningful change, similar to CW.
/// </summary>
/// <remarks>
/// In-memory only. For multi-instance deployments, replace with Redis,
/// Cosmos, or a SQL-backed implementation.
/// </remarks>
public sealed class ChangeTracker
{
    private readonly ConcurrentDictionary<int, TicketState> _state = new();

    public TicketState Snapshot(int ticketId) =>
        _state.TryGetValue(ticketId, out var s)
            ? s
            : new TicketState(LastActionId: 0, Closed: false);

    public void Record(int ticketId, TicketState state) => _state[ticketId] = state;
}

public sealed record TicketState(int LastActionId, bool Closed);
