"""In-memory change tracker, same role as the CW / Halo inbound samples.

Journal entries (`sys_journal_field`) carry no incrementing integer id, so we
track the latest `sys_created_on` we've forwarded per incident. That column is
stored UTC in the sortable `yyyy-MM-dd HH:mm:ss` format, so a plain string
compare is a correct "newer than" test.
"""

from __future__ import annotations

from dataclasses import dataclass
from threading import Lock


@dataclass(frozen=True)
class IncidentState:
    last_journal_created_on: str
    closed: bool


class ChangeTracker:
    def __init__(self) -> None:
        self._state: dict[str, IncidentState] = {}
        self._lock = Lock()

    def snapshot(self, sys_id: str) -> IncidentState:
        with self._lock:
            return self._state.get(
                sys_id, IncidentState(last_journal_created_on="", closed=False)
            )

    def record(self, sys_id: str, state: IncidentState) -> None:
        with self._lock:
            self._state[sys_id] = state
