using System.Numerics;
using BlurgText;

namespace Royale.Client.Rendering;

public static class TextQuadBatchBuilder
{
    public static TextQuadBatch Create(IReadOnlyList<TextQuadSource> sources, Vector2 origin)
    {
        ArgumentNullException.ThrowIfNull(sources);

        if (sources.Count == 0)
            return TextQuadBatch.Empty;

        var vertices = new List<TextVertex>(sources.Count * 4);
        var indices = new List<ushort>(sources.Count * 6);
        var commands = new List<TextDrawCommand>();

        IntPtr currentTexture = IntPtr.Zero;
        int currentCommandFirstIndex = 0;

        foreach (TextQuadSource source in sources)
        {
            if (source.Width <= 0 || source.Height <= 0 || source.TextureUserData == IntPtr.Zero)
                continue;

            if (vertices.Count > ushort.MaxValue - 4)
                throw new InvalidOperationException("Text quad batch exceeds 16-bit index capacity.");

            if (currentTexture != source.TextureUserData)
            {
                FinishCommand(commands, currentTexture, currentCommandFirstIndex, indices.Count);
                currentTexture = source.TextureUserData;
                currentCommandFirstIndex = indices.Count;
            }

            AppendQuad(vertices, indices, source, origin);
        }

        FinishCommand(commands, currentTexture, currentCommandFirstIndex, indices.Count);

        return indices.Count == 0
            ? TextQuadBatch.Empty
            : new TextQuadBatch(vertices, indices, commands);
    }

    public static TextQuadBatch CreateForText(
        string? text,
        IReadOnlyList<TextQuadSource> sources,
        Vector2 origin)
    {
        if (string.IsNullOrWhiteSpace(text))
            return TextQuadBatch.Empty;

        return Create(sources, origin);
    }

    private static void AppendQuad(
        List<TextVertex> vertices,
        List<ushort> indices,
        TextQuadSource source,
        Vector2 origin)
    {
        ushort baseVertex = checked((ushort)vertices.Count);
        Vector4 color = ToVector4(source.Color);

        float x0 = MathF.Round(origin.X + source.X);
        float y0 = MathF.Round(origin.Y + source.Y);
        float x1 = MathF.Round(x0 + source.Width);
        float y1 = MathF.Round(y0 + source.Height);

        vertices.Add(new TextVertex(new Vector2(x0, y0), new Vector2(source.U0, source.V0), color));
        vertices.Add(new TextVertex(new Vector2(x1, y0), new Vector2(source.U1, source.V0), color));
        vertices.Add(new TextVertex(new Vector2(x0, y1), new Vector2(source.U0, source.V1), color));
        vertices.Add(new TextVertex(new Vector2(x1, y1), new Vector2(source.U1, source.V1), color));

        indices.Add(baseVertex);
        indices.Add((ushort)(baseVertex + 1));
        indices.Add((ushort)(baseVertex + 2));
        indices.Add((ushort)(baseVertex + 2));
        indices.Add((ushort)(baseVertex + 1));
        indices.Add((ushort)(baseVertex + 3));
    }

    private static void FinishCommand(
        List<TextDrawCommand> commands,
        IntPtr texture,
        int firstIndex,
        int currentIndexCount)
    {
        int indexCount = currentIndexCount - firstIndex;
        if (texture == IntPtr.Zero || indexCount == 0)
            return;

        commands.Add(new TextDrawCommand(texture, checked((uint)firstIndex), checked((uint)indexCount)));
    }

    private static Vector4 ToVector4(BlurgColor color) =>
        new(
            color.R / 255.0f,
            color.G / 255.0f,
            color.B / 255.0f,
            color.A / 255.0f);
}
