using System.Buffers.Binary;
using System.Text.Json;

namespace Royale.Content.Models;

public static class GlbExternalResourceInspector
{
    public static IReadOnlyList<string> Inspect(string path)
    {
        byte[] bytes = File.ReadAllBytes(path);
        if (bytes.Length < 20
            || BinaryPrimitives.ReadUInt32LittleEndian(bytes) != 0x46546C67
            || BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(4)) != 2)
            throw new InvalidDataException($"'{path}' is not a GLB 2.0 file.");
        uint declaredLength = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(8));
        uint jsonLength = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(12));
        if (declaredLength != bytes.Length
            || BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(16)) != 0x4E4F534A
            || jsonLength > bytes.Length - 20)
            throw new InvalidDataException($"'{path}' has an invalid GLB container.");

        try
        {
            using JsonDocument document = JsonDocument.Parse(bytes.AsMemory(20, checked((int)jsonLength)));
            var resources = new HashSet<string>(StringComparer.Ordinal);
            Add("buffers");
            Add("images");
            return resources.Order(StringComparer.Ordinal).ToList();

            void Add(string property)
            {
                if (!document.RootElement.TryGetProperty(property, out JsonElement values)) return;
                foreach (JsonElement value in values.EnumerateArray())
                {
                    if (!value.TryGetProperty("uri", out JsonElement uriElement)) continue;
                    string uri = uriElement.GetString() ?? throw new InvalidDataException($"GLB {property} URI must be a string.");
                    if (uri.StartsWith("data:", StringComparison.OrdinalIgnoreCase)) continue;
                    string decoded = Uri.UnescapeDataString(uri).Replace('\\', '/');
                    if (Path.IsPathRooted(decoded) || decoded.Split('/').Any(segment => segment.Length == 0 || segment is "." or ".."))
                        throw new InvalidDataException($"GLB resource URI '{uri}' is not a contained portable path.");
                    resources.Add(decoded);
                }
            }
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException($"'{path}' has an invalid GLB JSON chunk.", exception);
        }
    }
}
