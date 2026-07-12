using Evergine.Bindings.Imgui;
using Evergine.Mathematics;
using Microsoft.Extensions.Logging;
using Royale.Content;
using Royale.Content.Maps;
using Royale.Content.Models;
using Royale.Editor.Launch;
using Royale.Editor.Viewport;
using Royale.Editor.Workspace;
using Royale.Platform.Desktop;
using Royale.Rendering;
using Royale.Rendering.Cameras;
using Royale.Rendering.Meshes;
using Royale.Rendering.Platform;
using Royale.Rendering.Screenshots;
using Royale.Rendering.UI;
using SDL;

namespace Royale.Editor.Platform;

public sealed unsafe class EditorApplication : ISdlDesktopApplication, IDisposable
{
    private const string ViewportName = "Viewport"; private const string HierarchyName = "Hierarchy"; private const string InspectorName = "Inspector"; private const string AssetsName = "Asset Browser"; private const string ValidationName = "Validation"; private const string LogName = "Log";
    private readonly EditorLaunchOptions options; private readonly ILogger logger; private readonly SdlDesktopHost host; private readonly EditorWorkspaceState workspace = new(); private readonly EditorLog log = new(); private readonly EditorCameraController camera = new();
    private readonly ViewportInputOwnership inputOwnership = new();
    private SdlGpuDevice? gpu; private SdlGpuImGuiBackend? imgui; private SdlGpuOffscreenTarget? target; private GameMap? map; private StaticMeshScene? scene; private ModelAssetManifest? manifest; private int frames; private ViewportPixelSize requestedSize = new(1, 1); private bool viewportHovered; private bool windowFocused = true;

