using System.Text.Json;

namespace Royale.Editor.Projects.Assets;

internal static class ProjectAssetTransaction
{
    internal const string JournalName = ".asset-transaction.json";

    internal static void Commit(RoyaleProjectPaths paths, string stagingRoot)
    {
        string journalPath = Path.Combine(paths.Root, JournalName);
        var entries = new[]
        {
            CreateEntry(stagingRoot, "assets", paths.Sources),
            CreateEntry(stagingRoot, "client", paths.GeneratedClient),
            CreateEntry(stagingRoot, "server", paths.GeneratedServer),
        };
        var journal = new Journal(stagingRoot, entries, 0);
        Write(journalPath, journal);
        try
        {
            for (int index = 0; index < entries.Length; index++)
            {
                Swap(entries[index]);
                journal = journal with { CompletedSwaps = index + 1 };
                Write(journalPath, journal);
            }
            foreach (Entry entry in entries)
                DeleteDirectory(entry.Backup);
            File.Delete(journalPath);
            DeleteDirectory(stagingRoot);
        }
        catch
        {
            Rollback(journal);
            throw;
        }
    }

    internal static void Recover(RoyaleProjectPaths paths)
    {
        string journalPath = Path.Combine(paths.Root, JournalName);
        if (!File.Exists(journalPath))
            return;
        Journal journal = JsonSerializer.Deserialize<Journal>(File.ReadAllBytes(journalPath))
            ?? throw new InvalidDataException("Asset transaction journal is invalid.");
        Rollback(journal);
    }

    internal static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (string directory in Directory.EnumerateDirectories(source, "*", SearchOption.AllDirectories))
            Directory.CreateDirectory(Path.Combine(destination, Path.GetRelativePath(source, directory)));
        foreach (string file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            string target = Path.Combine(destination, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target);
        }
    }

    private static Entry CreateEntry(string stagingRoot, string name, string destination) =>
        new(Path.Combine(stagingRoot, name), destination, Path.Combine(stagingRoot, $"{name}.backup"));

    private static void Swap(Entry entry)
    {
        if (!Directory.Exists(entry.Staged))
            throw new DirectoryNotFoundException(entry.Staged);
        if (Directory.Exists(entry.Destination))
            Directory.Move(entry.Destination, entry.Backup);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(entry.Destination)!);
            Directory.Move(entry.Staged, entry.Destination);
        }
        catch
        {
            if (Directory.Exists(entry.Backup) && !Directory.Exists(entry.Destination))
                Directory.Move(entry.Backup, entry.Destination);
            throw;
        }
    }

    private static void Rollback(Journal journal)
    {
        for (int index = journal.Entries.Length - 1; index >= 0; index--)
        {
            Entry entry = journal.Entries[index];
            if (Directory.Exists(entry.Backup))
            {
                DeleteDirectory(entry.Destination);
                Directory.Move(entry.Backup, entry.Destination);
            }
            else if (index < journal.CompletedSwaps)
            {
                DeleteDirectory(entry.Destination);
            }
        }
        string journalPath = Path.Combine(Path.GetDirectoryName(journal.StagingRoot)!, JournalName);
        if (File.Exists(journalPath))
            File.Delete(journalPath);
        DeleteDirectory(journal.StagingRoot);
    }

    private static void Write(string path, Journal journal) => File.WriteAllBytes(path, JsonSerializer.SerializeToUtf8Bytes(journal));
    private static void DeleteDirectory(string path) { if (Directory.Exists(path)) Directory.Delete(path, true); }

    private sealed record Entry(string Staged, string Destination, string Backup);
    private sealed record Journal(string StagingRoot, Entry[] Entries, int CompletedSwaps);
}
