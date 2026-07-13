using System.Text;

namespace Royale.Editor.Workspace;

public sealed class EditorUtf8InputBuffer
{
    private readonly byte[] buffer;

    public EditorUtf8InputBuffer(int capacity)
    {
        if (capacity < 1)
            throw new ArgumentOutOfRangeException(nameof(capacity), "Capacity must include space for a null terminator.");

        buffer = new byte[capacity];
    }

    public byte[] Buffer => buffer;
    public int Capacity => buffer.Length;
    public int PayloadCapacity => Capacity - 1;
    public bool WasTruncated { get; private set; }

    public string Value
    {
        get
        {
            int length = Array.IndexOf(buffer, (byte)0, 0, PayloadCapacity);
            if (length < 0)
                length = PayloadCapacity;

            return Encoding.UTF8.GetString(buffer, 0, length);
        }
    }

    public void SetValue(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        Array.Clear(buffer);

        Encoder encoder = Encoding.UTF8.GetEncoder();
        encoder.Convert(
            value.AsSpan(),
            buffer.AsSpan(0, PayloadCapacity),
            true,
            out _,
            out int bytesUsed,
            out bool completed);

        buffer[bytesUsed] = 0;
        WasTruncated = !completed;
    }
}
