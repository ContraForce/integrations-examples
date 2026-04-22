namespace ContraForce.Samples.CwInbound.CallbackModels;

/// <summary>
/// Parses the <c>externalReference</c> format written by the outbound samples:
/// <c>cf|{source}|{incidentId}</c>.
/// </summary>
public static class ExternalReferenceParser
{
    public static bool TryParse(string? externalReference, out string source, out string incidentId)
    {
        source = string.Empty;
        incidentId = string.Empty;

        if (string.IsNullOrEmpty(externalReference))
            return false;

        var parts = externalReference.Split('|', 3);
        if (parts.Length != 3 || parts[0] != "cf")
            return false;

        source = parts[1];
        incidentId = parts[2];
        return !string.IsNullOrEmpty(source) && !string.IsNullOrEmpty(incidentId);
    }
}
