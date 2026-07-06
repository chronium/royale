using Royale.Client.Rendering.Shaders;
using SDL;
using static SDL.SDL3;

namespace Royale.Client.Rendering.Text;

internal sealed unsafe class TextQuadRenderer : IDisposable
{
    private readonly SDL_GPUDevice* device;
    private readonly SDL_GPUShader* vertexShader;
    private readonly SDL_GPUShader* fragmentShader;
    private readonly SDL_GPUGraphicsPipeline* pipeline;
    private readonly SDL_GPUSampler* sampler;
    private SDL_GPUBuffer* vertexBuffer;
    private SDL_GPUBuffer* indexBuffer;
    private uint vertexCapacity;
    private uint indexCapacity;

    public TextQuadRenderer(
        SDL_GPUDevice* device,
        SDL_GPUTextureFormat swapchainFormat,
        SDL_GPUShaderFormat shaderFormat)
    {
        this.device = device;
        vertexShader = LoadShader("text_sprite.vert", shaderFormat, SDL_GPUShaderStage.SDL_GPU_SHADERSTAGE_VERTEX, uniformBufferCount: 1, samplerCount: 0);
        fragmentShader = LoadShader("text_sprite.frag", shaderFormat, SDL_GPUShaderStage.SDL_GPU_SHADERSTAGE_FRAGMENT, uniformBufferCount: 0, samplerCount: 1);
        sampler = CreateSampler();
        pipeline = CreatePipeline(swapchainFormat);
    }

