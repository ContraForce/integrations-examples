"""
Tracks the last processed ticket state per CW ticket id so that repeated
callbacks (CW fires one on every field update) don't generate duplicate
comments on the ContraForce side.

This sample keeps state in memory. For multi-instance deployments, replace
with a Redis / Cosmos / SQL-backed implementation.
"""

from __future__ import annotations

from dataclasses import dataclass
from threading import Lock


@dataclass(frozen=True)
class TicketState:
    last_note_id: int
    closed: bool


class ChangeTracker:
    def __init__(self) -> None:
        self._state: dict[int, TicketState] = {}
        self._lock = Lock()

    def snapshot(self, ticket_id: int) -> TicketState:
        with self._lock:
            return self._state.get(ticket_id, TicketState(last_note_id=0, closed=False))

    def record(self, ticket_id: int, state: TicketState) -> None:
        with self._lock:
            self._state[ticket_id] = state
