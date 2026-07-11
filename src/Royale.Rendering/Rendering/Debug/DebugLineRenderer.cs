using System.Numerics;
using Royale.Rendering.Cameras;
using Royale.Rendering.Shaders;
using SDL;
using static SDL.SDL3;

namespace Royale.Rendering.Debug;

internal sealed unsafe class DebugLineRenderer : IDisposable
{
    private readonly SDL_GPUDevice* device;
    private readonly SDL_GPUShader* vertexShader;
    private readonly SDL_GPUShader* fragmentShader;
    private readonly SDL_GPUGraphicsPipeline* pipeline;
    private SDL_GPUBuffer* vertexBuffer;
    private uint vertexCapacity;
    private uint uploadedVertexCount;

    public DebugLineRenderer(
        SDL_GPUDevice* device,
        SDL_GPUTextureFormat swapchainFormat,
        SDL_GPUShaderFormat shaderFormat,
        SDL_GPUTextureFormat depthFormat)
    {
        this.device = device;
        vertexShader = LoadShader("debug_line.vert", shaderFormat, SDL_GPUShaderStage.SDL_GPU_SHADERSTAGE_VERTEX, uniformBufferCount: 1);
        fragmentShader = LoadShader("debug_line.frag", shaderFormat, SDL_GPUShaderStage.SDL_GPU_SHADERSTAGE_FRAGMENT, uniformBufferCount: 0);
        pipeline = CreatePipeline(swapchainFormat, depthFormat);
    }

    public uint UploadedLineCount => uploadedVertexCount / 2;

    public void Upload(SDL_GPUCommandBuffer* commandBuffer, DebugPrimitiveList primitives)
    {
        ArgumentNullException.ThrowIfNull(primitives);

        DebugLineVertex[] vertices = primitives.ToVertices();
        uploadedVertexCount = (uint)vertices.Length;

        if (vertices.Length == 0)
            return;

        EnsureVertexBuffer(uploadedVertexCount);

        uint vertexBytes = checked((uint)(vertices.Length * DebugLineVertex.Stride));
        var transferCreateInfo = new SDL_GPUTransferBufferCreateInfo
        {
            usage = SDL_GPUTransferBufferUsage.SDL_GPU_TRANSFERBUFFERUSAGE_UPLOAD,
            size = vertexBytes,
        };

        SDL_GPUTransferBuffer* transferBuffer = SDL_CreateGPUTransferBuffer(device, &transferCreateInfo);
        if (transferBuffer is null)
            throw new InvalidOperationException($"SDL GPU debug line transfer buffer creation failed: {SDL_GetError()}");

        try
        {
            IntPtr mapped = SDL_MapGPUTransferBuffer(device, transferBuffer, cycle: false);
            if (mapped == IntPtr.Zero)
                throw new InvalidOperationException($"SDL GPU debug line transfer buffer mapping failed: {SDL_GetError()}");

            fixed (DebugLineVertex* vertexPointer = vertices)
                Buffer.MemoryCopy(vertexPointer, (void*)mapped, vertexBytes, vertexBytes);

            SDL_UnmapGPUTransferBuffer(device, transferBuffer);

            SDL_GPUCopyPass* copyPass = SDL_BeginGPUCopyPass(commandBuffer);
            if (copyPass is null)
                throw new InvalidOperationException($"SDL GPU debug line copy pass creation failed: {SDL_GetError()}");

            var source = new SDL_GPUTransferBufferLocation
            {
                transfer_buffer = transferBuffer,
                offset = 0,
            };
            var destination = new SDL_GPUBufferRegion
            {
                buffer = vertexBuffer,
                offset = 0,
                size = vertexBytes,
            };

            SDL_UploadToGPUBuffer(copyPass, &source, &destination, cycle: true);
            SDL_EndGPUCopyPass(copyPass);
        }
        finally
        {
            SDL_ReleaseGPUTransferBuffer(device, transferBuffer);
        }
    }

    public void Render(
        SDL_GPUCommandBuffer* commandBuffer,
        SDL_GPURenderPass* renderPass,
        uint width,
        uint height,
        RenderCamera camera)
    {
        if (uploadedVertexCount == 0)
            return;

        Matrix4x4 viewProjection = Matrix4x4.Transpose(camera.CreateViewMatrix() * camera.CreateProjectionMatrix(width, height));
        var vertexBinding = new SDL_GPUBufferBinding
        {
            buffer = vertexBuffer,
            offset = 0,
        };

        SDL_BindGPUGraphicsPipeline(renderPass, pipeline);
        SDL_BindGPUVertexBuffers(renderPass, 0, &vertexBinding, 1);
        SDL_PushGPUVertexUniformData(commandBuffer, 0, (IntPtr)(&viewProjection), (uint)sizeof(Matrix4x4));
        SDL_DrawGPUPrimitives(renderPass, uploadedVertexCount, 1, 0, 0);
    }

    public void Dispose()
    {
        if (vertexBuffer is not null)
            SDL_ReleaseGPUBuffer(device, vertexBuffer);

        if (pipeline is not null)
            SDL_ReleaseGPUGraphicsPipeline(device, pipeline);

        if (fragmentShader is not null)
            SDL_ReleaseGPUShader(device, fragmentShader);

        if (vertexShader is not null)
            SDL_ReleaseGPUShader(device, vertexShader);
    }

