// Application entry point and configuration for the Nick-Butler-Adroit API.
// Sets up dependency injection, middleware pipeline, rate limiting, CORS, and SignalR.

using System.Net;
using System.Text.Json;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.RateLimiting;
using NickButlerAdroit.Api.Hubs;
using NickButlerAdroit.Api.Repositories;
using NickButlerAdroit.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// Register core framework services
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    });
builder.Services.AddSignalR()
    .AddJsonProtocol(options =>
    {
        options.PayloadSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    });
builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer((document, context, ct) =>
    {
        document.Info.Title = "Nick-Butler-Adroit URL Shortener API";
        document.Info.Description = "A URL shortener proof-of-concept API. Create, resolve, and manage shortened URLs with click statistics and real-time notifications via SignalR.";
        document.Info.Version = "v1";
        return Task.CompletedTask;
    });
});

// Configure rate limiting with per-IP fixed-window policies to prevent abuse.
// Each policy partitions by client IP, allowing each address its own 1-minute budget.
// A global limiter acts as a safety net to protect the in-memory store from being
// overwhelmed regardless of how many distinct IPs are making requests.
// When DISABLE_RATE_LIMITING is set (e.g., in integration tests), all policies
// become no-ops so tests can run without hitting request limits.
var disableRateLimiting = builder.Configuration.GetValue<bool>("DISABLE_RATE_LIMITING");

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    if (disableRateLimiting)
    {
        // No-op policies for test environments
        options.AddPolicy("create", (HttpContext _) =>
            RateLimitPartition.GetNoLimiter(string.Empty));
        options.AddPolicy("resolve", (HttpContext _) =>
            RateLimitPartition.GetNoLimiter(string.Empty));
        options.AddPolicy("redirect", (HttpContext _) =>
            RateLimitPartition.GetNoLimiter(string.Empty));
    }
    else
    {
        // Global safety net: 1000 requests/minute across all clients combined.
        // Protects the in-memory store from distributed attacks (e.g., botnets).
        options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(_ =>
            RateLimitPartition.GetFixedWindowLimiter("global", _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 1000,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
            }));

        // URL creation/deletion: 20 requests/minute per IP — prevents spam link farms
        options.AddPolicy("create", context =>
            RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 20,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                }));

        // URL resolve (API lookup): 30 requests/minute per IP — prevents scraping
        options.AddPolicy("resolve", context =>
            RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 30,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                }));

        // Redirect (click-through): 60 requests/minute per IP — higher limit for normal browsing
        options.AddPolicy("redirect", context =>
            RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 60,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                }));
    }
});

// Register application services as singletons (in-memory storage requires single instance)
builder.Services.AddSingleton<IUrlRepository, InMemoryUrlRepository>();
builder.Services.AddSingleton<IUrlShortenerService, UrlShortenerService>();

// Configure CORS to allow the React frontend (localhost:3000) to call the API.
// AllowCredentials is required for SignalR WebSocket connections.
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins("http://localhost:3000")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

var app = builder.Build();

// Global exception handler — catches unhandled exceptions and returns a JSON error response.
// In development, includes the exception message; in production, returns a generic message
// to avoid leaking sensitive implementation details.
app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
        context.Response.ContentType = "application/json";

        var exceptionFeature = context.Features.Get<IExceptionHandlerFeature>();
        var logger = context.RequestServices.GetRequiredService<ILoggerFactory>()
            .CreateLogger("GlobalExceptionHandler");

        var message = "An unexpected error occurred.";
        if (exceptionFeature?.Error is not null)
        {
            logger.LogError(exceptionFeature.Error, "Unhandled exception");
            if (app.Environment.IsDevelopment())
            {
                message = exceptionFeature.Error.Message;
            }
        }

        var json = JsonSerializer.Serialize(new { error = message });
        await context.Response.WriteAsync(json);
    });
});

// Enable Swagger UI only in development for API exploration
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(options => options.SwaggerEndpoint("/openapi/v1.json", "Nick-Butler-Adroit API"));
}

// Middleware pipeline order matters: CORS → Rate Limiting → Routing
app.UseCors();
app.UseRateLimiter();
app.MapControllers();
app.MapHub<UrlHub>("/hubs/urls");  // SignalR hub endpoint for real-time URL events

app.Run();

// Partial class declaration enables WebApplicationFactory<Program> in integration tests
public partial class Program { }
