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
builder.Services.AddOpenApi();       // OpenAPI/Swagger document generation

// Configure rate limiting with fixed-window policies to prevent abuse.
// Each policy tracks requests per IP within a 1-minute sliding window.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // URL creation: 20 requests/minute — prevents spam link farms
    options.AddFixedWindowLimiter("create", limiterOptions =>
    {
        limiterOptions.PermitLimit = 20;
        limiterOptions.Window = TimeSpan.FromMinutes(1);
        limiterOptions.QueueLimit = 0;     // Reject immediately when limit is reached
    });

    // URL resolve (API lookup): 20 requests/minute — prevents scraping
    options.AddFixedWindowLimiter("resolve", limiterOptions =>
    {
        limiterOptions.PermitLimit = 20;
        limiterOptions.Window = TimeSpan.FromMinutes(1);
        limiterOptions.QueueLimit = 0;
    });

    // Redirect (click-through): 60 requests/minute — higher limit for normal browsing
    options.AddFixedWindowLimiter("redirect", limiterOptions =>
    {
        limiterOptions.PermitLimit = 60;
        limiterOptions.Window = TimeSpan.FromMinutes(1);
        limiterOptions.QueueLimit = 0;
    });
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
