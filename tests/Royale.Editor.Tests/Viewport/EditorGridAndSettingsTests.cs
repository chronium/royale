using Royale.Content.Maps;
using Royale.Editor.Viewport;

namespace Royale.Editor.Tests.Viewport;

public sealed class EditorGridAndSettingsTests
{
    [Fact]
    public void GridRoundsBoundsEmphasizesAxesAndMajorLines()
    {
        EditorGrid grid = EditorGridGenerator.Generate(new MapBounds
        {
            Min = new MapVector3(-2.2f, -1, -3.4f),
            Max = new MapVector3(12.1f, 5, 11.2f),
        }, 1.0f);

        Assert.Equal(1.0f, grid.VisualSpacing);
        Assert.Contains(grid.Lines, line => line.Kind == EditorGridLineKind.AxisX);
        Assert.Contains(grid.Lines, line => line.Kind == EditorGridLineKind.AxisZ);
        Assert.Contains(grid.Lines, line => line.Kind == EditorGridLineKind.Major);
        Assert.True(grid.Lines.Min(line => MathF.Min(line.Start.X, line.End.X)) <= -3.0f);
        Assert.True(grid.Lines.Max(line => MathF.Max(line.Start.X, line.End.X)) >= 13.0f);
    }

    [Fact]
    public void DenseGridStaysWithinBudgetWithoutChangingConfiguredSnapIncrement()
    {
        EditorGrid grid = EditorGridGenerator.Generate(new MapBounds
        {
            Min = new MapVector3(-1000, 0, -1000),
            Max = new MapVector3(1000, 0, 1000),
        }, 0.01f);
        var settings = new EditorTransformSettings { GridSpacing = 0.01f };

        Assert.True(grid.Lines.Count <= EditorGridGenerator.MaximumLineCount);
        Assert.True(grid.VisualSpacing > settings.GridSpacing);
        Assert.Equal(0.01f, settings.ActiveSnapIncrement);
    }

    [Fact]
    public void SettingsRoundTripAtomicallyWithAllTransformPreferences()
    {
        string directory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"royale-editor-settings-{Guid.NewGuid():N}");
        string path = System.IO.Path.Combine(directory, "editor-settings.json");
        var store = new EditorTransformSettingsStore(path);
        var expected = new EditorTransformSettings
        {
            GridVisible = false,
            SnappingEnabled = false,
            Operation = EditorTransformOperation.Rotate,
            Orientation = EditorTransformOrientation.Local,
            GridSpacing = 2.5f,
            RotationIncrementDegrees = 30,
            ScaleIncrement = 0.25f,
        };

        store.Write(expected);

        Assert.Equal(expected, store.Read());
        Assert.Empty(Directory.GetFiles(directory, "*.tmp", SearchOption.TopDirectoryOnly));
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("{\"version\":99}")]
    [InlineData("{\"version\":1,\"gridSpacing\":0}")]
    public void MalformedUnsupportedAndInvalidSettingsFallBackToDefaults(string json)
    {
        string path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"royale-editor-settings-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, json);

        Assert.Equal(new EditorTransformSettings(), new EditorTransformSettingsStore(path).Read());
    }

    [Fact]
    public void DefaultsMatchAuthoringContract()
    {
        var settings = new EditorTransformSettings();
        Assert.True(settings.GridVisible);
        Assert.True(settings.SnappingEnabled);
        Assert.Equal(1, settings.GridSpacing);
        Assert.Equal(15, settings.RotationIncrementDegrees);
        Assert.Equal(0.1f, settings.ScaleIncrement);
    }

    [Fact]
    public void SnapIncrementsAreResolvedForTheEffectiveOperation()
    {
        var settings = new EditorTransformSettings
        {
            Operation = EditorTransformOperation.Scale,
            GridSpacing = 2.0f,
            ScaleIncrement = 0.25f,
        };

        Assert.Equal(0.25f, settings.ActiveSnapIncrement);
        Assert.Equal(2.0f, settings.GetSnapIncrement(EditorTransformOperation.Translate));
    }
}
