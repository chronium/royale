namespace Royale.Editor.Workspace;
public sealed class EditorLog(int capacity = 200)
{
    private readonly Queue<string> entries = new();
    public IReadOnlyList<string> Entries => entries.ToArray();
    public void Add(string message) { entries.Enqueue(message); while (entries.Count > capacity) entries.Dequeue(); }
}
