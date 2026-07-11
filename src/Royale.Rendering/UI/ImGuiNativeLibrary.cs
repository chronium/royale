using System.Reflection;
using Evergine.Bindings.Imgui;
using Royale.Native;

namespace Royale.Rendering.UI;

internal static class ImGuiNativeLibrary
{
    private static bool configured;

    public static void ConfigureResolvers()
    {
        if (configured)
            return;

        NativeLibraryResolver.ConfigureForAssembly(typeof(ImguiNative).Assembly);
        NativeLibraryResolver.ConfigureForAssembly(Assembly.GetExecutingAssembly());
        configured = true;
    }
}
