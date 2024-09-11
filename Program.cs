using System.Collections.Concurrent;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

//var leaderboardService = new LeaderboardService();
var leaderboardService = new ShardedLeaderboardService(BucketCount: 10);

app.MapPost("/customer/{customerId}/score/{score}", (long customerId, decimal score) =>
{
    var newScore = leaderboardService.UpdateScore(customerId, score);
    return Results.Ok(new { Score = newScore });
});

app.MapGet("/leaderboard", (int start, int end) =>
{
    var customers = leaderboardService.GetCustomersByRank(start, end);
    return Results.Ok(customers);
});

app.MapGet("/leaderboard/{customerId}", (long customerId, int high = 0, int low = 0) =>
{
    var customers = leaderboardService.GetCustomersByCustomerId(customerId, high, low);
    return Results.Ok(customers);
});

app.MapGet("/leaderboard/firstrun", (int count=1000) =>
{
    leaderboardService.FirstRun(count);
    return Results.Ok();
});

app.Run();