    private void EnsureVertexBuffer(uint vertexCount)
    {
        if (vertexBuffer is not null && vertexCapacity >= vertexCount)
            return;

        if (vertexBuffer is not null)
            SDL_ReleaseGPUBuffer(device, vertexBuffer);

        vertexCapacity = Math.Max(128u, NextPowerOfTwo(vertexCount));
        var createInfo = new SDL_GPUBufferCreateInfo
        {
            usage = SDL_GPUBufferUsageFlags.SDL_GPU_BUFFERUSAGE_VERTEX,
            size = checked(vertexCapacity * (uint)DebugLineVertex.Stride),
        };

        vertexBuffer = SDL_CreateGPUBuffer(device, &createInfo);
        if (vertexBuffer is null)
            throw new InvalidOperationException($"SDL GPU debug line vertex buffer creation failed: {SDL_GetError()}");
    }

    private SDL_GPUGraphicsPipeline* CreatePipeline(SDL_GPUTextureFormat swapchainFormat, SDL_GPUTextureFormat depthFormat)
    {
        var vertexBufferDescription = new SDL_GPUVertexBufferDescription
        {
            slot = 0,
            pitch = (uint)DebugLineVertex.Stride,
            input_rate = SDL_GPUVertexInputRate.SDL_GPU_VERTEXINPUTRATE_VERTEX,
        };

        SDL_GPUVertexAttribute* vertexAttributes = stackalloc SDL_GPUVertexAttribute[2];
        vertexAttributes[0] = new SDL_GPUVertexAttribute
        {
            location = 0,
            buffer_slot = 0,
            format = SDL_GPUVertexElementFormat.SDL_GPU_VERTEXELEMENTFORMAT_FLOAT3,
            offset = DebugLineVertex.PositionOffset,
        };
        vertexAttributes[1] = new SDL_GPUVertexAttribute
        {
            location = 1,
            buffer_slot = 0,
            format = SDL_GPUVertexElementFormat.SDL_GPU_VERTEXELEMENTFORMAT_FLOAT4,
            offset = (uint)DebugLineVertex.ColorOffset,
        };

        var colorTargetDescription = new SDL_GPUColorTargetDescription
        {
            format = swapchainFormat,
        };

        var createInfo = new SDL_GPUGraphicsPipelineCreateInfo
        {
            vertex_shader = vertexShader,
            fragment_shader = fragmentShader,
            vertex_input_state = new SDL_GPUVertexInputState
            {
                vertex_buffer_descriptions = &vertexBufferDescription,
                num_vertex_buffers = 1,
                vertex_attributes = vertexAttributes,
                num_vertex_attributes = 2,
            },
            primitive_type = SDL_GPUPrimitiveType.SDL_GPU_PRIMITIVETYPE_LINELIST,
            rasterizer_state = new SDL_GPURasterizerState
            {
                fill_mode = SDL_GPUFillMode.SDL_GPU_FILLMODE_FILL,
                cull_mode = SDL_GPUCullMode.SDL_GPU_CULLMODE_NONE,
                front_face = SDL_GPUFrontFace.SDL_GPU_FRONTFACE_COUNTER_CLOCKWISE,
            },
            multisample_state = new SDL_GPUMultisampleState
            {
                sample_count = SDL_GPUSampleCount.SDL_GPU_SAMPLECOUNT_1,
            },
            depth_stencil_state = new SDL_GPUDepthStencilState
            {
                enable_depth_test = false,
                enable_depth_write = false,
            },
            target_info = new SDL_GPUGraphicsPipelineTargetInfo
            {
                color_target_descriptions = &colorTargetDescription,
                num_color_targets = 1,
                depth_stencil_format = depthFormat,
                has_depth_stencil_target = true,
            },
        };

        SDL_GPUGraphicsPipeline* createdPipeline = SDL_CreateGPUGraphicsPipeline(device, &createInfo);
        if (createdPipeline is null)
            throw new InvalidOperationException($"SDL GPU debug line graphics pipeline creation failed: {SDL_GetError()}");

        return createdPipeline;
    }

    private SDL_GPUShader* LoadShader(string shaderName, SDL_GPUShaderFormat shaderFormat, SDL_GPUShaderStage shaderStage, uint uniformBufferCount)
    {
        string path = Path.Combine(AppContext.BaseDirectory, "shaders", ShaderAssetSelector.GetShaderFileName(shaderName, shaderFormat));

        if (!File.Exists(path))
            throw new FileNotFoundException($"SDL GPU debug shader asset was not found: {path}", path);

        byte[] code = File.ReadAllBytes(path);
        string entrypointName = ShaderAssetSelector.GetEntrypoint(shaderFormat);
        byte[] entrypointBytes = System.Text.Encoding.UTF8.GetBytes(entrypointName + '\0');

        fixed (byte* codePointer = code)
        fixed (byte* entrypoint = entrypointBytes)
        {
            var createInfo = new SDL_GPUShaderCreateInfo
            {
                code_size = (nuint)code.Length,
                code = codePointer,
                entrypoint = entrypoint,
                format = shaderFormat,
                stage = shaderStage,
                num_uniform_buffers = uniformBufferCount,
            };

            SDL_GPUShader* shader = SDL_CreateGPUShader(device, &createInfo);

            if (shader is null)
                throw new InvalidOperationException($"SDL GPU debug shader creation failed for {path}: {SDL_GetError()}");

            return shader;
        }
    }

    private static uint NextPowerOfTwo(uint value)
    {
        value--;
        value |= value >> 1;
        value |= value >> 2;
        value |= value >> 4;
        value |= value >> 8;
        value |= value >> 16;
        return value + 1;
    }
}
