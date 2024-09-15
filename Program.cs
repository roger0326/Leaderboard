using System.Collections.Concurrent;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Caching.Memory;

var builder = WebApplication.CreateBuilder(args);

//builder.Services.AddSingleton<LeaderboardService>();
builder.Services.AddSingleton<SkipListNodeLeaderboardService>();
builder.Services.AddSingleton<RateLimitTracker>();
builder.Services.AddMemoryCache();
//builder.Logging.AddConfiguration(builder.Configuration.GetSection("Logging"));

// Add rate limiting service
builder.Services.AddRateLimiter(options =>
{
    // Read operations rate limiting policy
    options.AddPolicy("read", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = 200,
                QueueLimit = 0,
                Window = TimeSpan.FromMinutes(1)
            }));

    // Write operations rate limiting policy
    options.AddPolicy("write", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = 50,
                QueueLimit = 0,
                Window = TimeSpan.FromMinutes(1)
            }));

    // Custom rate limit handler
    options.OnRejected = async (context, token) =>
    {
        context.HttpContext.Response.StatusCode = 429;
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            await context.HttpContext.Response.WriteAsync(
                $"Too many requests. Please try again after {retryAfter.TotalSeconds} seconds.", cancellationToken: token);
        }
        else
        {
            await context.HttpContext.Response.WriteAsync(
                "Too many requests. Please try again later.", cancellationToken: token);
        }

        // Log rate limit event
        var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
        logger.LogWarning("Rate limit exceeded for IP {IpAddress}",
            context.HttpContext.Connection.RemoteIpAddress);
    };
});

var app = builder.Build();

// Enable rate limiting middleware
app.UseRateLimiter();

// Add rate limit counter middleware
app.Use(async (context, next) =>
{
    var endpoint = context.GetEndpoint();
    var rateLimitingMetadata = endpoint?.Metadata.GetMetadata<EnableRateLimitingAttribute>();
    if (rateLimitingMetadata != null)
    {
        var tracker = context.RequestServices.GetRequiredService<RateLimitTracker>();
        var cacheKey = $"RateLimit_{rateLimitingMetadata.PolicyName}_{context.Connection.RemoteIpAddress}";
        tracker.IncrementCount(cacheKey);
    }
    await next();
});

app.MapGet("/firstrun", async (SkipListNodeLeaderboardService leaderboardService) =>
{
    for (int i = 1; i <= 10000; i++)
    {
        await leaderboardService.UpdateScoreAsync(i, i);
    }
    return Results.Ok("Initialization test data completed");
}).RequireRateLimiting("write");

app.MapPost("/customer/{customerId}/score/{score}", async (long customerId, decimal score, SkipListNodeLeaderboardService leaderboardService) =>
{
    if (customerId <= 0 || score < -1000 || score > 1000)
    {
        return Results.BadRequest("Invalid input parameters");
    }

    var newScore = await leaderboardService.UpdateScoreAsync(customerId, score);
    return Results.Ok(newScore);
}).RequireRateLimiting("write");

app.MapGet("/leaderboard", async (int? start, int? end, SkipListNodeLeaderboardService leaderboardService) =>
{
    if (start == null || end == null || start > end || start <= 0)
    {
        return Results.BadRequest("Invalid input parameters");
    }

    var result = await leaderboardService.GetLeaderboardAsync(start.Value, end.Value);
    return Results.Json(result);
}).RequireRateLimiting("read");

app.MapGet("/leaderboard/{customerId}", async (long customerId, int? high, int? low, SkipListNodeLeaderboardService leaderboardService) =>
{
    if (customerId <= 0 || high < 0 || low < 0)
    {
        return Results.BadRequest("Invalid input parameters");
    }

    high ??= 0;
    low ??= 0;

    var result = await leaderboardService.GetCustomerNeighborsAsync(customerId, high.Value, low.Value);
    if (result.Count == 0)
    {
        return Results.NotFound("Customer not found in the leaderboard");
    }

    return Results.Json(result);
}).RequireRateLimiting("read");

// Add rate limit stats endpoint
app.MapGet("/rate-limit-stats", (RateLimitTracker tracker) =>
{
    return Results.Ok(tracker.GetStats());
});//.RequireAuthorization(); // only authorized users can access this endpoint

app.Run();

//Further adjust the current limiting parameters and statistical methods according to actual needs
public class RateLimitTracker
{
    private readonly ConcurrentDictionary<string, int> _counts = new();
    private readonly IMemoryCache _cache;

    public RateLimitTracker(IMemoryCache cache)
    {
        _cache = cache;
    }

    public void IncrementCount(string key)
    {
        _counts.AddOrUpdate(key, 1, (_, oldValue) => oldValue + 1);
        _cache.GetOrCreate(key, entry =>
        {
            entry.SetAbsoluteExpiration(TimeSpan.FromMinutes(1));
            entry.RegisterPostEvictionCallback((_, _, _, _) =>
            {
                _counts.TryRemove(key, out _);
            });
            return 0;
        });
    }

    public Dictionary<string, int> GetStats()
    {
        return new Dictionary<string, int>(_counts);
    }
}
