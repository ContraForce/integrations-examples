using System.Collections.Concurrent;

namespace ContraForce.Samples.SnowInbound.CallbackModels;

/// <summary>
/// Tracks the last processed state per ServiceNow incident (keyed by sys_id)
/// so that repeated Business Rule callbacks — ServiceNow fires one on every
/// update — don't generate duplicate comments on the ContraForce side.
/// </summary>
/// <remarks>
/// Journal entries (<c>sys_journal_field</c>) have no incrementing integer id,
/// so we track the latest <c>sys_created_on</c> we've forwarded. That column is
/// stored UTC in the sortable <c>yyyy-MM-dd HH:mm:ss</c> format, so a plain
/// ordinal string compare is a correct "newer than" test.
///
/// This sample keeps state in memory. For multi-instance deployments, replace
/// with a Redis, Cosmos, or SQL-backed implementation.
/// </remarks>
public sealed class ChangeTracker
{
    private readonly ConcurrentDictionary<string, IncidentState> _state = new(
        StringComparer.Ordinal
    );

    public IncidentState Snapshot(string sysId) =>
        _state.TryGetValue(sysId, out var s)
            ? s
            : new IncidentState(LastJournalCreatedOn: string.Empty, Closed: false);

    public void Record(string sysId, IncidentState state) => _state[sysId] = state;
}

public sealed record IncidentState(string LastJournalCreatedOn, bool Closed);
