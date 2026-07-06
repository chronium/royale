using Royale.Native;

namespace Royale.Native.Tests;

public sealed class NativeLibraryResolverTests
{
    [Theory]
    [InlineData("SDL3", "libSDL3.dylib")]
    [InlineData("box3d", "libbox3d.dylib")]
    [InlineData("cimgui", "libroyale_imgui.dylib")]
    [InlineData("royale_imgui", "libroyale_imgui.dylib")]
    [InlineData("libblurgtext", "libblurgtext.dylib")]
    public void MacOSArm64MappingsUseBundledRuntimeNativeLayout(string importName, string fileName)
    {
        string path = NativeLibraryResolver.GetExpectedPath(importName, NativeLibraryResolver.MacOSArm64Rid);

        Assert.EndsWith(Path.Combine("runtimes", "osx-arm64", "native", fileName), path);
    }

    [Fact]
    public void LinuxX64Box3DMappingUsesBundledRuntimeNativeLayout()
    {
        string path = NativeLibraryResolver.GetExpectedPath("box3d", NativeLibraryResolver.LinuxX64Rid);

        Assert.EndsWith(Path.Combine("runtimes", "linux-x64", "native", "libbox3d.so"), path);
    }

    [Fact]
    public void UnsupportedRidThrows()
    {
        Assert.Throws<DllNotFoundException>(() =>
            NativeLibraryResolver.GetExpectedPath("box3d", "win-x64"));
    }

    [Fact]
    public void UnsupportedImportThrows()
    {
        Assert.Throws<DllNotFoundException>(() =>
            NativeLibraryResolver.GetExpectedPath("SDL3", NativeLibraryResolver.LinuxX64Rid));
    }
}
