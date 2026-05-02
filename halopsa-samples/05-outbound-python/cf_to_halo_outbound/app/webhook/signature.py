"""
Verifies the HMAC-SHA256 signature ContraForce sends with each webhook.

Signature format: HMAC_SHA256(secret, "{timestamp}.{raw_body}")
returned as "sha256=<lowercase_hex>".

Raw body bytes must be compared exactly — do not round-trip through JSON.
"""

from __future__ import annotations

import hashlib
import hmac
import time


def verify(
    *,
    secret: str,
    signature_header: str | None,
    timestamp_header: str | None,
    raw_body: bytes,
    max_skew_seconds: int,
    now_unix: int | None = None,
) -> bool:
    if not signature_header or not timestamp_header:
        return False

    try:
        ts = int(timestamp_header)
    except ValueError:
        return False

    current = now_unix if now_unix is not None else int(time.time())
    if abs(current - ts) > max_skew_seconds:
        return False

    message = f"{ts}.".encode("utf-8") + raw_body
    digest = hmac.new(secret.encode("utf-8"), message, hashlib.sha256).hexdigest()
    expected = f"sha256={digest}"

    return hmac.compare_digest(expected, signature_header)
