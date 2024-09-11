using System.Collections.Concurrent;
using System.ComponentModel;

public class ShardedLeaderboardService
{
    private ConcurrentDictionary<long, Customer> customers = new();
    private ConcurrentDictionary<long, SortedSet<Customer>> scoreRankings = new();
    private readonly ReaderWriterLockSlim leaderboardLock = new ReaderWriterLockSlim(LockRecursionPolicy.SupportsRecursion);
    //private ReaderWriterLockSlim leaderboardLock = new();
    private readonly int _bucketCount;


    public ShardedLeaderboardService(int BucketCount = 1000)
    {
        _bucketCount = BucketCount;
    }

    public decimal UpdateScore(long customerId, decimal scoreChange)
    {
        if (scoreChange > 1000 || scoreChange < -1000)
            throw new ArgumentOutOfRangeException(nameof(scoreChange), "Score must be between -1000 and 1000.");

        var customer = customers.GetOrAdd(customerId, new Customer { CustomerID = customerId, Score = 0, Rank = 0 });
        leaderboardLock.EnterWriteLock();
        try
        {
            // Remove the customer temporarily to update the score and reinsert
            var scoreBucket = GetScoreBucket(customer.Score);
            customer.Score += scoreChange;
            if (scoreRankings.Count > 0 && scoreRankings.ContainsKey(scoreBucket) && scoreRankings[scoreBucket].Contains(customer))
            {
                scoreRankings[scoreBucket].Remove(customer);
            }
            if (customer.Score > 0)
            {
                scoreBucket = GetScoreBucket(customer.Score);
                scoreRankings.TryAdd(scoreBucket, new SortedSet<Customer>());
                scoreRankings[scoreBucket].Add(customer);
                RecomputeRanks(customer, scoreBucket);
            }
        }
        finally
        {
            leaderboardLock.ExitWriteLock();
        }
        return customer.Score;
    }

    private void RecomputeRanks(Customer currentCustomer, long scoreBucket)
    {
        // RecomputeRanks
        var sortedCustomers = customers.Values
            .Where(c => c.Score > 0)
            .OrderByDescending(c => c.Score)
            .ThenBy(c => c.CustomerID)
            .ToList();

        for (int i = 0; i < sortedCustomers.Count; i++)
        {
            sortedCustomers[i].Rank = i + 1;
        }

        // update scoreRankings
        scoreRankings.Clear();
        foreach (var customer in sortedCustomers)
        {
            var bucket = (long)customer.Score;
            if (!scoreRankings.ContainsKey(bucket))
            {
                scoreRankings[bucket] = new();
            }
            scoreRankings[bucket].Add(customer);
        }

        // 
        //foreach (var bucket in scoreRankings.Keys)
        //{
        //    scoreRankings[bucket].Sort();
        //}

        //int rank = 1;
        //var newCustomer = currentCustomer;
        //foreach (var customer in scoreRankings[scoreBucket])
        //{
        //    if (currentCustomer.CustomerID == customer.CustomerID)
        //    {
        //        newCustomer.Rank = rank;
        //    }
        //    rank++;
        //}
        //foreach (var key in scoreRankings.Keys)
        //{
        //    if (key > scoreBucket)
        //    {
        //        newCustomer.Rank += scoreRankings[key].Count;
        //    }
        //}
        //leaderboardLock.EnterWriteLock();
        //try
        //{
        //    customers.TryUpdate(currentCustomer.CustomerID, newCustomer, currentCustomer);
        //}
        //finally
        //{
        //    leaderboardLock.ExitWriteLock();
        //}
    }

    private long GetScoreBucket(decimal score)
    {

        if (score <= 0)
            return 0;
        // Shard 
        return (long)(score / _bucketCount);
    }

    public List<Customer> GetCustomersByRank(int start, int end)
    {
        leaderboardLock.EnterReadLock();
        try
        {
            var result = new List<Customer>();

            var startCustomer = customers.Values.FirstOrDefault((c) => c.Rank == start);
            if (startCustomer == null) return result;

            var endCustomer = customers.Values.FirstOrDefault((c) => c.Rank == end);
            if (startCustomer == null) return result;

            result= customers.Values.OrderBy((c)=>c.Rank).Skip(start - 1).Take(end - start + 1).ToList();
            //int currentCount = 0;
            //int skipStart = start;
            //bool isFirst = true;
            //foreach (var key in scoreRankings.Keys.OrderByDescending((i) => i))
            //{
            //    currentCount += scoreRankings[key].Count;
            //    if (skipStart <= currentCount)
            //    {
            //        if (end <= currentCount)
            //        {
            //            result.AddRange(scoreRankings[key].Skip(isFirst ? skipStart - 1 : 0).Take(end - skipStart + 1));
            //        }
            //        else
            //        {
            //            result.AddRange(scoreRankings[key].Skip(isFirst ? skipStart - 1 : 0).Take(scoreRankings[key].Count));
            //        }
            //        isFirst = false;
            //        skipStart += result.Count;
            //    }
            //}
            return result;
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
            if (customers.Count < 1)
            {
                return new List<Customer>();
            }
            var customer = customers[customerId];
            if (customer == null) return new List<Customer>();

            var result = new List<Customer>();

            return GetCustomersByRank(customer.Rank - high, customer.Rank + low);
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