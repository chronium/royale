using Royale.Box3D.Bodies;
using Royale.Box3D.Geometry;
using Royale.Box3D.Runtime;
using Royale.Box3D.Worlds;
using Royale.Box3D.Bindings.Interop;

namespace Royale.Box3D.Tests.Runtime;

public sealed class Box3DRuntimeTests
{
    [Fact]
    public void WrapperExposesBindingLibraryName()
    {
        Assert.Equal(Box3DBindingSurface.NativeLibraryName, Box3DRuntime.NativeLibraryName);
    }
}
