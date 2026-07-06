using System.Numerics;
using SDL;
using static SDL.SDL3;

namespace Royale.Client.Rendering;

internal sealed unsafe class StaticMeshRenderer : IDisposable
{
    private const SDL_GPUTextureFormat DepthFormat = SDL_GPUTextureFormat.SDL_GPU_TEXTUREFORMAT_D32_FLOAT;

    private readonly SDL_GPUDevice* device;
    private readonly SDL_GPUShader* vertexShader;
    private readonly SDL_GPUShader* fragmentShader;
    private readonly SDL_GPUGraphicsPipeline* pipeline;
    private readonly SDL_GPUBuffer* vertexBuffer;
    private readonly SDL_GPUBuffer* indexBuffer;
    private readonly StaticMeshGeometry unitBoxMesh;
    private readonly IReadOnlyList<StaticMeshInstance> previewInstances;
    private SDL_GPUTexture* depthTexture;
    private uint depthTextureWidth;
    private uint depthTextureHeight;

    public StaticMeshRenderer(
        SDL_GPUDevice* device,
        SDL_GPUTextureFormat swapchainFormat,
        SDL_GPUShaderFormat shaderFormat,
        IReadOnlyList<StaticMeshInstance> staticMeshInstances)
        : this(device, swapchainFormat, shaderFormat, UnitBoxMesh.Create(), staticMeshInstances)
    {
    }

    private StaticMeshRenderer(
        SDL_GPUDevice* device,
        SDL_GPUTextureFormat swapchainFormat,
        SDL_GPUShaderFormat shaderFormat,
        StaticMeshGeometry unitBoxMesh,
        IReadOnlyList<StaticMeshInstance> staticMeshInstances)
    {
        this.device = device;
        this.unitBoxMesh = unitBoxMesh;
        previewInstances = staticMeshInstances;

        vertexShader = LoadShader("basic.vert", shaderFormat, SDL_GPUShaderStage.SDL_GPU_SHADERSTAGE_VERTEX, uniformBufferCount: 1);
        fragmentShader = LoadShader("basic.frag", shaderFormat, SDL_GPUShaderStage.SDL_GPU_SHADERSTAGE_FRAGMENT, uniformBufferCount: 0);
        vertexBuffer = CreateBuffer(SDL_GPUBufferUsageFlags.SDL_GPU_BUFFERUSAGE_VERTEX, (uint)(unitBoxMesh.Vertices.Count * sizeof(StaticMeshVertex)));
        indexBuffer = CreateBuffer(SDL_GPUBufferUsageFlags.SDL_GPU_BUFFERUSAGE_INDEX, (uint)(unitBoxMesh.Indices.Count * sizeof(ushort)));
        pipeline = CreatePipeline(swapchainFormat);

        UploadGeometry();
    }

    public void Render(
        SDL_GPUCommandBuffer* commandBuffer,
        SDL_GPURenderPass* renderPass,
        uint width,
        uint height,
        RenderCamera camera)
    {
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

        SDL_BindGPUGraphicsPipeline(renderPass, pipeline);
        SDL_BindGPUVertexBuffers(renderPass, 0, &vertexBinding, 1);
        SDL_BindGPUIndexBuffer(renderPass, &indexBinding, SDL_GPUIndexElementSize.SDL_GPU_INDEXELEMENTSIZE_16BIT);

        foreach (StaticMeshInstance instance in previewInstances)
        {
            Matrix4x4 worldViewProjection = StaticMeshDraw.CreateTransposedWorldViewProjection(instance, camera, width, height);
            SDL_PushGPUVertexUniformData(commandBuffer, 0, (IntPtr)(&worldViewProjection), (uint)sizeof(Matrix4x4));
            SDL_DrawGPUIndexedPrimitives(renderPass, (uint)unitBoxMesh.Indices.Count, 1, 0, 0, 0);
        }
    }

