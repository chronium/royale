namespace Royale.Client.Rendering;

public sealed class TextQuadBatch
{
    public static readonly TextQuadBatch Empty = new([], [], []);

    public TextQuadBatch(
        IReadOnlyList<TextVertex> vertices,
        IReadOnlyList<ushort> indices,
        IReadOnlyList<TextDrawCommand> drawCommands)
    {
        Vertices = vertices;
        Indices = indices;
        DrawCommands = drawCommands;
    }

    public IReadOnlyList<TextVertex> Vertices { get; }

    public IReadOnlyList<ushort> Indices { get; }

    public IReadOnlyList<TextDrawCommand> DrawCommands { get; }

    public bool IsEmpty => Indices.Count == 0;
}
