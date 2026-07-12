using System.Text;
using Royale.Content.Maps;
using Royale.Editor.Documents;
using Royale.Editor.Persistence;

namespace Royale.Editor.Tests.Persistence;

public sealed class EditorMapPersistenceTests
{
    private static string SourceMap => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "Royale.Content", "Maps", "graybox.json"));

    [Fact]
    public void SaveIsCanonicalReloadableAndDoesNotSerializeEditorIds()
    {
        string directory = TempDirectory(); string path = Path.Combine(directory, "graybox.json");
        EditorMapDocument document = EditorMapPersistence.Load(SourceMap); document.Execute(new SetMapNameCommand(document.Map.Name, "Edited")); EditorMapPersistence.Save(document, path, false);
        byte[] bytes = File.ReadAllBytes(path); string json = Encoding.UTF8.GetString(bytes);
        Assert.False(bytes.Take(3).SequenceEqual(new byte[] { 0xEF, 0xBB, 0xBF })); Assert.EndsWith("\n", json); Assert.DoesNotContain("\r", json); Assert.Contains("\"id\"", json); Assert.DoesNotContain("editorId", json, StringComparison.OrdinalIgnoreCase); Assert.False(document.IsDirty);
        Assert.Equal("Edited", MapCatalog.LoadFile(path).Name); Directory.Delete(directory, true);
    }

    [Fact]
    public void DetectsExternalChangesAndCleansTemporaryFiles()
    {
        string directory = TempDirectory(); string path = Path.Combine(directory, "graybox.json"); File.Copy(SourceMap, path); EditorMapDocument document = EditorMapPersistence.Load(path); document.Execute(new SetMapNameCommand(document.Map.Name, "Edited")); File.AppendAllText(path, " ");
        Assert.Throws<IOException>(() => EditorMapPersistence.Save(document, path, true)); Assert.True(document.IsDirty); Assert.Empty(Directory.GetFiles(directory, "*.tmp")); Directory.Delete(directory, true);
    }

    [Theory][InlineData("wrong.json")][InlineData("graybox.txt")]
    public void EnforcesSaveAsFilename(string filename) { EditorMapDocument document = EditorMapPersistence.Load(SourceMap); Assert.NotNull(EditorMapPersistence.ValidateDestination(document, filename)); }

    private static string TempDirectory() { string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")); Directory.CreateDirectory(path); return path; }
}
