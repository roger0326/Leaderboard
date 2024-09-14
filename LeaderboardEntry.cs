public class LeaderboardEntry
{
    public long CustomerId { get; set; }
    public decimal Score { get; set; }
    public int Rank { get; set; }

    public LeaderboardEntry(long customerId, decimal score, int rank)
    {
        CustomerId = customerId;
        Score = score;
        Rank = rank;
    }
}
