using System.Numerics;
using Royale.Rendering.Cameras;
using Royale.Rendering.Meshes;

namespace Royale.Rendering.Tests.Rendering.Meshes;

public sealed class ModelContactSheetFramingTests
{
    [Fact]
    public void DefinesAllAxisAndDiagonalViewsInStableSheetOrder()
    {
        Assert.Equal(["+X", "+Y", "+Z", "-X", "-Y", "-Z"],
            ModelContactSheetFraming.AxisViews.Select(view => view.Label));
        Assert.Equal(
            [
                "+X +Y +Z", "-X +Y +Z", "-X +Y -Z", "+X +Y -Z",
                "+X -Y +Z", "-X -Y +Z", "-X -Y -Z", "+X -Y -Z",
            ],
            ModelContactSheetFraming.DiagonalViews.Select(view => view.Label));
        Assert.Equal(14, ModelContactSheetFraming.AxisViews.Count + ModelContactSheetFraming.DiagonalViews.Count);
        Assert.All(ModelContactSheetFraming.AxisViews.Concat(ModelContactSheetFraming.DiagonalViews), view =>
            Assert.InRange(view.CameraFromDirection.Length(), 0.9999f, 1.0001f));
    }

    [Fact]
    public void TopAndBottomViewsUseNonDegenerateExplicitUpDirections()
    {
        ModelContactSheetView top = ModelContactSheetFraming.AxisViews.Single(view => view.Label == "+Y");
        ModelContactSheetView bottom = ModelContactSheetFraming.AxisViews.Single(view => view.Label == "-Y");

        Assert.Equal(-Vector3.UnitZ, top.UpDirection);
        Assert.Equal(Vector3.UnitZ, bottom.UpDirection);
        Assert.Equal(0.0f, Vector3.Dot(top.CameraFromDirection, top.UpDirection), 5);
        Assert.Equal(0.0f, Vector3.Dot(bottom.CameraFromDirection, bottom.UpDirection), 5);
        Assert.True(Matrix4x4.Invert(ModelContactSheetFraming.CreateCamera(Bounds, top).CreateViewMatrix(), out _));
        Assert.True(Matrix4x4.Invert(ModelContactSheetFraming.CreateCamera(Bounds, bottom).CreateViewMatrix(), out _));
    }

    [Fact]
    public void EveryViewUsesOneOrthographicScaleAndContainsBoundsInClipSpace()
    {
        float expectedSize = ModelContactSheetFraming.CalculateOrthographicSize(Bounds);
        foreach (ModelContactSheetView view in ModelContactSheetFraming.AxisViews.Concat(ModelContactSheetFraming.DiagonalViews))
        {
            RenderCamera camera = ModelContactSheetFraming.CreateCamera(Bounds, view);
            Assert.Equal(RenderProjectionMode.Orthographic, camera.ProjectionMode);
            Assert.Equal(expectedSize, camera.OrthographicVerticalSize, 5);
            Matrix4x4 viewProjection = camera.CreateViewMatrix() * camera.CreateProjectionMatrix(384, 384);
            foreach (Vector3 corner in Corners(Bounds))
            {
                Vector4 clip = Vector4.Transform(new Vector4(corner, 1.0f), viewProjection);
                Assert.InRange(clip.X / clip.W, -1.0001f, 1.0001f);
                Assert.InRange(clip.Y / clip.W, -1.0001f, 1.0001f);
                Assert.InRange(clip.Z / clip.W, -0.0001f, 1.0001f);
            }
        }
    }

    [Fact]
    public void OrthographicProjectionUsesAspectAndDegenerateBoundsRemainFinite()
    {
        RenderCamera camera = ModelContactSheetFraming.CreateCamera(
            new ModelBounds(new Vector3(2.0f), new Vector3(2.0f)),
            ModelContactSheetFraming.AxisViews[0]);
        Matrix4x4 square = camera.CreateProjectionMatrix(384, 384);
        Matrix4x4 wide = camera.CreateProjectionMatrix(768, 384);

        Assert.Equal(square.M22, wide.M22, 5);
        Assert.Equal(square.M11 * 0.5f, wide.M11, 5);
        Assert.True(float.IsFinite(camera.OrthographicVerticalSize));
        Assert.True(camera.OrthographicVerticalSize > 0.0f);
        Assert.True(camera.FarPlane > camera.NearPlane);
    }

    private static ModelBounds Bounds => new(new Vector3(-2.0f, -1.0f, -3.0f), new Vector3(4.0f, 5.0f, 2.0f));

    private static IEnumerable<Vector3> Corners(ModelBounds bounds)
    {
        foreach (float x in new[] { bounds.Minimum.X, bounds.Maximum.X })
        foreach (float y in new[] { bounds.Minimum.Y, bounds.Maximum.Y })
        foreach (float z in new[] { bounds.Minimum.Z, bounds.Maximum.Z })
            yield return new Vector3(x, y, z);
    }
}
