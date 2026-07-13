using System.Numerics;
using Royale.Content.Maps;

namespace Royale.Editor.Viewport;

public enum EditorGridLineKind
{
    Minor,
    Major,
    AxisX,
    AxisZ,
}

public readonly record struct EditorGridLine(Vector3 Start, Vector3 End, EditorGridLineKind Kind);

public sealed record EditorGrid(IReadOnlyList<EditorGridLine> Lines, float VisualSpacing);

public static class EditorGridGenerator
{
    public const int MaximumLineCount = 402;

    public static EditorGrid Generate(MapBounds bounds, float configuredSpacing)
    {
        if (!float.IsFinite(configuredSpacing) || configuredSpacing <= 0.0f)
            throw new ArgumentOutOfRangeException(nameof(configuredSpacing));

        float minimumX = MathF.Floor(bounds.Min.X / configuredSpacing) * configuredSpacing;
        float maximumX = MathF.Ceiling(bounds.Max.X / configuredSpacing) * configuredSpacing;
        float minimumZ = MathF.Floor(bounds.Min.Z / configuredSpacing) * configuredSpacing;
        float maximumZ = MathF.Ceiling(bounds.Max.Z / configuredSpacing) * configuredSpacing;
        int exactXCount = Math.Max(1, (int)MathF.Round((maximumX - minimumX) / configuredSpacing) + 1);
        int exactZCount = Math.Max(1, (int)MathF.Round((maximumZ - minimumZ) / configuredSpacing) + 1);
        int subdivision = Math.Max(1, (int)MathF.Ceiling((exactXCount + exactZCount) / (float)MaximumLineCount));
        float visualSpacing;
        int visualXCount;
        int visualZCount;
        do
        {
            visualSpacing = configuredSpacing * subdivision++;
            minimumX = MathF.Floor(bounds.Min.X / visualSpacing) * visualSpacing;
            maximumX = MathF.Ceiling(bounds.Max.X / visualSpacing) * visualSpacing;
            minimumZ = MathF.Floor(bounds.Min.Z / visualSpacing) * visualSpacing;
            maximumZ = MathF.Ceiling(bounds.Max.Z / visualSpacing) * visualSpacing;
            visualXCount = Math.Max(1, (int)MathF.Round((maximumX - minimumX) / visualSpacing) + 1);
            visualZCount = Math.Max(1, (int)MathF.Round((maximumZ - minimumZ) / visualSpacing) + 1);
        }
        while (visualXCount + visualZCount > MaximumLineCount);

        var lines = new List<EditorGridLine>();
        AddLines(minimumX, maximumX, visualSpacing, x => new EditorGridLine(
            new Vector3(x, 0.0f, minimumZ),
            new Vector3(x, 0.0f, maximumZ),
            NearlyZero(x) ? EditorGridLineKind.AxisZ : IsMajor(x, configuredSpacing) ? EditorGridLineKind.Major : EditorGridLineKind.Minor));
        AddLines(minimumZ, maximumZ, visualSpacing, z => new EditorGridLine(
            new Vector3(minimumX, 0.0f, z),
            new Vector3(maximumX, 0.0f, z),
            NearlyZero(z) ? EditorGridLineKind.AxisX : IsMajor(z, configuredSpacing) ? EditorGridLineKind.Major : EditorGridLineKind.Minor));

        return new EditorGrid(lines, visualSpacing);

        void AddLines(float minimum, float maximum, float spacing, Func<float, EditorGridLine> create)
        {
            int count = Math.Max(1, (int)MathF.Round((maximum - minimum) / spacing) + 1);
            for (int index = 0; index < count; index++)
                lines.Add(create(index == count - 1 ? maximum : minimum + index * spacing));
        }
    }

    private static bool IsMajor(float coordinate, float configuredSpacing) =>
        MathF.Abs(coordinate / configuredSpacing % 10.0f) < 0.001f;

    private static bool NearlyZero(float value) => MathF.Abs(value) < 0.0001f;
}
