public class Customer : IComparable<Customer>
{
    public long CustomerID { get; set; }
    public decimal Score { get; set; }
    public int Rank { get; set; }

    public int CompareTo(Customer other)
    {
        if (other == null) return 1;

        int scoreComparison = other.Score.CompareTo(this.Score);
        if (scoreComparison != 0)
            return scoreComparison;

        return this.CustomerID.CompareTo(other.CustomerID);
    }
}