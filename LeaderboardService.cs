using System.Collections.Concurrent;

public class LeaderboardService
{

    ConcurrentDictionary<long, Customer> customers = new();
    SortedSet<Customer> scoreRankings = new(new CustomerScoreComparer());
    private ReaderWriterLockSlim leaderboardLock = new();

    public decimal UpdateScore(long customerId, decimal scoreChange)
    {
        if (scoreChange > 1000 || scoreChange < -1000)
            throw new ArgumentOutOfRangeException(nameof(scoreChange), "Score must be between -1000 and 1000.");

        var customer = customers.GetOrAdd(customerId, new Customer { CustomerID = customerId, Score = 0, Rank = 0 });
        leaderboardLock.EnterWriteLock();
        try
        {
            // Remove the customer temporarily to update the score and reinsert
            scoreRankings.Remove(customer);
            customer.Score += scoreChange;
            if (customer.Score > 0)
            {
                scoreRankings.Add(customer);
                RecomputeRanks(customer);
            }
        }
        finally
        {
            leaderboardLock.ExitWriteLock();
        }
        return customer.Score;
    }

    private void RecomputeRanks(Customer currentCustomer)
    {
        int rank = 1;
        foreach (var customer in scoreRankings)
        {
            if (currentCustomer.CustomerID == customer.CustomerID)
            {
                customer.Rank = rank;
            }
            rank++;
        }
    }

    public List<Customer> GetCustomersByRank(int start, int end)
    {
        leaderboardLock.EnterReadLock();
        try
        {
            return scoreRankings.Skip(start - 1).Take(end - start + 1).ToList();
        }
        finally
        {
            leaderboardLock.ExitReadLock();
        }
    }

    public List<Customer> GetCustomersByCustomerId(long customerId, int high, int low)
    {
        leaderboardLock.EnterReadLock();
        try
        {
            var customer = scoreRankings.FirstOrDefault(c => c.CustomerID == customerId);
            if (customer == null) return new List<Customer>();

            var index = scoreRankings.ToList().FindIndex(c => c.CustomerID == customerId);
            var result = scoreRankings.Skip(Math.Max(0, index - high)).Take(high).ToList();
            result.Add(customer);
            result.AddRange(scoreRankings.Skip(index + 1).Take(low));

            return result;
        }
        finally
        {
            leaderboardLock.ExitReadLock();
        }
    }

    public void FirstRun(int count)
    {
        for (int i = 1; i <= count; i++)
        {
            UpdateScore(i, i);
        }
    }
}