    public EditorApplication(EditorLaunchOptions options, ILogger<EditorApplication> logger)
    {
        this.options = options; this.logger = logger;
        host = new SdlDesktopHost(new SdlWindowSettings("Royale Editor", 1920, 1080, SDL_WindowFlags.SDL_WINDOW_RESIZABLE | SDL_WindowFlags.SDL_WINDOW_HIGH_PIXEL_DENSITY), new SdlLoopSettings(1.0 / 60.0, 4, 1), logger);
        if (options.ResetLayout) workspace.RequestLayoutReset();
    }
    public void Run() => host.Run(this);
    public void Initialize(SdlDesktopHost desktopHost)
    {
        try
        {
            string layout = EditorLayoutPath.Resolve(); Directory.CreateDirectory(Path.GetDirectoryName(layout)!);
            map = MapCatalog.LoadById(options.MapId); camera.Frame(map.WorldBounds); log.Add($"Loaded map {map.Id}.");
            string manifestPath = Path.Combine(AppContext.BaseDirectory, "assets", ContentCatalog.ModelAssetManifestFileName); manifest = ModelAssetManifestLoader.LoadGenerated(manifestPath);
            StaticMeshAssetCache cache = StaticMeshAssetCache.Load(AppContext.BaseDirectory); var assets = map.StaticModels.Select(x => x.AssetId).Distinct(StringComparer.Ordinal).ToDictionary(x => x, cache.GetRequired, StringComparer.Ordinal); scene = MapStaticMeshScene.CreateScene(map, assets);
            gpu = SdlGpuDevice.Create(host.Window!); target = gpu.CreateOffscreenTarget(1, 1); imgui = SdlGpuImGuiBackend.Create(host.Window!, gpu, new SdlGpuImGuiSettings(true, layout));
            if (!File.Exists(layout)) workspace.RequestLayoutReset();
            log.Add("Rendering and ImGui docking initialized."); logger.LogInformation("Loaded editor map {MapId} and {AssetCount} manifest assets.", map.Id, manifest.Assets.Count);
        }
        catch (Exception ex) { log.Add($"Initialization failed: {ex.Message}"); logger.LogError(ex, "Editor initialization failed for map {MapId}.", options.MapId); throw; }
    }
    public void Update(SdlFrameTime time)
    {
        imgui?.NewFrame(time.DeltaSeconds);
        bool escape = host.Input.WasKeyPressed((int)SDL_Keycode.SDLK_ESCAPE); bool right = host.Input.IsMouseButtonDown(SDL3.SDL_BUTTON_RIGHT);
        inputOwnership.Update(viewportHovered, workspace.ViewportVisible, windowFocused, right, escape);
        camera.SetCaptured(inputOwnership.Captured);
        imgui?.SetMouseInputEnabled(inputOwnership.ImGuiMouseInputEnabled);
        host.Window?.RelativeMouseMode.SetEnabled(inputOwnership.Captured);
        camera.Move(new DebugCameraInput(Down(SDL_Keycode.SDLK_W), Down(SDL_Keycode.SDLK_S), Down(SDL_Keycode.SDLK_A), Down(SDL_Keycode.SDLK_D), Down(SDL_Keycode.SDLK_E), Down(SDL_Keycode.SDLK_Q), host.Input.MouseDeltaX, host.Input.MouseDeltaY, camera.Captured), time.DeltaSeconds);
    }
    private bool Down(SDL_Keycode key) => host.Input.IsKeyDown((int)key);
    public void FixedUpdate(SdlFixedTickTime time) { }
    public void ProcessEvent(in SDL_Event e)
    {
        SDL_Event value = e; imgui?.ProcessEvent(&value);
        switch (e.Type) { case SDL_EventType.SDL_EVENT_KEY_DOWN: if (!e.key.repeat) host.Input.SetKeyDown((int)e.key.key); break; case SDL_EventType.SDL_EVENT_KEY_UP: host.Input.SetKeyUp((int)e.key.key); break; case SDL_EventType.SDL_EVENT_MOUSE_BUTTON_DOWN: host.Input.SetMouseButtonDown(e.button.button); break; case SDL_EventType.SDL_EVENT_MOUSE_BUTTON_UP: host.Input.SetMouseButtonUp(e.button.button); break; case SDL_EventType.SDL_EVENT_MOUSE_MOTION: host.Input.AddMouseDelta(e.motion.xrel, e.motion.yrel); break; case SDL_EventType.SDL_EVENT_WINDOW_FOCUS_GAINED: windowFocused = true; break; case SDL_EventType.SDL_EVENT_WINDOW_FOCUS_LOST: windowFocused = false; ReleaseViewportInput(); break; }
    }
    public void Render(SdlFrameTime time)
    {
        if (gpu is null || imgui is null || target is null || scene is null || map is null || manifest is null) return;
        if (target.Width != requestedSize.Width || target.Height != requestedSize.Height) target.Resize(requestedSize.Width, requestedSize.Height);
        gpu.RenderOffscreen(target, new RenderFrame(camera.ToRenderCamera(), scene, RenderViewMode.Normal));
        BuildWorkspace(target, map, manifest);
        frames++; bool capture = options.ScreenshotPath is not null && frames == options.ScreenshotAfterFrames;
        GpuImageReadback? image = gpu.PresentFrame(new RenderFrame(camera.ToRenderCamera(), new StaticMeshScene([], []), RenderViewMode.Normal), imgui, capture);
        if (capture && image is not null) { BmpScreenshotWriter.Save(options.ScreenshotPath!, image.RgbaBytes, image.Width, image.Height, SDL_GPUTextureFormat.SDL_GPU_TEXTUREFORMAT_R8G8B8A8_UNORM); host.RequestExit(); }
    }
    private void BuildWorkspace(SdlGpuOffscreenTarget viewport, GameMap loadedMap, ModelAssetManifest loadedManifest)
    {
        BuildMenu(); ImGuiViewport* main = ImguiNative.igGetMainViewport(); uint dockId = ImguiNative.igGetID_Str("RoyaleEditorDockspace");
        if (workspace.ConsumeLayoutReset()) ResetLayout(dockId, main->Size);
        ImguiNative.igDockSpaceOverViewport(dockId, main, ImGuiDockNodeFlags.PassthruCentralNode, null);
        if (workspace.HierarchyVisible) Window(HierarchyName, () => { Group("Static Boxes", loadedMap.StaticBoxes.Select(x => x.Id)); Group("Static Models", loadedMap.StaticModels.Select(x => x.Id)); Group("Spawn Points", loadedMap.SpawnPoints.Select(x => x.Id)); Group("Loot Points", loadedMap.LootPoints.Select(x => x.Id)); Group("Navigation Nodes", loadedMap.Navigation.Waypoints.Select(x => x.Id)); });
        if (workspace.InspectorVisible) Window(InspectorName, () => { Text($"Map: {loadedMap.Name} ({loadedMap.Id})"); MapVector3 min = loadedMap.WorldBounds.Min, max = loadedMap.WorldBounds.Max, zone = loadedMap.SafeZone.Center; Text($"Bounds min: {min.X:0.##}, {min.Y:0.##}, {min.Z:0.##}"); Text($"Bounds max: {max.X:0.##}, {max.Y:0.##}, {max.Z:0.##}"); Text($"Safe zone: {zone.X:0.##}, {zone.Y:0.##}, {zone.Z:0.##}; r {loadedMap.SafeZone.Radius:0.##}"); EditorMapSummary s = EditorMapSummary.Create(loadedMap); Text($"Boxes {s.StaticBoxes}; models {s.StaticModels}"); Text($"Spawns {s.SpawnPoints}; loot {s.LootPoints}; nav {s.NavigationNodes}"); });
        if (workspace.AssetBrowserVisible) Window(AssetsName, () => { foreach (ModelAssetDefinition a in loadedManifest.Assets) Text($"{a.Id}  [{(a.Render is null ? "no render" : "render ready")}]"); });
        if (workspace.ValidationVisible) Window(ValidationName, () => { Text("Runtime map loading: successful"); Text("Model manifest loading: successful"); Text($"Map content: {loadedMap.StaticBoxes.Count + loadedMap.StaticModels.Count} static objects"); });
        if (workspace.LogVisible) Window(LogName, () => { foreach (string entry in log.Entries) Text(entry); });
        if (workspace.ViewportVisible) Window(ViewportName, () => { Vector2 available = ImguiNative.igGetContentRegionAvail(); ImGuiIO* io = ImguiNative.igGetIO_Nil(); requestedSize = ViewportPixelSize.FromLogical(available.X, available.Y, io->DisplayFramebufferScale.X, io->DisplayFramebufferScale.Y); viewportHovered = ImguiNative.igIsWindowHovered(ImGuiHoveredFlags.RootAndChildWindows); ImguiNative.igImage(new ImTextureRef { _TexID = (ulong)viewport.NativeTextureHandle }, available, new Vector2(0, 0), new Vector2(1, 1)); }); else viewportHovered = false;
    }
    private void BuildMenu()
    {
        if (!ImguiNative.igBeginMainMenuBar()) return;
        if (ImguiNative.igBeginMenu("File", true)) { ImguiNative.igMenuItem_Bool("New", "", false, false); ImguiNative.igMenuItem_Bool("Save", "", false, false); if (ImguiNative.igMenuItem_Bool("Exit", "", false, true)) host.RequestExit(); ImguiNative.igEndMenu(); }
        if (ImguiNative.igBeginMenu("View", true)) { workspace.HierarchyVisible = Toggle("Hierarchy", workspace.HierarchyVisible); workspace.InspectorVisible = Toggle("Inspector", workspace.InspectorVisible); workspace.AssetBrowserVisible = Toggle("Asset Browser", workspace.AssetBrowserVisible); workspace.ValidationVisible = Toggle("Validation", workspace.ValidationVisible); workspace.LogVisible = Toggle("Log", workspace.LogVisible); workspace.ViewportVisible = Toggle("Viewport", workspace.ViewportVisible); ImguiNative.igEndMenu(); }
        if (ImguiNative.igBeginMenu("Window", true)) { if (ImguiNative.igMenuItem_Bool("Reset Default Layout", "", false, true)) workspace.RequestLayoutReset(); ImguiNative.igEndMenu(); } ImguiNative.igEndMainMenuBar();
    }
    private static bool Toggle(string name, bool value) => ImguiNative.igMenuItem_Bool(name, "", value, true) ? !value : value;
    private static void Window(string name, Action body) { if (ImguiNative.igBegin(name, null, ImGuiWindowFlags.None)) body(); ImguiNative.igEnd(); }
    private static void Text(string value) => ImguiNative.igTextUnformatted(value, null);
    private static void Group(string name, IEnumerable<string> entries) { if (!ImguiNative.igCollapsingHeader_TreeNodeFlags(name, ImGuiTreeNodeFlags.DefaultOpen)) return; foreach (string entry in entries) Text(entry); }
    private static void ResetLayout(uint root, Vector2 size)
    {
        ImGuiDockBuilder.RemoveNode(root); ImGuiDockBuilder.AddDockSpace(root); ImGuiDockBuilder.SetNodeSize(root, size); uint left = ImGuiDockBuilder.Split(root, ImGuiDir.Left, .20f, out uint rest); uint right = ImGuiDockBuilder.Split(rest, ImGuiDir.Right, .25f, out uint center); uint bottom = ImGuiDockBuilder.Split(center, ImGuiDir.Down, .25f, out uint viewport);
        ImGuiDockBuilder.DockWindow(HierarchyName, left); ImGuiDockBuilder.DockWindow(InspectorName, right); ImGuiDockBuilder.DockWindow(AssetsName, bottom); ImGuiDockBuilder.DockWindow(ValidationName, bottom); ImGuiDockBuilder.DockWindow(LogName, bottom); ImGuiDockBuilder.DockWindow(ViewportName, viewport); ImGuiDockBuilder.Finish(root);
    }
    private void ReleaseViewportInput()
    {
        inputOwnership.Release();
        camera.SetCaptured(false);
        imgui?.SetMouseInputEnabled(true);
        host.Window?.RelativeMouseMode.SetEnabled(false);
    }

    public void Dispose() { ReleaseViewportInput(); target?.Dispose(); imgui?.Dispose(); gpu?.Dispose(); host.Dispose(); }
}
