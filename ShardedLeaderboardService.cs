//public class ShardedLeaderboardService
//{
//    private readonly List<LeaderboardShard> _shards;
//    private readonly int _shardCount;
//    private readonly decimal _scoreRange;

//    public ShardedLeaderboardService(int shardCount, decimal maxScore)
//    {
//        _shardCount = shardCount;
//        _scoreRange = maxScore / shardCount;
//        _shards = Enumerable.Range(0, shardCount)
//            .Select(_ => new LeaderboardShard())
//            .ToList();
//    }

//    private int GetShardIndex(decimal score)
//    {
//        return Math.Min((int)(score / _scoreRange), _shardCount - 1);
//    }

//    public async Task<decimal> UpdateScoreAsync(long customerId, decimal scoreChange)
//    {
//        var oldShardIndex = GetShardIndex(await GetScoreAsync(customerId));
//        var newScore = await _shards[oldShardIndex].UpdateScoreAsync(customerId, scoreChange);
//        var newShardIndex = GetShardIndex(newScore);

//        if (oldShardIndex != newShardIndex)
//        {
//            await _shards[oldShardIndex].RemoveCustomerAsync(customerId);
//            await _shards[newShardIndex].AddCustomerAsync(customerId, newScore);
//        }

//        return newScore;
//    }

//    public async Task<List<LeaderboardEntry>> GetLeaderboardAsync(int start, int end)
//    {
//        var tasks = _shards.Select(shard => shard.GetLeaderboardAsync(0, end)).ToList();
//        var results = await Task.WhenAll(tasks);
//        return results.SelectMany(x => x)
//            .OrderByDescending(x => x.Score)
//            .ThenBy(x => x.CustomerId)
//            .Skip(start - 1)
//            .Take(end - start + 1)
//            .Select((entry, index) => new LeaderboardEntry
//            {
//                CustomerId = entry.CustomerId,
//                Score = entry.Score,
//                Rank = start + index
//            })
//            .ToList();
//    }

//    public async Task<List<LeaderboardEntry>> GetCustomerNeighborsAsync(long customerId, int high, int low)
//    {
//        var score = await GetScoreAsync(customerId);
//        var shardIndex = GetShardIndex(score);
//        var result = await _shards[shardIndex].GetCustomerNeighborsAsync(customerId, high, low);

//        if (result.Count < high + low + 1)
//        {
//            // 需要从相邻的分片获取更多数据
//            // 这里需要实现跨分片查询的逻辑
//        }

//        return result;
//    }

//    private async Task<decimal> GetScoreAsync(long customerId)
//    {
//        foreach (var shard in _shards)
//        {
//            var score = await shard.GetScoreAsync(customerId);
//            if (score > 0) return score;
//        }
//        return 0;
//    }
//}