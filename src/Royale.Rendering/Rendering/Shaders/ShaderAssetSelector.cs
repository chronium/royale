using SDL;

namespace Royale.Rendering.Shaders;

public static class ShaderAssetSelector
{
    public static string GetShaderFileName(string shaderName, SDL_GPUShaderFormat format)
    {
        string extension = format switch
        {
            SDL_GPUShaderFormat.SDL_GPU_SHADERFORMAT_MSL => ".msl",
            SDL_GPUShaderFormat.SDL_GPU_SHADERFORMAT_SPIRV => ".spv",
            SDL_GPUShaderFormat.SDL_GPU_SHADERFORMAT_DXIL => ".hlsl",
            _ => throw new NotSupportedException($"Unsupported SDL GPU shader format: {format}"),
        };

        return shaderName + extension;
    }

    public static string GetEntrypoint(SDL_GPUShaderFormat format)
    {
        return format switch
        {
            SDL_GPUShaderFormat.SDL_GPU_SHADERFORMAT_MSL => "main0",
            SDL_GPUShaderFormat.SDL_GPU_SHADERFORMAT_SPIRV => "main",
            SDL_GPUShaderFormat.SDL_GPU_SHADERFORMAT_DXIL => "main",
            _ => throw new NotSupportedException($"Unsupported SDL GPU shader format: {format}"),
        };
    }
}
