var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<LeaderboardService>();

var app = builder.Build();

app.MapGet("/firstrun", async (LeaderboardService leaderboardService) =>
{
    for (int i = 1; i <= 1000; i++)
    {
        await leaderboardService.UpdateScoreAsync(i, i);
    }
    return Results.Ok("Initialization test data completed");
});

app.MapPost("/customer/{customerId}/score/{score}", async (long customerId, decimal score, LeaderboardService leaderboardService) =>
{
    if (customerId <= 0 || score < -1000 || score > 1000)
    {
        return Results.BadRequest("Invalid input parameters");
    }

    var newScore = await leaderboardService.UpdateScoreAsync(customerId, score);
    return Results.Ok(newScore);
});

app.MapGet("/leaderboard", async (int? start, int? end, LeaderboardService leaderboardService) =>
{
    if (start == null || end == null || start > end || start <= 0)
    {
        return Results.BadRequest("Invalid input parameters");
    }

    var result = await leaderboardService.GetLeaderboardAsync(start.Value, end.Value);
    return Results.Ok(result);
});

app.MapGet("/leaderboard/{customerId}", async (long customerId, int? high, int? low, LeaderboardService leaderboardService) =>
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

    return Results.Ok(result);
});

app.Run();
