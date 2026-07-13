namespace Royale.Editor.Playtest;

public interface IPlaytestProcess : IDisposable
{
    event Action<string, bool>? OutputReceived;

    event Action<int>? Exited;

    bool HasExited { get; }

    void Start();

    void KillTree();
}

public interface IPlaytestProcessFactory
{
    IPlaytestProcess Create(PlaytestProcessStartInfo startInfo);
}
