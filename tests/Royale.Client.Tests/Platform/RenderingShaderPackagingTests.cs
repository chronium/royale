namespace Royale.Client.Tests.Platform;

public sealed class RenderingShaderPackagingTests
{
    [Theory]
    [InlineData("basic.vert.spv")]
    [InlineData("basic.vert.msl")]
    [InlineData("basic.vert.hlsl")]
    [InlineData("debug_line.frag.spv")]
    [InlineData("text_sprite.frag.msl")]
    public void RenderingShaderAssetsReachClientOutput(string fileName)
    {
        Assert.True(File.Exists(Path.Combine(AppContext.BaseDirectory, "shaders", fileName)));
    }
}
