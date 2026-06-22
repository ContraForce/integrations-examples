using System.Security.Cryptography;
using System.Text;

namespace ContraForce.Samples.SnowOutbound.Webhook;

/// <summary>
/// Verifies the HMAC-SHA256 signature ContraForce sends with each webhook.
/// </summary>
/// <remarks>
/// Signature format: <c>HMAC_SHA256(secret, "{timestamp}.{raw_body}")</c>
/// returned as <c>sha256=&lt;lowercase_hex&gt;</c>.
/// Raw body bytes must be compared exactly — do not round-trip through JSON.
/// </remarks>
public static class WebhookSignatureValidator
{
    public static bool Verify(
        string secret,
        string signatureHeader,
        string timestampHeader,
        ReadOnlySpan<byte> rawBody,
        int maxSkewSeconds,
        DateTimeOffset now
    )
    {
        if (string.IsNullOrEmpty(signatureHeader) || string.IsNullOrEmpty(timestampHeader))
            return false;

        if (!long.TryParse(timestampHeader, out var ts))
            return false;

        var eventTime = DateTimeOffset.FromUnixTimeSeconds(ts);
        if (Math.Abs((now - eventTime).TotalSeconds) > maxSkewSeconds)
            return false;

        var keyBytes = Encoding.UTF8.GetBytes(secret);
        var prefix = Encoding.UTF8.GetBytes($"{ts}.");

        using var hmac = new HMACSHA256(keyBytes);
        hmac.TransformBlock(prefix, 0, prefix.Length, null, 0);
        var bodyArray = rawBody.ToArray();
        hmac.TransformFinalBlock(bodyArray, 0, bodyArray.Length);
        var expected = "sha256=" + Convert.ToHexString(hmac.Hash!).ToLowerInvariant();

        var a = Encoding.UTF8.GetBytes(expected);
        var b = Encoding.UTF8.GetBytes(signatureHeader);
        return a.Length == b.Length && CryptographicOperations.FixedTimeEquals(a, b);
    }
}
