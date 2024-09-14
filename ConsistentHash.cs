using System.Security.Cryptography;
using System.Text;

public class ConsistentHash<T>
{
    private readonly SortedDictionary<int, T> circle = new();
    private readonly int numberOfReplicas;

    public ConsistentHash(int numberOfReplicas)
    {
        this.numberOfReplicas = numberOfReplicas;
    }

    public void Add(T node)
    {
        for (int i = 0; i < numberOfReplicas; i++)
        {
            int hash = Hash(node.GetHashCode().ToString() + i);
            circle[hash] = node;
        }
    }

    public void Remove(T node)
    {
        for (int i = 0; i < numberOfReplicas; i++)
        {
            int hash = Hash(node.GetHashCode().ToString() + i);
            circle.Remove(hash);
        }
    }

    public T Get(object key)
    {
        if (circle.Count == 0)
        {
            throw new InvalidOperationException("No nodes in the hash circle");
        }

        int hash = Hash(key.ToString());
        if (!circle.ContainsKey(hash))
        {
            var tailMap = circle.Keys.Where(k => k >= hash);
            hash = tailMap.Any() ? tailMap.First() : circle.Keys.First();
        }

        return circle[hash];
    }

    private int Hash(string key)
    {
        using var md5 = MD5.Create();
        byte[] data = md5.ComputeHash(Encoding.UTF8.GetBytes(key));
        return BitConverter.ToInt32(data, 0);
    }
}