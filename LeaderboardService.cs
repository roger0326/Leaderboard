using System.Collections.Concurrent;

public class LeaderboardService
{
    private readonly ConcurrentDictionary<long, Customer> customers = new();
    private readonly SortedDictionary<(decimal Score, long CustomerId), long> leaderboard = new(Comparer<(decimal Score, long CustomerId)>.Create((a, b) =>
    {
        int scoreComparison = b.Score.CompareTo(a.Score);
        return scoreComparison != 0 ? scoreComparison : a.CustomerId.CompareTo(b.CustomerId);
    }));
    private readonly AsyncReaderWriterLock asyncLock = new();

    public async Task<decimal> UpdateScoreAsync(long customerId, decimal scoreChange)
    {
        using (await asyncLock.WriterLockAsync())
        {
            var customer = customers.GetOrAdd(customerId, _ => new Customer { CustomerId = customerId });
            decimal oldScore = customer.Score;
            customer.Score += scoreChange;

            if (oldScore > 0)
            {
                leaderboard.Remove((oldScore, customerId));
            }
            if (customer.Score > 0)
            {
                leaderboard[(customer.Score, customerId)] = customerId;
            }

            return customer.Score;
        }
    }

    public async Task<List<LeaderboardEntry>> GetLeaderboardAsync(int start, int end)
    {
        using (await asyncLock.ReaderLockAsync())
        {
            var result = new List<LeaderboardEntry>();
            var rankedCustomers = leaderboard.Skip(start - 1).Take(end - start + 1);
            int rank = start;
            foreach (var entry in rankedCustomers)
            {
                result.Add(new LeaderboardEntry
                {
                    CustomerId = entry.Value,
                    Score = entry.Key.Score,
                    Rank = rank++
                });
            }
            return result;
        }
    }

    public async Task<List<LeaderboardEntry>> GetCustomerNeighborsAsync(long customerId, int high, int low)
    {
        using (await asyncLock.ReaderLockAsync())
        {
            var result = new List<LeaderboardEntry>();
            if (!customers.TryGetValue(customerId, out var customer) || customer.Score <= 0)
            {
                return result;
            }

            var customerEntry = leaderboard.FirstOrDefault(x => x.Value == customerId);
            if (customerEntry.Equals(default))
            {
                return result;
            }

            int rank = leaderboard.Count(x => x.Key.Score > customerEntry.Key.Score || (x.Key.Score == customerEntry.Key.Score && x.Key.CustomerId < customerEntry.Key.CustomerId)) + 1;

            var higherRanked = leaderboard.Where(x => x.Key.Score > customerEntry.Key.Score || (x.Key.Score == customerEntry.Key.Score && x.Key.CustomerId < customerEntry.Key.CustomerId)).TakeLast(high);
            var lowerRanked = leaderboard.Where(x => x.Key.Score < customerEntry.Key.Score || (x.Key.Score == customerEntry.Key.Score && x.Key.CustomerId > customerEntry.Key.CustomerId)).Take(low);

            int higherRankedCount = higherRanked.Count();
            foreach (var entry in higherRanked)
            {
                result.Add(new LeaderboardEntry
                {
                    CustomerId = entry.Value,
                    Score = entry.Key.Score,
                    Rank = rank - higherRankedCount
                });
                higherRankedCount--;
            }

            result.Add(new LeaderboardEntry
            {
                CustomerId = customerEntry.Value,
                Score = customerEntry.Key.Score,
                Rank = rank
            });

            rank++;
            foreach (var entry in lowerRanked)
            {
                result.Add(new LeaderboardEntry
                {
                    CustomerId = entry.Value,
                    Score = entry.Key.Score,
                    Rank = rank++
                });
            }

            return result;
        }
    }
}
