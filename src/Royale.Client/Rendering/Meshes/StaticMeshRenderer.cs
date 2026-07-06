using System.Numerics;
using Royale.Client.Rendering.Cameras;
using Royale.Client.Rendering.Shaders;
using SDL;
using static SDL.SDL3;

namespace Royale.Client.Rendering.Meshes;

internal sealed unsafe class StaticMeshRenderer : IDisposable
{
    internal const SDL_GPUTextureFormat DepthFormat = SDL_GPUTextureFormat.SDL_GPU_TEXTUREFORMAT_D32_FLOAT;

    private readonly SDL_GPUDevice* device;
    private readonly SDL_GPUShader* vertexShader;
    private readonly SDL_GPUShader* fragmentShader;
    private readonly SDL_GPUGraphicsPipeline* pipeline;
    private readonly UploadedStaticMesh[] uploadedMeshes;
    private readonly StaticMeshLightingConstants lightingConstants = StaticMeshLightingConstants.CreateDefault();
    private SDL_GPUTexture* depthTexture;
    private uint depthTextureWidth;
    private uint depthTextureHeight;

    public StaticMeshRenderer(
        SDL_GPUDevice* device,
        SDL_GPUTextureFormat swapchainFormat,
        SDL_GPUShaderFormat shaderFormat,
        StaticMeshScene scene)
        : this(device, swapchainFormat, shaderFormat, scene.CreateRenderBatches())
    {
    }

    private StaticMeshRenderer(
        SDL_GPUDevice* device,
        SDL_GPUTextureFormat swapchainFormat,
        SDL_GPUShaderFormat shaderFormat,
        IReadOnlyList<StaticMeshRenderBatch> renderBatches)
    {
        this.device = device;

        vertexShader = LoadShader("basic.vert", shaderFormat, SDL_GPUShaderStage.SDL_GPU_SHADERSTAGE_VERTEX, uniformBufferCount: 1);
        fragmentShader = LoadShader("basic.frag", shaderFormat, SDL_GPUShaderStage.SDL_GPU_SHADERSTAGE_FRAGMENT, uniformBufferCount: 1);
        pipeline = CreatePipeline(swapchainFormat);
        uploadedMeshes = renderBatches.Select(CreateUploadedMesh).ToArray();

        foreach (UploadedStaticMesh mesh in uploadedMeshes)
            UploadGeometry(mesh);
    }

