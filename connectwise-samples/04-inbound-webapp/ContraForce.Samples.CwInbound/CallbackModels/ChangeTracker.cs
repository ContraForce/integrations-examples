using System.Collections.Concurrent;

namespace ContraForce.Samples.CwInbound.CallbackModels;

/// <summary>
/// Tracks the last processed ticket state per CW ticket id so that repeated
/// callbacks (CW fires one on every field update) don't generate duplicate
/// comments on the ContraForce side.
/// </summary>
/// <remarks>
/// This sample keeps state in memory. For multi-instance deployments, replace
/// with a Redis, Cosmos, or SQL-backed implementation.
/// </remarks>
public sealed class ChangeTracker
{
    private readonly ConcurrentDictionary<int, TicketState> _state = new();

    public TicketState Snapshot(int ticketId) =>
        _state.TryGetValue(ticketId, out var s) ? s : new TicketState(LastNoteId: 0, Closed: false);

    public void Record(int ticketId, TicketState state) => _state[ticketId] = state;
}

public sealed record TicketState(int LastNoteId, bool Closed);
