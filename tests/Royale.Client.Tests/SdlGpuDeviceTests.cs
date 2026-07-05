using Royale.Client.Platform;
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
}