    public void Render(
        SDL_GPUCommandBuffer* commandBuffer,
        SDL_GPURenderPass* renderPass,
        uint width,
        uint height,
        RenderCamera camera)
    {
        SDL_BindGPUGraphicsPipeline(renderPass, pipeline);
        fixed (StaticMeshLightingConstants* lightingPointer = &lightingConstants)
            SDL_PushGPUFragmentUniformData(commandBuffer, 0, (IntPtr)lightingPointer, (uint)sizeof(StaticMeshLightingConstants));

        foreach (UploadedStaticMesh mesh in uploadedMeshes)
        {
            var vertexBinding = new SDL_GPUBufferBinding
            {
                buffer = mesh.VertexBuffer,
                offset = 0,
            };
            var indexBinding = new SDL_GPUBufferBinding
            {
                buffer = mesh.IndexBuffer,
                offset = 0,
            };

            SDL_BindGPUVertexBuffers(renderPass, 0, &vertexBinding, 1);
            SDL_BindGPUIndexBuffer(renderPass, &indexBinding, SDL_GPUIndexElementSize.SDL_GPU_INDEXELEMENTSIZE_16BIT);

            foreach (StaticMeshInstance instance in mesh.Instances)
            {
                StaticMeshInstanceShaderConstants instanceConstants = StaticMeshDraw.CreateShaderConstants(instance, camera, width, height);
                SDL_PushGPUVertexUniformData(commandBuffer, 0, (IntPtr)(&instanceConstants), (uint)sizeof(StaticMeshInstanceShaderConstants));
                SDL_DrawGPUIndexedPrimitives(renderPass, mesh.IndexCount, 1, 0, 0, 0);
            }
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

        foreach (UploadedStaticMesh mesh in uploadedMeshes)
            mesh.Dispose(device);

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
            pitch = (uint)StaticMeshVertex.Stride,
            input_rate = SDL_GPUVertexInputRate.SDL_GPU_VERTEXINPUTRATE_VERTEX,
        };

        SDL_GPUVertexAttribute* vertexAttributes = stackalloc SDL_GPUVertexAttribute[2];
        vertexAttributes[0] = new SDL_GPUVertexAttribute
        {
            location = 0,
            buffer_slot = 0,
            format = SDL_GPUVertexElementFormat.SDL_GPU_VERTEXELEMENTFORMAT_FLOAT3,
            offset = StaticMeshVertex.PositionOffset,
        };
        vertexAttributes[1] = new SDL_GPUVertexAttribute
        {
            location = 1,
            buffer_slot = 0,
            format = SDL_GPUVertexElementFormat.SDL_GPU_VERTEXELEMENTFORMAT_FLOAT3,
            offset = (uint)StaticMeshVertex.NormalOffset,
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

    private UploadedStaticMesh CreateUploadedMesh(StaticMeshRenderBatch batch)
    {
        if (batch.Geometry.Vertices.Count == 0)
            throw new InvalidOperationException($"Static mesh batch '{batch.DebugName}' has no vertices.");

        if (batch.Geometry.Indices.Count == 0)
            throw new InvalidOperationException($"Static mesh batch '{batch.DebugName}' has no indices.");

        return new UploadedStaticMesh(
            batch.DebugName,
            batch.Geometry,
            batch.Instances,
            CreateBuffer(SDL_GPUBufferUsageFlags.SDL_GPU_BUFFERUSAGE_VERTEX, (uint)(batch.Geometry.Vertices.Count * StaticMeshVertex.Stride)),
            CreateBuffer(SDL_GPUBufferUsageFlags.SDL_GPU_BUFFERUSAGE_INDEX, (uint)(batch.Geometry.Indices.Count * sizeof(ushort))));
    }

    private void UploadGeometry(UploadedStaticMesh mesh)
    {
        uint vertexBytes = (uint)(mesh.Geometry.Vertices.Count * StaticMeshVertex.Stride);
        uint indexBytes = (uint)(mesh.Geometry.Indices.Count * sizeof(ushort));
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

            StaticMeshVertex[] vertices = mesh.Geometry.Vertices.ToArray();
            ushort[] indices = mesh.Geometry.Indices.ToArray();

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
                buffer = mesh.VertexBuffer,
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
                buffer = mesh.IndexBuffer,
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

    private sealed class UploadedStaticMesh
    {
        public UploadedStaticMesh(
            string debugName,
            StaticMeshGeometry geometry,
            IReadOnlyList<StaticMeshInstance> instances,
            SDL_GPUBuffer* vertexBuffer,
            SDL_GPUBuffer* indexBuffer)
        {
            DebugName = debugName;
            Geometry = geometry;
            Instances = instances;
            VertexBuffer = vertexBuffer;
            IndexBuffer = indexBuffer;
            IndexCount = (uint)geometry.Indices.Count;
        }

        public string DebugName { get; }

        public StaticMeshGeometry Geometry { get; }

        public IReadOnlyList<StaticMeshInstance> Instances { get; }

        public SDL_GPUBuffer* VertexBuffer { get; private set; }

        public SDL_GPUBuffer* IndexBuffer { get; private set; }

        public uint IndexCount { get; }

        public void Dispose(SDL_GPUDevice* device)
        {
            if (IndexBuffer is not null)
            {
                SDL_ReleaseGPUBuffer(device, IndexBuffer);
                IndexBuffer = null;
            }

            if (VertexBuffer is not null)
            {
                SDL_ReleaseGPUBuffer(device, VertexBuffer);
                VertexBuffer = null;
            }
        }
    }
}
