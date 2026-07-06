using System.Numerics;
using BlurgText;

namespace Royale.Client.Rendering;

public static class TextQuadBatchBuilder
{
    public static TextQuadBatch Create(IReadOnlyList<TextQuadSource> sources, Vector2 origin)
    {
        ArgumentNullException.ThrowIfNull(sources);
        return CreateCombined(sources, origin, []);
    }

    public static TextQuadBatch CreateProjected(IReadOnlyList<TextProjectedQuadSource> sources)
    {
        ArgumentNullException.ThrowIfNull(sources);
        return CreateCombined([], Vector2.Zero, sources);
    }

    public static TextQuadBatch CreateCombined(
        IReadOnlyList<TextQuadSource> screenSources,
        Vector2 screenOrigin,
        IReadOnlyList<TextProjectedQuadSource> projectedSources)
    {
        ArgumentNullException.ThrowIfNull(screenSources);
        ArgumentNullException.ThrowIfNull(projectedSources);

        if (screenSources.Count == 0 && projectedSources.Count == 0)
            return TextQuadBatch.Empty;

        var vertices = new List<TextVertex>((screenSources.Count + projectedSources.Count) * 4);
        var indices = new List<ushort>((screenSources.Count + projectedSources.Count) * 6);
        var commands = new List<TextDrawCommand>();

        IntPtr currentTexture = IntPtr.Zero;
        int currentCommandFirstIndex = 0;

        foreach (TextQuadSource source in screenSources)
        {
            if (source.Width <= 0 || source.Height <= 0 || source.TextureUserData == IntPtr.Zero)
                continue;

            if (currentTexture != source.TextureUserData)
            {
                FinishCommand(commands, currentTexture, currentCommandFirstIndex, indices.Count);
                currentTexture = source.TextureUserData;
                currentCommandFirstIndex = indices.Count;
            }

            AppendQuad(vertices, indices, source, screenOrigin);
        }

        foreach (TextProjectedQuadSource source in projectedSources)
        {
            if (source.TextureUserData == IntPtr.Zero)
                continue;

            if (currentTexture != source.TextureUserData)
            {
                FinishCommand(commands, currentTexture, currentCommandFirstIndex, indices.Count);
                currentTexture = source.TextureUserData;
                currentCommandFirstIndex = indices.Count;
            }

            AppendProjectedQuad(vertices, indices, source);
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
        EnsureCanAppendQuad(vertices);

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

        AppendQuadIndices(vertices, indices, baseVertex);
    }

    private static void AppendProjectedQuad(
        List<TextVertex> vertices,
        List<ushort> indices,
        TextProjectedQuadSource source)
    {
        EnsureCanAppendQuad(vertices);

        ushort baseVertex = checked((ushort)vertices.Count);
        Vector4 color = ToVector4(source.Color);

        vertices.Add(new TextVertex(source.TopLeft, new Vector2(source.U0, source.V0), color));
        vertices.Add(new TextVertex(source.TopRight, new Vector2(source.U1, source.V0), color));
        vertices.Add(new TextVertex(source.BottomLeft, new Vector2(source.U0, source.V1), color));
        vertices.Add(new TextVertex(source.BottomRight, new Vector2(source.U1, source.V1), color));

        AppendQuadIndices(vertices, indices, baseVertex);
    }

    private static void AppendQuadIndices(List<TextVertex> vertices, List<ushort> indices, ushort baseVertex)
    {
        indices.Add(baseVertex);
        indices.Add((ushort)(baseVertex + 1));
        indices.Add((ushort)(baseVertex + 2));
        indices.Add((ushort)(baseVertex + 2));
        indices.Add((ushort)(baseVertex + 1));
        indices.Add((ushort)(baseVertex + 3));
    }

    private static void EnsureCanAppendQuad(List<TextVertex> vertices)
    {
        if (vertices.Count > ushort.MaxValue - 4)
            throw new InvalidOperationException("Text quad batch exceeds 16-bit index capacity.");
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
