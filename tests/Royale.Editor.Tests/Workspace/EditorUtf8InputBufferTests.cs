using System.Text;
using Royale.Content.Maps;
using Royale.Editor.Documents;
using Royale.Editor.Workspace;

namespace Royale.Editor.Tests.Workspace;

public sealed class EditorUtf8InputBufferTests
{
    [Fact]
    public void StoresShortValueAndNullTerminatesIt()
    {
        var input = new EditorUtf8InputBuffer(256);

        input.SetValue("Arena");

        Assert.Equal("Arena", input.Value);
        Assert.False(input.WasTruncated);
        Assert.Equal(255, input.PayloadCapacity);
        Assert.Equal(0, input.Buffer[5]);
    }

    [Fact]
    public void StoresExactPayloadBoundary()
    {
        var input = new EditorUtf8InputBuffer(256);
        string value = new('a', input.PayloadCapacity);

        input.SetValue(value);

        Assert.Equal(value, input.Value);
        Assert.False(input.WasTruncated);
        Assert.Equal(0, input.Buffer[^1]);
    }

    [Fact]
    public void TruncatesLongAsciiAtPayloadBoundary()
    {
        var input = new EditorUtf8InputBuffer(256);

        input.SetValue(new string('a', 300));

        Assert.Equal(new string('a', 255), input.Value);
        Assert.True(input.WasTruncated);
        AssertValidBoundedUtf8(input);
    }

    [Fact]
    public void TruncatesWithoutSplittingMultibyteSequence()
    {
        var input = new EditorUtf8InputBuffer(256);
        string value = new string('a', 254) + "€";

        input.SetValue(value);

        Assert.Equal(new string('a', 254), input.Value);
        Assert.DoesNotContain('�', input.Value);
        Assert.True(input.WasTruncated);
        AssertValidBoundedUtf8(input);
    }

    [Fact]
    public void ReplacingValueClearsStaleBytes()
    {
        var input = new EditorUtf8InputBuffer(16);
        input.SetValue("longer value");

        input.SetValue("short");

        Assert.Equal("short", input.Value);
        Assert.All(input.Buffer.Skip(5), value => Assert.Equal(0, value));
    }

    [Fact]
    public void SynchronizingLongNameDoesNotMutateDocument()
    {
        string name = string.Concat(Enumerable.Repeat("地図", 100));
        var document = new EditorMapDocument(new GameMap { Id = "map", Name = name }, null, null, false);
        var input = new EditorUtf8InputBuffer(256);

        input.SetValue(document.Map.Name);

        Assert.True(input.WasTruncated);
        Assert.Equal(name, document.Map.Name);
        Assert.False(document.IsDirty);
        Assert.NotEqual(document.Map.Name, input.Value);
        AssertValidBoundedUtf8(input);
    }

    private static void AssertValidBoundedUtf8(EditorUtf8InputBuffer input)
    {
        byte[] payload = Encoding.UTF8.GetBytes(input.Value);
        Assert.True(payload.Length <= input.PayloadCapacity);
        Assert.Equal(0, input.Buffer[payload.Length]);
        Assert.Equal(input.Value, new UTF8Encoding(false, true).GetString(payload));
    }
}
