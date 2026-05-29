using System.Text;

namespace Civic.API.Services.Campaign;

/// <summary>
/// Opaque cursor for reverse-chronological feeds. Encodes the CreatedAt of the
/// last returned item; decoding yields the "older than" bound for the next page.
/// </summary>
public static class FeedCursor
{
    public static string Encode(DateTime createdAt) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes(
            DateTime.SpecifyKind(createdAt, DateTimeKind.Utc).Ticks.ToString()));

    public static bool TryDecode(string? cursor, out DateTime before)
    {
        before = default;
        if (string.IsNullOrWhiteSpace(cursor)) return false;
        try
        {
            var raw = Encoding.UTF8.GetString(Convert.FromBase64String(cursor));
            if (long.TryParse(raw, out var ticks))
            {
                before = new DateTime(ticks, DateTimeKind.Utc);
                return true;
            }
        }
        catch (FormatException) { /* malformed cursor → ignore */ }
        return false;
    }
}
