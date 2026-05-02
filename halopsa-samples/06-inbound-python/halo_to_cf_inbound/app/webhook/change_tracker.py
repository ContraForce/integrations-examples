"""In-memory change tracker, same shape as the CW inbound sample."""

from __future__ import annotations

from dataclasses import dataclass
from threading import Lock


@dataclass(frozen=True)
class TicketState:
    last_action_id: int
    closed: bool


class ChangeTracker:
    def __init__(self) -> None:
        self._state: dict[int, TicketState] = {}
        self._lock = Lock()

    def snapshot(self, ticket_id: int) -> TicketState:
        with self._lock:
            return self._state.get(ticket_id, TicketState(last_action_id=0, closed=False))

    def record(self, ticket_id: int, state: TicketState) -> None:
        with self._lock:
            self._state[ticket_id] = state
