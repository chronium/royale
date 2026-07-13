using System.Diagnostics;

namespace Royale.Editor.Playtest;

public sealed class SystemPlaytestProcessFactory : IPlaytestProcessFactory
{
    public IPlaytestProcess Create(PlaytestProcessStartInfo startInfo) =>
        new SystemPlaytestProcess(startInfo);
}

internal sealed class SystemPlaytestProcess : IPlaytestProcess
{
    private readonly Process process;

    public SystemPlaytestProcess(PlaytestProcessStartInfo startInfo)
    {
        var processStartInfo = new ProcessStartInfo
        {
            FileName = startInfo.FileName,
            WorkingDirectory = startInfo.WorkingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        foreach (string argument in startInfo.Arguments)
            processStartInfo.ArgumentList.Add(argument);

        process = new Process
        {
            StartInfo = processStartInfo,
            EnableRaisingEvents = true,
        };
        process.OutputDataReceived += (_, eventArgs) =>
        {
            if (eventArgs.Data is not null)
                OutputReceived?.Invoke(eventArgs.Data, false);
        };
        process.ErrorDataReceived += (_, eventArgs) =>
        {
            if (eventArgs.Data is not null)
                OutputReceived?.Invoke(eventArgs.Data, true);
        };
        process.Exited += (_, _) => Exited?.Invoke(process.ExitCode);
    }

    public event Action<string, bool>? OutputReceived;

    public event Action<int>? Exited;

    public bool HasExited => process.HasExited;

    public void Start()
    {
        if (!process.Start())
            throw new InvalidOperationException($"Could not start process '{process.StartInfo.FileName}'.");
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
    }

    public void KillTree()
    {
        if (!process.HasExited)
            process.Kill(entireProcessTree: true);
    }

    public void Dispose() => process.Dispose();
}
