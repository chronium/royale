using System.Diagnostics;

namespace Royale.Rendering.Tests.Platform;

public sealed class SdlGpuIntegrationTests
{
    [Fact]
    public async Task StandaloneHarnessRendersAndResizesOffscreenTargetWhenEnabled()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("ROYALE_GPU_TESTS"), "1", StringComparison.Ordinal))
            return;

        string harnessPath = Path.Combine(AppContext.BaseDirectory, "gpu-harness", "Royale.Rendering.GpuHarness.dll");
        Assert.True(File.Exists(harnessPath), $"GPU harness was not packaged at '{harnessPath}'.");
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                ArgumentList = { harnessPath },
                WorkingDirectory = Path.GetDirectoryName(harnessPath)!,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            },
        };

        Assert.True(process.Start(), "GPU harness process did not start.");
        Task<string> stdoutTask = process.StandardOutput.ReadToEndAsync();
        Task<string> stderrTask = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync();
            string timedOutStdout = await stdoutTask;
            string timedOutStderr = await stderrTask;
            Assert.Fail($"GPU harness exceeded 30 seconds.\nstdout:\n{timedOutStdout}\nstderr:\n{timedOutStderr}");
        }

        string stdout = await stdoutTask;
        string stderr = await stderrTask;
        Assert.True(process.ExitCode == 0, $"GPU harness exited with code {process.ExitCode}.\nstdout:\n{stdout}\nstderr:\n{stderr}");
        Assert.Contains("GPU_HARNESS_SUCCESS", stdout, StringComparison.Ordinal);
    }
}
