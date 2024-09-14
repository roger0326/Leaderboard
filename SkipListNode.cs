using System.Collections.Concurrent;

public class SkipListNode<T>
{
    public T Value { get; set; }
    public SkipListNode<T>[] Next { get; private set; }
    public SkipListNode<T> Previous { get; set; }
    public ConcurrentDictionary<int, int> SpanMap { get; private set; }

    public SkipListNode(T value, int levels)
    {
        Value = value;
        Next = new SkipListNode<T>[levels];
        SpanMap = new ();
        for (int i = 0; i < levels; i++)
        {
            SpanMap[i] = 1; // 初始化跨度为1
        }
        Previous = null;
    }
}

public class SkipList<T> where T : IComparable<T>
{
    private readonly int maxLevel;
    private readonly double probability;
    public readonly SkipListNode<T> Head;
    public readonly SkipListNode<T> Tail;
    private int currentLevel;
    private readonly Random random;
    private readonly Comparison<T> comparer;
    private int count;

    public SkipList(int maxLevel = 32, double probability = 0.5, Comparison<T> customComparer = null)
    {
        this.maxLevel = maxLevel;
        this.probability = probability;
        this.Head = new SkipListNode<T>(default, maxLevel);
        this.Tail = new SkipListNode<T>(default, maxLevel);
        for (int i = 0; i < maxLevel; i++)
        {
            Head.Next[i] = Tail;
            Head.SpanMap[i] = 1;
        }
        this.Tail.Previous = Head;
        this.currentLevel = 1;
        this.random = new Random();
        this.comparer = customComparer != null ? customComparer : DefaultComparer;
        this.count = 0;
    }

    private int DefaultComparer(T a, T b)
    {
        if (a is ValueTuple<decimal, long> tupleA && b is ValueTuple<decimal, long> tupleB)
        {
            int scoreComparison = tupleA.Item1.CompareTo(tupleB.Item1);
            return scoreComparison != 0 ? -scoreComparison : tupleA.Item2.CompareTo(tupleB.Item2);
        }
        return a.CompareTo(b);
    }

    private int GetRandomLevel()
    {
        int level = 1;
        while (random.NextDouble() < probability && level < maxLevel)
        {
            level++;
        }
        return level;
    }

    public void Insert(T value)
    {
        var update = new SkipListNode<T>[maxLevel];
        var current = Head;
        var rank = new int[maxLevel];

        for (int i = currentLevel - 1; i >= 0; i--)
        {
            rank[i] = i == currentLevel - 1 ? 0 : rank[i + 1];
            while (current.Next[i] != Tail && comparer(current.Next[i].Value, value) < 0)
            {
                rank[i] += current.SpanMap[i];
                current = current.Next[i];
            }
            update[i] = current;
        }

        int level = GetRandomLevel();

        if (level > currentLevel)
        {
            for (int i = currentLevel; i < level; i++)
            {
                rank[i] = 0;
                update[i] = Head;
                Head.SpanMap[i] = count + 1;
            }
            currentLevel = level;
        }

        var newNode = new SkipListNode<T>(value, level);
        for (int i = 0; i < level; i++)
        {
            newNode.Next[i] = update[i].Next[i];
            update[i].Next[i] = newNode;

            newNode.SpanMap[i] = update[i].SpanMap[i] - (rank[0] - rank[i]);
            update[i].SpanMap[i] = (rank[0] - rank[i]) + 1;
        }

        for (int i = level; i < currentLevel; i++)
        {
            update[i].SpanMap[i]++;
        }

        newNode.Previous = update[0];
        if (newNode.Next[0] != Tail)
        {
            newNode.Next[0].Previous = newNode;
        }
        else
        {
            Tail.Previous = newNode;
        }

        count++;
    }

    public bool Remove(T value)
    {
        var update = new SkipListNode<T>[maxLevel];
        var current = Head;

        for (int i = currentLevel - 1; i >= 0; i--)
        {
            while (current.Next[i] != Tail && comparer(current.Next[i].Value, value) < 0)
            {
                current = current.Next[i];
            }
            update[i] = current;
        }

        current = current.Next[0];

        if (current != Tail && comparer(current.Value, value) == 0)
        {
            for (int i = 0; i < currentLevel; i++)
            {
                if (update[i].Next[i] != current)
                    break;
                update[i].Next[i] = current.Next[i];
                update[i].SpanMap[i] += current.SpanMap[i] - 1;
            }

            if (current.Next[0] != Tail)
            {
                current.Next[0].Previous = current.Previous;
            }
            else
            {
                Tail.Previous = current.Previous;
            }

            while (currentLevel > 1 && Head.Next[currentLevel - 1] == Tail)
            {
                currentLevel--;
            }

            count--;
            return true;
        }

        return false;
    }

    public int GetCount()
    {
        return count;
    }

    public SkipListNode<T> FindNode(T value)
    {
        var current = Head;
        for (int i = currentLevel - 1; i >= 0; i--)
        {
            while (current.Next[i] != Tail && comparer(current.Next[i].Value, value) < 0)
            {
                current = current.Next[i];
            }
        }
        current = current.Next[0];
        return (current != Tail && comparer(current.Value, value) == 0) ? current : null;
    }

    public int GetRank(T value)
    {
        var current = Head;
        int rank = 0;
        for (int i = currentLevel - 1; i >= 0; i--)
        {
            while (current.Next[i] != Tail && comparer(current.Next[i].Value, value) < 0)
            {
                rank += current.SpanMap[i];
                current = current.Next[i];
            }
        }
        current = current.Next[0];
        return (current != Tail && comparer(current.Value, value) == 0) ? rank + 1 : rank;
    }

    public IEnumerable<T> GetAllElements()
    {
        var current = Head.Next[0];
        while (current != Tail)
        {
            yield return current.Value;
            current = current.Next[0];
        }
    }
}