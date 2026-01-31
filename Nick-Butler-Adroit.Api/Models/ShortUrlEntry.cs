namespace NickButlerAdroit.Api.Models;

/// <summary>
/// Internal storage entity representing a shortened URL mapping.
/// Stored in the repository's ConcurrentDictionary. Mutable only via
/// the thread-safe <see cref="IncrementClickCount"/> method; all other
/// properties are read-only and set at creation time.
/// </summary>
public class ShortUrlEntry
{
    /// <summary>The unique short code (key in the dictionary).</summary>
    public string ShortCode { get; }

    /// <summary>The normalized destination URL.</summary>
    public string LongUrl { get; }

    /// <summary>UTC timestamp of when this entry was created.</summary>
    public DateTime CreatedAt { get; }

    /// <summary>
    /// Current click count. Reads the volatile _clickCount field.
    /// Thread-safe because int reads are atomic on all .NET platforms.
    /// </summary>
    public int ClickCount => _clickCount;

    /// <summary>Backing field for click count, modified only via Interlocked.Increment.</summary>
    private int _clickCount;

    public ShortUrlEntry(string shortCode, string longUrl)
    {
        ShortCode = shortCode;
        LongUrl = longUrl;
        CreatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Atomically increments the click counter using Interlocked.Increment.
    /// Safe for concurrent calls from multiple threads (e.g., simultaneous redirect requests).
    /// Returns the new count after incrementing.
    /// </summary>
    public int IncrementClickCount()
    {
        return Interlocked.Increment(ref _clickCount);
    }
}
