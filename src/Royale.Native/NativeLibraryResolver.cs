using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Royale.Native;

public static class NativeLibraryResolver
{
    public const string MacOSArm64Rid = "osx-arm64";
    public const string LinuxX64Rid = "linux-x64";

    private static readonly ConcurrentDictionary<Assembly, bool> ConfiguredAssemblies = new();

    public static string CurrentRuntimeIdentifier
    {
        get
        {
            if (OperatingSystem.IsMacOS() && RuntimeInformation.ProcessArchitecture == Architecture.Arm64)
                return MacOSArm64Rid;

            return RuntimeInformation.RuntimeIdentifier;
        }
    }

    public static string GetExpectedPath(string importName)
    {
        return GetExpectedPath(importName, CurrentRuntimeIdentifier);
    }

    public static string GetExpectedPath(string importName, string runtimeIdentifier)
    {
        string rid = runtimeIdentifier;
        string fileName = GetNativeFileName(importName, rid);
        return Path.Combine(AppContext.BaseDirectory, "runtimes", rid, "native", fileName);
    }

    public static void ConfigureForAssembly(Assembly assembly)
    {
        if (!ConfiguredAssemblies.TryAdd(assembly, true))
            return;

        NativeLibrary.SetDllImportResolver(assembly, Resolve);
    }

    private static IntPtr Resolve(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
    {
        if (!TryGetNativeFileName(libraryName, CurrentRuntimeIdentifier, out string? fileName))
            return IntPtr.Zero;

        string rid = CurrentRuntimeIdentifier;
        string path = Path.Combine(AppContext.BaseDirectory, "runtimes", rid, "native", fileName);
        if (!File.Exists(path))
        {
            throw new DllNotFoundException(
                $"Native library '{libraryName}' is not bundled for RID '{rid}'. Expected path: {path}");
        }

        return NativeLibrary.Load(path);
    }

    private static string GetNativeFileName(string importName, string rid)
    {
        if (TryGetNativeFileName(importName, rid, out string? fileName))
            return fileName;

        throw new DllNotFoundException(
            $"Native library '{importName}' has no resolver mapping for RID '{rid}'.");
    }

    private static bool TryGetNativeFileName(string importName, string rid, [NotNullWhen(true)] out string? fileName)
    {
        fileName = (rid, importName) switch
        {
            (MacOSArm64Rid, "SDL3") => "libSDL3.dylib",
            (MacOSArm64Rid, "box3d") => "libbox3d.dylib",
            (MacOSArm64Rid, "cimgui") => "libroyale_imgui.dylib",
            (MacOSArm64Rid, "royale_imgui") => "libroyale_imgui.dylib",
            (LinuxX64Rid, "box3d") => "libbox3d.so",
            _ => null,
        };

        return fileName is not null;
    }
}
