using Royale.Client.Platform;
using Royale.Client.Rendering;
using Royale.Client.Rendering.Cameras;
using Royale.Client.Rendering.Debug;
using Royale.Client.Rendering.Meshes;
using Royale.Client.Rendering.Screenshots;
using Royale.Client.Rendering.Shaders;
using Royale.Client.Rendering.Text;
using SDL;

namespace Royale.Client.Tests;

public sealed class SdlGpuDeviceTests
{
    [Fact]
    public void RequestedShaderFormatsIncludeSupportedTargets()
    {
        Assert.True(SdlGpuDevice.RequestedShaderFormats.HasFlag(SDL_GPUShaderFormat.SDL_GPU_SHADERFORMAT_SPIRV));
        Assert.True(SdlGpuDevice.RequestedShaderFormats.HasFlag(SDL_GPUShaderFormat.SDL_GPU_SHADERFORMAT_MSL));
        Assert.True(SdlGpuDevice.RequestedShaderFormats.HasFlag(SDL_GPUShaderFormat.SDL_GPU_SHADERFORMAT_DXIL));
    }

    [Fact]
    public void SelectPreferredShaderFormatUsesStableOrder()
    {
        SDL_GPUShaderFormat supported =
            SDL_GPUShaderFormat.SDL_GPU_SHADERFORMAT_SPIRV |
            SDL_GPUShaderFormat.SDL_GPU_SHADERFORMAT_MSL |
            SDL_GPUShaderFormat.SDL_GPU_SHADERFORMAT_DXIL;

        Assert.Equal(SDL_GPUShaderFormat.SDL_GPU_SHADERFORMAT_MSL, SdlGpuDevice.SelectPreferredShaderFormat(supported));
    }

    [Fact]
    public void SelectPreferredShaderFormatFallsBackToSpirvBeforeDxil()
    {
        SDL_GPUShaderFormat supported =
            SDL_GPUShaderFormat.SDL_GPU_SHADERFORMAT_SPIRV |
            SDL_GPUShaderFormat.SDL_GPU_SHADERFORMAT_DXIL;

        Assert.Equal(SDL_GPUShaderFormat.SDL_GPU_SHADERFORMAT_SPIRV, SdlGpuDevice.SelectPreferredShaderFormat(supported));
    }

    [Fact]
    public void SelectPreferredShaderFormatReturnsNullWhenNoKnownFormatIsSupported()
    {
        Assert.Null(SdlGpuDevice.SelectPreferredShaderFormat(0));
    }

    [Theory]
    [InlineData(SDL_GPUShaderFormat.SDL_GPU_SHADERFORMAT_MSL, "basic.vert.msl")]
    [InlineData(SDL_GPUShaderFormat.SDL_GPU_SHADERFORMAT_SPIRV, "basic.vert.spv")]
    [InlineData(SDL_GPUShaderFormat.SDL_GPU_SHADERFORMAT_DXIL, "basic.vert.hlsl")]
    public void ShaderAssetSelectorUsesCompiledShaderExtension(SDL_GPUShaderFormat format, string expectedFileName)
    {
        Assert.Equal(expectedFileName, ShaderAssetSelector.GetShaderFileName("basic.vert", format));
    }

    [Theory]
    [InlineData(SDL_GPUShaderFormat.SDL_GPU_SHADERFORMAT_MSL, "main0")]
    [InlineData(SDL_GPUShaderFormat.SDL_GPU_SHADERFORMAT_SPIRV, "main")]
    [InlineData(SDL_GPUShaderFormat.SDL_GPU_SHADERFORMAT_DXIL, "main")]
    public void ShaderAssetSelectorUsesBackendEntrypoint(SDL_GPUShaderFormat format, string expectedEntrypoint)
    {
        Assert.Equal(expectedEntrypoint, ShaderAssetSelector.GetEntrypoint(format));
    }
}
