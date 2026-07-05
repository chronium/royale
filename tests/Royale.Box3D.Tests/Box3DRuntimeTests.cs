using Royale.Box3D;
using Royale.Box3D.Bindings;

namespace Royale.Box3D.Tests;

public sealed class Box3DRuntimeTests
{
    [Fact]
    public void WrapperExposesBindingLibraryName()
    {
        Assert.Equal(Box3DBindingSurface.NativeLibraryName, Box3DRuntime.NativeLibraryName);
    }
}
