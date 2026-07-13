namespace Royale.Editor.Workspace;
public sealed class EditorLog(int capacity = 200)
{
    private readonly Queue<string> entries = new();
    private readonly object sync = new();

    public IReadOnlyList<string> Entries
    {
        get
        {
            lock (sync)
                return entries.ToArray();
        }
    }

    public void Add(string message)
    {
        lock (sync)
        {
            entries.Enqueue(message);
            while (entries.Count > capacity)
                entries.Dequeue();
        }
    }
}