    public SDL_GPUDepthStencilTargetInfo GetDepthTarget(uint width, uint height)
    {
        EnsureDepthTexture(width, height);

        return new SDL_GPUDepthStencilTargetInfo
        {
            texture = depthTexture,
            clear_depth = 1.0f,
            load_op = SDL_GPULoadOp.SDL_GPU_LOADOP_CLEAR,
            store_op = SDL_GPUStoreOp.SDL_GPU_STOREOP_DONT_CARE,
            stencil_load_op = SDL_GPULoadOp.SDL_GPU_LOADOP_DONT_CARE,
            stencil_store_op = SDL_GPUStoreOp.SDL_GPU_STOREOP_DONT_CARE,
        };
    }

    public void Dispose()
    {
        if (depthTexture is not null)
            SDL_ReleaseGPUTexture(device, depthTexture);

        if (pipeline is not null)
            SDL_ReleaseGPUGraphicsPipeline(device, pipeline);

        if (indexBuffer is not null)
            SDL_ReleaseGPUBuffer(device, indexBuffer);

        if (vertexBuffer is not null)
            SDL_ReleaseGPUBuffer(device, vertexBuffer);

        if (fragmentShader is not null)
            SDL_ReleaseGPUShader(device, fragmentShader);

        if (vertexShader is not null)
            SDL_ReleaseGPUShader(device, vertexShader);
    }

