#include "imgui.h"
#include "backends/imgui_impl_sdl3.h"
#include "backends/imgui_impl_sdlgpu3.h"

extern "C"
{
__attribute__((visibility("default"))) bool royale_imgui_sdl3_init_for_sdlgpu(SDL_Window* window)
{
    return ImGui_ImplSDL3_InitForSDLGPU(window);
}

__attribute__((visibility("default"))) bool royale_imgui_sdl3_process_event(const SDL_Event* event)
{
    return ImGui_ImplSDL3_ProcessEvent(event);
}

__attribute__((visibility("default"))) void royale_imgui_sdl3_new_frame()
{
    ImGui_ImplSDL3_NewFrame();
}

__attribute__((visibility("default"))) void royale_imgui_sdl3_shutdown()
{
    ImGui_ImplSDL3_Shutdown();
}

__attribute__((visibility("default"))) bool royale_imgui_sdlgpu3_init(SDL_GPUDevice* device, int color_target_format)
{
    ImGui_ImplSDLGPU3_InitInfo init_info = {};
    init_info.Device = device;
    init_info.ColorTargetFormat = static_cast<SDL_GPUTextureFormat>(color_target_format);
    init_info.MSAASamples = SDL_GPU_SAMPLECOUNT_1;
    return ImGui_ImplSDLGPU3_Init(&init_info);
}

__attribute__((visibility("default"))) void royale_imgui_sdlgpu3_new_frame()
{
    ImGui_ImplSDLGPU3_NewFrame();
}

__attribute__((visibility("default"))) void royale_imgui_sdlgpu3_shutdown()
{
    ImGui_ImplSDLGPU3_Shutdown();
}

__attribute__((visibility("default"))) void royale_imgui_sdlgpu3_prepare_draw_data(ImDrawData* draw_data, SDL_GPUCommandBuffer* command_buffer)
{
    ImGui_ImplSDLGPU3_PrepareDrawData(draw_data, command_buffer);
}

__attribute__((visibility("default"))) void royale_imgui_sdlgpu3_render_draw_data(ImDrawData* draw_data, SDL_GPUCommandBuffer* command_buffer, SDL_GPURenderPass* render_pass)
{
    ImGui_ImplSDLGPU3_RenderDrawData(draw_data, command_buffer, render_pass);
}
}
