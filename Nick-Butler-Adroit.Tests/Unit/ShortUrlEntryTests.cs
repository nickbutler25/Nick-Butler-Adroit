using NickButlerAdroit.Api.Models;

namespace NickButlerAdroit.Tests.Unit;

public class ShortUrlEntryTests
{
    [Fact]
    public void ClickCount_StartsAtZero()
    {
        var entry = new ShortUrlEntry("abc", "https://example.com");

        Assert.Equal(0, entry.ClickCount);
    }

    [Fact]
    public void IncrementClickCount_IncrementsAtomically()
    {
        var entry = new ShortUrlEntry("abc", "https://example.com");

        entry.IncrementClickCount();
        entry.IncrementClickCount();
        entry.IncrementClickCount();

        Assert.Equal(3, entry.ClickCount);
    }

    [Fact]
    public void Constructor_SetsProperties()
    {
        var entry = new ShortUrlEntry("mycode", "https://example.com");

        Assert.Equal("mycode", entry.ShortCode);
        Assert.Equal("https://example.com", entry.LongUrl);
        Assert.True(entry.CreatedAt <= DateTime.UtcNow);
    }
}
