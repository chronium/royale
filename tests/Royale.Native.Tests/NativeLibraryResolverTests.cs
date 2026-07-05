using Royale.Native;

namespace Royale.Native.Tests;

public sealed class NativeLibraryResolverTests
{
    [Theory]
    [InlineData("SDL3", "libSDL3.dylib")]
    [InlineData("box3d", "libbox3d.dylib")]
    [InlineData("cimgui", "libroyale_imgui.dylib")]
    [InlineData("royale_imgui", "libroyale_imgui.dylib")]
    public void MacOSArm64MappingsUseBundledRuntimeNativeLayout(string importName, string fileName)
    {
        if (NativeLibraryResolver.CurrentRuntimeIdentifier != NativeLibraryResolver.MacOSArm64Rid)
            return;

        string path = NativeLibraryResolver.GetExpectedPath(importName);

        Assert.EndsWith(Path.Combine("runtimes", "osx-arm64", "native", fileName), path);
    }
}