    public void Upload(SDL_GPUCommandBuffer* commandBuffer, TextQuadBatch batch)
    {
        ArgumentNullException.ThrowIfNull(batch);

        if (batch.IsEmpty)
            return;

        EnsureBuffers(checked((uint)batch.Vertices.Count), checked((uint)batch.Indices.Count));

        TextVertex[] vertices = batch.Vertices.ToArray();
        ushort[] indices = batch.Indices.ToArray();
        uint vertexBytes = checked((uint)(vertices.Length * TextVertex.Stride));
        uint indexBytes = checked((uint)(indices.Length * sizeof(ushort)));
        uint transferBytes = checked(vertexBytes + indexBytes);

        var transferCreateInfo = new SDL_GPUTransferBufferCreateInfo
        {
            usage = SDL_GPUTransferBufferUsage.SDL_GPU_TRANSFERBUFFERUSAGE_UPLOAD,
            size = transferBytes,
        };

        SDL_GPUTransferBuffer* transferBuffer = SDL_CreateGPUTransferBuffer(device, &transferCreateInfo);
        if (transferBuffer is null)
            throw new InvalidOperationException($"SDL GPU text transfer buffer creation failed: {SDL_GetError()}");

        try
        {
            IntPtr mapped = SDL_MapGPUTransferBuffer(device, transferBuffer, cycle: false);
            if (mapped == IntPtr.Zero)
                throw new InvalidOperationException($"SDL GPU text transfer buffer mapping failed: {SDL_GetError()}");

            fixed (TextVertex* vertexPointer = vertices)
            fixed (ushort* indexPointer = indices)
            {
                Buffer.MemoryCopy(vertexPointer, (void*)mapped, transferBytes, vertexBytes);
                Buffer.MemoryCopy(indexPointer, (byte*)mapped + vertexBytes, transferBytes - vertexBytes, indexBytes);
            }

            SDL_UnmapGPUTransferBuffer(device, transferBuffer);

            SDL_GPUCopyPass* copyPass = SDL_BeginGPUCopyPass(commandBuffer);
            if (copyPass is null)
                throw new InvalidOperationException($"SDL GPU text copy pass creation failed: {SDL_GetError()}");

            var vertexSource = new SDL_GPUTransferBufferLocation
            {
                transfer_buffer = transferBuffer,
                offset = 0,
            };
            var vertexDestination = new SDL_GPUBufferRegion
            {
                buffer = vertexBuffer,
                offset = 0,
                size = vertexBytes,
            };
            SDL_UploadToGPUBuffer(copyPass, &vertexSource, &vertexDestination, cycle: true);

            var indexSource = new SDL_GPUTransferBufferLocation
            {
                transfer_buffer = transferBuffer,
                offset = vertexBytes,
            };
            var indexDestination = new SDL_GPUBufferRegion
            {
                buffer = indexBuffer,
                offset = 0,
                size = indexBytes,
            };
            SDL_UploadToGPUBuffer(copyPass, &indexSource, &indexDestination, cycle: true);

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
        TextQuadBatch batch)
    {
        if (batch.IsEmpty)
            return;

        var vertexBinding = new SDL_GPUBufferBinding
        {
            buffer = vertexBuffer,
            offset = 0,
        };
        var indexBinding = new SDL_GPUBufferBinding
        {
            buffer = indexBuffer,
            offset = 0,
        };
        var constants = new TextScreenConstants(width, height);

        SDL_BindGPUGraphicsPipeline(renderPass, pipeline);
        SDL_BindGPUVertexBuffers(renderPass, 0, &vertexBinding, 1);
        SDL_BindGPUIndexBuffer(renderPass, &indexBinding, SDL_GPUIndexElementSize.SDL_GPU_INDEXELEMENTSIZE_16BIT);
        SDL_PushGPUVertexUniformData(commandBuffer, 0, (IntPtr)(&constants), (uint)sizeof(TextScreenConstants));

        foreach (TextDrawCommand command in batch.DrawCommands)
        {
            var textureBinding = new SDL_GPUTextureSamplerBinding
            {
                texture = (SDL_GPUTexture*)command.TextureUserData,
                sampler = sampler,
            };
            SDL_BindGPUFragmentSamplers(renderPass, 0, &textureBinding, 1);
            SDL_DrawGPUIndexedPrimitives(renderPass, command.IndexCount, 1, command.FirstIndex, 0, 0);
        }
    }

    public void Dispose()
    {
        if (indexBuffer is not null)
            SDL_ReleaseGPUBuffer(device, indexBuffer);

        if (vertexBuffer is not null)
            SDL_ReleaseGPUBuffer(device, vertexBuffer);

        if (pipeline is not null)
            SDL_ReleaseGPUGraphicsPipeline(device, pipeline);

        if (sampler is not null)
            SDL_ReleaseGPUSampler(device, sampler);

        if (fragmentShader is not null)
            SDL_ReleaseGPUShader(device, fragmentShader);

        if (vertexShader is not null)
            SDL_ReleaseGPUShader(device, vertexShader);
    }

    private void EnsureBuffers(uint vertexCount, uint indexCount)
    {
        if (vertexBuffer is null || vertexCapacity < vertexCount)
        {
            if (vertexBuffer is not null)
                SDL_ReleaseGPUBuffer(device, vertexBuffer);

            vertexCapacity = Math.Max(128u, NextPowerOfTwo(vertexCount));
            vertexBuffer = CreateBuffer(SDL_GPUBufferUsageFlags.SDL_GPU_BUFFERUSAGE_VERTEX, checked(vertexCapacity * (uint)TextVertex.Stride));
        }

        if (indexBuffer is null || indexCapacity < indexCount)
        {
            if (indexBuffer is not null)
                SDL_ReleaseGPUBuffer(device, indexBuffer);

            indexCapacity = Math.Max(192u, NextPowerOfTwo(indexCount));
            indexBuffer = CreateBuffer(SDL_GPUBufferUsageFlags.SDL_GPU_BUFFERUSAGE_INDEX, checked(indexCapacity * sizeof(ushort)));
        }
    }

    private SDL_GPUBuffer* CreateBuffer(SDL_GPUBufferUsageFlags usage, uint size)
    {
        var createInfo = new SDL_GPUBufferCreateInfo
        {
            usage = usage,
            size = size,
        };

        SDL_GPUBuffer* buffer = SDL_CreateGPUBuffer(device, &createInfo);
        if (buffer is null)
            throw new InvalidOperationException($"SDL GPU text buffer creation failed: {SDL_GetError()}");

        return buffer;
    }

    private SDL_GPUSampler* CreateSampler()
    {
        var createInfo = new SDL_GPUSamplerCreateInfo
        {
            min_filter = SDL_GPUFilter.SDL_GPU_FILTER_LINEAR,
            mag_filter = SDL_GPUFilter.SDL_GPU_FILTER_LINEAR,
            mipmap_mode = SDL_GPUSamplerMipmapMode.SDL_GPU_SAMPLERMIPMAPMODE_NEAREST,
            address_mode_u = SDL_GPUSamplerAddressMode.SDL_GPU_SAMPLERADDRESSMODE_CLAMP_TO_EDGE,
            address_mode_v = SDL_GPUSamplerAddressMode.SDL_GPU_SAMPLERADDRESSMODE_CLAMP_TO_EDGE,
            address_mode_w = SDL_GPUSamplerAddressMode.SDL_GPU_SAMPLERADDRESSMODE_CLAMP_TO_EDGE,
        };

        SDL_GPUSampler* createdSampler = SDL_CreateGPUSampler(device, &createInfo);
        if (createdSampler is null)
            throw new InvalidOperationException($"SDL GPU text sampler creation failed: {SDL_GetError()}");

        return createdSampler;
    }

    private SDL_GPUGraphicsPipeline* CreatePipeline(SDL_GPUTextureFormat swapchainFormat)
    {
        var vertexBufferDescription = new SDL_GPUVertexBufferDescription
        {
            slot = 0,
            pitch = (uint)TextVertex.Stride,
            input_rate = SDL_GPUVertexInputRate.SDL_GPU_VERTEXINPUTRATE_VERTEX,
        };

        SDL_GPUVertexAttribute* vertexAttributes = stackalloc SDL_GPUVertexAttribute[3];
        vertexAttributes[0] = new SDL_GPUVertexAttribute
        {
            location = 0,
            buffer_slot = 0,
            format = SDL_GPUVertexElementFormat.SDL_GPU_VERTEXELEMENTFORMAT_FLOAT2,
            offset = TextVertex.PositionOffset,
        };
        vertexAttributes[1] = new SDL_GPUVertexAttribute
        {
            location = 1,
            buffer_slot = 0,
            format = SDL_GPUVertexElementFormat.SDL_GPU_VERTEXELEMENTFORMAT_FLOAT2,
            offset = (uint)TextVertex.TexCoordOffset,
        };
        vertexAttributes[2] = new SDL_GPUVertexAttribute
        {
            location = 2,
            buffer_slot = 0,
            format = SDL_GPUVertexElementFormat.SDL_GPU_VERTEXELEMENTFORMAT_FLOAT4,
            offset = (uint)TextVertex.ColorOffset,
        };

        var colorTargetDescription = new SDL_GPUColorTargetDescription
        {
            format = swapchainFormat,
            blend_state = new SDL_GPUColorTargetBlendState
            {
                src_color_blendfactor = SDL_GPUBlendFactor.SDL_GPU_BLENDFACTOR_SRC_ALPHA,
                dst_color_blendfactor = SDL_GPUBlendFactor.SDL_GPU_BLENDFACTOR_ONE_MINUS_SRC_ALPHA,
                color_blend_op = SDL_GPUBlendOp.SDL_GPU_BLENDOP_ADD,
                src_alpha_blendfactor = SDL_GPUBlendFactor.SDL_GPU_BLENDFACTOR_ONE,
                dst_alpha_blendfactor = SDL_GPUBlendFactor.SDL_GPU_BLENDFACTOR_ONE_MINUS_SRC_ALPHA,
                alpha_blend_op = SDL_GPUBlendOp.SDL_GPU_BLENDOP_ADD,
                enable_blend = true,
            },
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
                num_vertex_attributes = 3,
            },
            primitive_type = SDL_GPUPrimitiveType.SDL_GPU_PRIMITIVETYPE_TRIANGLELIST,
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
                has_depth_stencil_target = false,
            },
        };

        SDL_GPUGraphicsPipeline* createdPipeline = SDL_CreateGPUGraphicsPipeline(device, &createInfo);
        if (createdPipeline is null)
            throw new InvalidOperationException($"SDL GPU text graphics pipeline creation failed: {SDL_GetError()}");

        return createdPipeline;
    }

    private SDL_GPUShader* LoadShader(
        string shaderName,
        SDL_GPUShaderFormat shaderFormat,
        SDL_GPUShaderStage shaderStage,
        uint uniformBufferCount,
        uint samplerCount)
    {
        string path = Path.Combine(AppContext.BaseDirectory, "shaders", ShaderAssetSelector.GetShaderFileName(shaderName, shaderFormat));

        if (!File.Exists(path))
            throw new FileNotFoundException($"SDL GPU text shader asset was not found: {path}", path);

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
                num_samplers = samplerCount,
            };

            SDL_GPUShader* shader = SDL_CreateGPUShader(device, &createInfo);
            if (shader is null)
                throw new InvalidOperationException($"SDL GPU text shader creation failed for {path}: {SDL_GetError()}");

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
        value++;
        return value;
    }
}
