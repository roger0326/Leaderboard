public class CustomerScoreComparer : IComparer<Customer>
{
    public int Compare(Customer x, Customer y)
    {
        if (x.Score == y.Score)
        {
            return x.CustomerID.CompareTo(y.CustomerID);
        }
        return x.Score.CompareTo(y.Score);
    }
}