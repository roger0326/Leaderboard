using System.Collections.Concurrent;

public class SkipListNodeLeaderboardService
{
    private readonly ConcurrentDictionary<long, Customer> customers = new();
    private readonly SkipList<(decimal Score, long CustomerId)> leaderboard;
    private readonly ReaderWriterLockSlim rwLock = new ReaderWriterLockSlim();

    public SkipListNodeLeaderboardService()
    {
        leaderboard = new SkipList<(decimal Score, long CustomerId)>(
            maxLevel: 32,
            probability: 0.5,
            customComparer: (a, b) =>
            {
                int scoreComparison = b.Score.CompareTo(a.Score);
                return scoreComparison != 0 ? scoreComparison : a.CustomerId.CompareTo(b.CustomerId);
            }
        );
    }

    public async Task<decimal> UpdateScoreAsync(long customerId, decimal scoreChange)
    {
        var customer = customers.GetOrAdd(customerId, _ => new Customer { CustomerId = customerId });

        rwLock.EnterWriteLock();
        try
        {
            decimal oldScore = customer.Score;
            decimal newScore = oldScore + scoreChange;

            customer.Score = newScore;

            leaderboard.Remove((oldScore, customerId));
            if (newScore > 0)
            {
                leaderboard.Insert((newScore, customerId));
            }

            return newScore;
        }
        finally
        {
            rwLock.ExitWriteLock();
        }
    }

    public async Task<List<LeaderboardEntry>> GetLeaderboardAsync(int startRank, int endRank)
    {
        rwLock.EnterReadLock();
        try
        {
            var leaderboardList = new List<LeaderboardEntry>();
            var current = leaderboard.Head.Next[0];
            int currentRank = 1;

            while (current != leaderboard.Tail && currentRank <= endRank)
            {
                if (currentRank >= startRank)
                {
                    leaderboardList.Add(new LeaderboardEntry(current.Value.CustomerId, current.Value.Score, currentRank));
                }
                current = current.Next[0];
                currentRank++;
            }

            return leaderboardList;
        }
        finally
        {
            rwLock.ExitReadLock();
        }
    }

    public async Task<List<LeaderboardEntry>> GetCustomerNeighborsAsync(long customerId, int prevCount, int nextCount)
    {
        rwLock.EnterReadLock();
        try
        {
            var neighbors = new List<LeaderboardEntry>();

            if (!customers.TryGetValue(customerId, out var customer))
            {
                throw new ArgumentException("Customer not found");
            }

            var currentScore = (customer.Score, customerId);
            var currentNode = leaderboard.FindNode(currentScore);

            if (currentNode == null)
            {
                throw new InvalidOperationException("Customer not found in leaderboard");
            }

            int currentRank = leaderboard.GetRank(currentScore);

            var prevNode = currentNode.Previous;
            for (int i = 0; i < prevCount && prevNode != leaderboard.Head; i++)
            {
                neighbors.Insert(0, new LeaderboardEntry(prevNode.Value.CustomerId, prevNode.Value.Score, currentRank - i - 1));
                prevNode = prevNode.Previous;
            }

            neighbors.Add(new LeaderboardEntry(customerId, customer.Score, currentRank));

            var nextNode = currentNode.Next[0];
            for (int i = 0; i < nextCount && nextNode != leaderboard.Tail; i++)
            {
                neighbors.Add(new LeaderboardEntry(nextNode.Value.CustomerId, nextNode.Value.Score, currentRank + i + 1));
                nextNode = nextNode.Next[0];
            }

            return neighbors;
        }
        finally
        {
            rwLock.ExitReadLock();
        }
    }
}