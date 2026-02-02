using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace NickButlerAdroit.Tests.Integration;

/// <summary>
/// Custom WebApplicationFactory that disables rate limiting for integration tests.
/// Sets the DISABLE_RATE_LIMITING configuration flag, which causes Program.cs to
/// register no-op rate limiting policies. Tests that verify rate limiting behaviour
/// should use the standard <see cref="WebApplicationFactory{TEntryPoint}"/> instead.
/// </summary>
public class NoRateLimitWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration(config =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DISABLE_RATE_LIMITING"] = "true",
            });
        });
    }
}
