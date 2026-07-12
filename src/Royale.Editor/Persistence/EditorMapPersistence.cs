using System.Security.Cryptography;
using Royale.Content.Maps;
using Royale.Editor.Documents;

namespace Royale.Editor.Persistence;

public static class EditorMapPersistence
{
    public static EditorMapDocument Load(string path, bool requiresSaveAs = false)
    {
        string fullPath = Path.GetFullPath(path);
        GameMap map = MapCatalog.LoadFile(fullPath);
        return new(map, fullPath, Fingerprint(fullPath), requiresSaveAs);
    }

    public static string? ValidateDestination(EditorMapDocument document, string path)
    {
        if (!string.Equals(Path.GetExtension(path), ".json", StringComparison.OrdinalIgnoreCase)) return "Map filename must use the .json extension.";
        if (!string.Equals(Path.GetFileNameWithoutExtension(path), document.Map.Id, StringComparison.Ordinal)) return $"Map filename must be '{document.Map.Id}.json'.";
        return null;
    }

    public static void Save(EditorMapDocument document, string path, bool checkExternalChange)
    {
        ArgumentNullException.ThrowIfNull(document);
        string fullPath = Path.GetFullPath(path);
        string? destinationError = ValidateDestination(document, fullPath);
        if (destinationError is not null) throw new InvalidOperationException(destinationError);
        MapCatalog.Validate(document.Map, fullPath);
        if (checkExternalChange && File.Exists(fullPath))
        {
            if (document.SourceFingerprint is null || !string.Equals(Fingerprint(fullPath), document.SourceFingerprint, StringComparison.Ordinal))
                throw new IOException($"Map file '{fullPath}' changed externally; save was cancelled.");
        }

        string directory = Path.GetDirectoryName(fullPath) ?? throw new InvalidOperationException("Destination has no directory.");
        Directory.CreateDirectory(directory);
        string temporary = Path.Combine(directory, $".{Path.GetFileName(fullPath)}.{Guid.NewGuid():N}.tmp");
        try
        {
            byte[] bytes = MapFileSerializer.Serialize(document.Map);
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
            {
                stream.Write(bytes);
                stream.Flush(true);
            }
            MapCatalog.LoadFile(temporary, document.Map.Id);
            File.Move(temporary, fullPath, true);
            document.MarkSaved(fullPath, Fingerprint(fullPath));
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    public static string Fingerprint(string path)
    {
        using FileStream stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }
}