    private SDL_GPUShader* LoadShader(string shaderName, SDL_GPUShaderFormat shaderFormat, SDL_GPUShaderStage shaderStage, uint uniformBufferCount)
    {
        string path = Path.Combine(AppContext.BaseDirectory, "shaders", ShaderAssetSelector.GetShaderFileName(shaderName, shaderFormat));

        if (!File.Exists(path))
            throw new FileNotFoundException($"SDL GPU shader asset was not found: {path}", path);

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
                throw new InvalidOperationException($"SDL GPU shader creation failed for {path}: {SDL_GetError()}");

            return shader;
        }
    }

    private SDL_GPUGraphicsPipeline* CreatePipeline(SDL_GPUTextureFormat swapchainFormat)
    {
        var vertexBufferDescription = new SDL_GPUVertexBufferDescription
        {
            slot = 0,
            pitch = (uint)sizeof(StaticMeshVertex),
            input_rate = SDL_GPUVertexInputRate.SDL_GPU_VERTEXINPUTRATE_VERTEX,
        };

        SDL_GPUVertexAttribute* vertexAttributes = stackalloc SDL_GPUVertexAttribute[2];
        vertexAttributes[0] = new SDL_GPUVertexAttribute
        {
            location = 0,
            buffer_slot = 0,
            format = SDL_GPUVertexElementFormat.SDL_GPU_VERTEXELEMENTFORMAT_FLOAT3,
            offset = 0,
        };
        vertexAttributes[1] = new SDL_GPUVertexAttribute
        {
            location = 1,
            buffer_slot = 0,
            format = SDL_GPUVertexElementFormat.SDL_GPU_VERTEXELEMENTFORMAT_FLOAT3,
            offset = (uint)sizeof(Vector3),
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
                compare_op = SDL_GPUCompareOp.SDL_GPU_COMPAREOP_LESS,
                enable_depth_test = true,
                enable_depth_write = true,
            },
            target_info = new SDL_GPUGraphicsPipelineTargetInfo
            {
                color_target_descriptions = &colorTargetDescription,
                num_color_targets = 1,
                depth_stencil_format = DepthFormat,
                has_depth_stencil_target = true,
            },
        };

        SDL_GPUGraphicsPipeline* createdPipeline = SDL_CreateGPUGraphicsPipeline(device, &createInfo);

        if (createdPipeline is null)
            throw new InvalidOperationException($"SDL GPU graphics pipeline creation failed: {SDL_GetError()}");

        return createdPipeline;
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
            throw new InvalidOperationException($"SDL GPU buffer creation failed: {SDL_GetError()}");

        return buffer;
    }

    private void UploadGeometry()
    {
        uint vertexBytes = (uint)(unitBoxMesh.Vertices.Count * sizeof(StaticMeshVertex));
        uint indexBytes = (uint)(unitBoxMesh.Indices.Count * sizeof(ushort));
        uint transferBytes = vertexBytes + indexBytes;

        var transferCreateInfo = new SDL_GPUTransferBufferCreateInfo
        {
            usage = SDL_GPUTransferBufferUsage.SDL_GPU_TRANSFERBUFFERUSAGE_UPLOAD,
            size = transferBytes,
        };

        SDL_GPUTransferBuffer* transferBuffer = SDL_CreateGPUTransferBuffer(device, &transferCreateInfo);

        if (transferBuffer is null)
            throw new InvalidOperationException($"SDL GPU transfer buffer creation failed: {SDL_GetError()}");

        try
        {
            IntPtr mapped = SDL_MapGPUTransferBuffer(device, transferBuffer, cycle: false);

            if (mapped == IntPtr.Zero)
                throw new InvalidOperationException($"SDL GPU transfer buffer mapping failed: {SDL_GetError()}");

            StaticMeshVertex[] vertices = unitBoxMesh.Vertices.ToArray();
            ushort[] indices = unitBoxMesh.Indices.ToArray();

            fixed (StaticMeshVertex* vertexPointer = vertices)
            fixed (ushort* indexPointer = indices)
            {
                Buffer.MemoryCopy(vertexPointer, (void*)mapped, transferBytes, vertexBytes);
                Buffer.MemoryCopy(indexPointer, (byte*)mapped + vertexBytes, transferBytes - vertexBytes, indexBytes);
            }

            SDL_UnmapGPUTransferBuffer(device, transferBuffer);

            SDL_GPUCommandBuffer* commandBuffer = SDL_AcquireGPUCommandBuffer(device);

            if (commandBuffer is null)
                throw new InvalidOperationException($"SDL GPU command buffer acquisition failed: {SDL_GetError()}");

            SDL_GPUCopyPass* copyPass = SDL_BeginGPUCopyPass(commandBuffer);

            if (copyPass is null)
                throw new InvalidOperationException($"SDL GPU copy pass creation failed: {SDL_GetError()}");

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
            SDL_UploadToGPUBuffer(copyPass, &vertexSource, &vertexDestination, cycle: false);

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
            SDL_UploadToGPUBuffer(copyPass, &indexSource, &indexDestination, cycle: false);

            SDL_EndGPUCopyPass(copyPass);

            if (!SDL_SubmitGPUCommandBuffer(commandBuffer))
                throw new InvalidOperationException($"SDL GPU command buffer submission failed: {SDL_GetError()}");
        }
        finally
        {
            SDL_ReleaseGPUTransferBuffer(device, transferBuffer);
        }
    }

    private void EnsureDepthTexture(uint width, uint height)
    {
        if (depthTexture is not null && depthTextureWidth == width && depthTextureHeight == height)
            return;

        if (depthTexture is not null)
            SDL_ReleaseGPUTexture(device, depthTexture);

        depthTextureWidth = width;
        depthTextureHeight = height;

        var createInfo = new SDL_GPUTextureCreateInfo
        {
            type = SDL_GPUTextureType.SDL_GPU_TEXTURETYPE_2D,
            format = DepthFormat,
            usage = SDL_GPUTextureUsageFlags.SDL_GPU_TEXTUREUSAGE_DEPTH_STENCIL_TARGET,
            width = Math.Max(1, width),
            height = Math.Max(1, height),
            layer_count_or_depth = 1,
            num_levels = 1,
            sample_count = SDL_GPUSampleCount.SDL_GPU_SAMPLECOUNT_1,
        };

        depthTexture = SDL_CreateGPUTexture(device, &createInfo);

        if (depthTexture is null)
            throw new InvalidOperationException($"SDL GPU depth texture creation failed: {SDL_GetError()}");
    }
}
