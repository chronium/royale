using System.Numerics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Royale.Editor.Viewport;

public enum EditorTransformOperation
{
    Translate,
    Rotate,
    Scale,
}

public enum EditorTransformOrientation
{
    Local,
    World,
}

public sealed record EditorTransformSettings
{
    public const int CurrentVersion = 1;
    public const float MinimumGridSpacing = 0.01f;
    public const float MaximumGridSpacing = 100.0f;
    public const float MinimumRotationIncrement = 1.0f;
    public const float MaximumRotationIncrement = 180.0f;
    public const float MinimumScaleIncrement = 0.01f;
    public const float MaximumScaleIncrement = 10.0f;

    public int Version { get; init; } = CurrentVersion;
    public bool GridVisible { get; init; } = true;
    public bool SnappingEnabled { get; init; } = true;
    public EditorTransformOperation Operation { get; init; } = EditorTransformOperation.Translate;
    public EditorTransformOrientation Orientation { get; init; } = EditorTransformOrientation.World;
    public float GridSpacing { get; init; } = 1.0f;
    public float RotationIncrementDegrees { get; init; } = 15.0f;
    public float ScaleIncrement { get; init; } = 0.1f;

    public bool IsValid =>
        Version == CurrentVersion &&
        Enum.IsDefined(Operation) &&
        Enum.IsDefined(Orientation) &&
        IsInRange(GridSpacing, MinimumGridSpacing, MaximumGridSpacing) &&
        IsInRange(RotationIncrementDegrees, MinimumRotationIncrement, MaximumRotationIncrement) &&
        IsInRange(ScaleIncrement, MinimumScaleIncrement, MaximumScaleIncrement);

    public float ActiveSnapIncrement => GetSnapIncrement(Operation);

    public float GetSnapIncrement(EditorTransformOperation operation) => operation switch
    {
        EditorTransformOperation.Translate => GridSpacing,
        EditorTransformOperation.Rotate => RotationIncrementDegrees,
        EditorTransformOperation.Scale => ScaleIncrement,
        _ => GridSpacing,
    };

    public Vector3 GetSnapVector(EditorTransformOperation operation) =>
        new(GetSnapIncrement(operation));

    private static bool IsInRange(float value, float minimum, float maximum) =>
        float.IsFinite(value) && value >= minimum && value <= maximum;
}

public sealed class EditorTransformSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    private readonly string path;

    public EditorTransformSettingsStore(string? path = null)
    {
        this.path = path ?? ResolveDefaultPath();
    }

    public string Path => path;

    public EditorTransformSettings Read()
    {
        try
        {
            if (!File.Exists(path))
                return new EditorTransformSettings();
            using FileStream stream = File.OpenRead(path);
            EditorTransformSettings? settings = JsonSerializer.Deserialize<EditorTransformSettings>(stream, JsonOptions);
            return settings?.IsValid == true ? settings : new EditorTransformSettings();
        }
        catch (JsonException)
        {
            return new EditorTransformSettings();
        }
        catch (IOException)
        {
            return new EditorTransformSettings();
        }
        catch (UnauthorizedAccessException)
        {
            return new EditorTransformSettings();
        }
    }

    public void Write(EditorTransformSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (!settings.IsValid)
            throw new ArgumentException("Editor transform settings contain unsupported or invalid values.", nameof(settings));

        string directory = System.IO.Path.GetDirectoryName(path)!;
        Directory.CreateDirectory(directory);
        string temporary = System.IO.Path.Combine(directory, $".{System.IO.Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
        try
        {
            string json = JsonSerializer.Serialize(settings, JsonOptions).Replace("\r\n", "\n", StringComparison.Ordinal) + "\n";
            File.WriteAllText(temporary, json, new UTF8Encoding(false));
            File.Move(temporary, path, true);
        }
        finally
        {
            if (File.Exists(temporary))
                File.Delete(temporary);
        }
    }

    public static string ResolveDefaultPath() => System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Royale",
        "Editor",
        "editor-settings.json");
}
