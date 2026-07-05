using System.Reflection;
using System.Runtime.InteropServices;
using Evergine.Bindings.Imgui;

namespace Royale.Client.Platform;

internal static class ImGuiNativeLibrary
{
    private static bool configured;

    public static void ConfigureResolvers()
    {
        if (configured)
            return;

        NativeLibrary.SetDllImportResolver(typeof(ImguiNative).Assembly, Resolve);
        NativeLibrary.SetDllImportResolver(Assembly.GetExecutingAssembly(), Resolve);
        configured = true;
    }

    private static IntPtr Resolve(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
    {
        if (libraryName is not ("cimgui" or "royale_imgui"))
            return IntPtr.Zero;

        string path = Path.Combine(AppContext.BaseDirectory, NativeLibraryFileName);
        return File.Exists(path)
            ? NativeLibrary.Load(path)
            : IntPtr.Zero;
    }

    private static string NativeLibraryFileName
    {
        get
        {
            if (OperatingSystem.IsMacOS())
                return "libroyale_imgui.dylib";

            if (OperatingSystem.IsLinux())
                return "libroyale_imgui.so";

            if (OperatingSystem.IsWindows())
                return "royale_imgui.dll";

            return "libroyale_imgui";
        }
    }
}
