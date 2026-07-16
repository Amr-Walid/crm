namespace UniGroup.CRM.Client.Services;

/// <summary>
/// Small, dependency-free helpers for reading query-string parameters from an
/// absolute URI in the Blazor WebAssembly client (where
/// <c>NavigationManager.Uri</c> is the only reliable source of the current
/// query string).
/// </summary>
public static class QueryStringHelper
{
    /// <summary>
    /// Returns the first value of <paramref name="key"/> from the query string
    /// of <paramref name="uri"/>, or <c>null</c> if the key is absent.
    /// </summary>
    public static string? GetString(string uri, string key)
    {
        foreach (var (k, v) in EnumeratePairs(uri))
        {
            if (string.Equals(k, key, StringComparison.OrdinalIgnoreCase))
            {
                return v;
            }
        }
        return null;
    }

    /// <summary>
    /// Interprets a query parameter as a boolean. Accepts <c>true</c>, <c>1</c>,
    /// and <c>yes</c> (case-insensitive) as truthy; a bare flag (<c>?embedded</c>
    /// with no value) is also treated as true. Absent key returns false.
    /// </summary>
    public static bool GetBool(string uri, string key)
    {
        foreach (var (k, v) in EnumeratePairs(uri))
        {
            if (!string.Equals(k, key, StringComparison.OrdinalIgnoreCase)) continue;
            if (string.IsNullOrEmpty(v)) return true; // bare "?embedded"
            return v.Equals("true", StringComparison.OrdinalIgnoreCase)
                   || v.Equals("1", StringComparison.Ordinal)
                   || v.Equals("yes", StringComparison.OrdinalIgnoreCase);
        }
        return false;
    }

    private static IEnumerable<(string Key, string Value)> EnumeratePairs(string uri)
    {
        var idx = uri.IndexOf('?');
        if (idx < 0 || idx == uri.Length - 1) yield break;

        // Trim off any fragment (#...) before parsing.
        var query = uri[(idx + 1)..];
        var hashIdx = query.IndexOf('#');
        if (hashIdx >= 0) query = query[..hashIdx];

        foreach (var part in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var eq = part.IndexOf('=');
            if (eq < 0)
            {
                yield return (Uri.UnescapeDataString(part), string.Empty);
            }
            else
            {
                var key = Uri.UnescapeDataString(part[..eq]);
                var value = Uri.UnescapeDataString(part[(eq + 1)..]);
                yield return (key, value);
            }
        }
    }
}
