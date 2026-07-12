using System.Numerics;
using Royale.Content.Maps;
using Royale.Editor.Viewport;
using Royale.Rendering.Cameras;
namespace Royale.Editor.Tests.Viewport;
public sealed class ViewportAndCameraTests
{
    [Theory][InlineData(100,50,2,2,200,100)][InlineData(0,0,2,2,1,1)][InlineData(-5,10,1,1,1,10)] public void ConvertsLogicalSize(float w,float h,float sx,float sy,int ew,int eh) => Assert.Equal(new ViewportPixelSize(ew,eh), ViewportPixelSize.FromLogical(w,h,sx,sy));
    [Fact] public void CaptureRequiresHoveredRightMouseAndEscapeReleases() { var c=new EditorCameraController(); c.UpdateCapture(false,true,false); Assert.False(c.Captured); c.UpdateCapture(true,true,false); Assert.True(c.Captured); c.UpdateCapture(true,true,true); Assert.False(c.Captured); }
    [Fact] public void MovementOnlyAppliesWhileCaptured() { var c=new EditorCameraController(); Vector3 before=c.Camera.Position; var input=new DebugCameraInput(true,false,false,false,false,false,0,0,false); c.Move(input,1); Assert.Equal(before,c.Camera.Position); c.UpdateCapture(true,true,false); c.Move(input,1); Assert.NotEqual(before,c.Camera.Position); }
    [Fact] public void FramesMapBoundsAndDerivesFarPlane() { var c=new EditorCameraController(); c.Frame(new MapBounds{Min=new MapVector3(-100,-5,-100),Max=new MapVector3(100,20,100)}); Assert.True(c.FarPlane>100); Assert.True(c.ToRenderCamera().FarPlane>100); }
}
