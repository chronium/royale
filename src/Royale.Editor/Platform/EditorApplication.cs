using Evergine.Bindings.Imgui;
using Evergine.Mathematics;
using Microsoft.Extensions.Logging;
using Royale.Content;
using Royale.Content.Maps;
using Royale.Content.Models;
using Royale.Editor.Launch;
using Royale.Editor.Dialogs;
using Royale.Editor.Documents;
using Royale.Editor.Persistence;
using Royale.Editor.Projects;
using Royale.Editor.Viewport;
using Royale.Editor.Workspace;
using Royale.Editor.Workspace.Assets;
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
    private const string ViewportName = "Viewport";
    private const string HierarchyName = "Hierarchy";
    private const string InspectorName = "Inspector";
    private const string AssetsName = "Asset Browser";
    private const string ValidationName = "Validation";
    private const string LogName = "Log";

    private readonly EditorLaunchOptions options;
    private readonly ILogger logger;
    private readonly SdlDesktopHost host;
    private readonly EditorWorkspaceState workspace = new();
    private readonly EditorLog log = new();
    private readonly EditorCameraController camera = new();
    private readonly ViewportInputOwnership inputOwnership = new();
    private readonly IEditorFileDialogService dialogs;
    private readonly byte[] nameBuffer = new byte[256];
    private readonly byte[] newProjectIdBuffer = new byte[128];
    private readonly byte[] newProjectNameBuffer = new byte[256];

    private SdlGpuDevice? gpu;
    private SdlGpuImGuiBackend? imgui;
    private SdlGpuOffscreenTarget? target;
    private EditorMapDocument? document;
    private EditorProjectSession? projectSession;
    private readonly RecentProjectStore recentProjects = new();
    private StaticMeshScene? scene;
    private ModelAssetManifest? manifest;
    private AssetBrowserModel? assetBrowser;
    private AssetBrowserRenderer? assetBrowserRenderer;
    private StaticMeshAssetCache? meshCache;
    private int frames;
    private ViewportPixelSize requestedSize = new(1, 1);
    private bool viewportHovered;
    private bool windowFocused = true;
    private bool modalOpenRequested;
    private bool newProjectModalRequested;
    private PendingOperation pendingOperation;
    private EditorKeyboardShortcut pendingShortcut;
    private string validationMessage = "Runtime map loading: successful";

    public EditorApplication(EditorLaunchOptions options, ILogger<EditorApplication> logger, IEditorFileDialogService? dialogs = null)
    {
        this.options = options;
        this.logger = logger;
        this.dialogs = dialogs ?? new SdlEditorFileDialogService();
        host = new SdlDesktopHost(
            new SdlWindowSettings(
                "Royale Editor",
                1920,
                1080,
                SDL_WindowFlags.SDL_WINDOW_RESIZABLE | SDL_WindowFlags.SDL_WINDOW_HIGH_PIXEL_DENSITY),
            new SdlLoopSettings(1.0 / 60.0, 4, 1),
            logger);

        if (options.ResetLayout)
            workspace.RequestLayoutReset();
    }

    public void Run() => host.Run(this);

    public void Initialize(SdlDesktopHost desktopHost)
    {
        try
        {
            string layout = EditorLayoutPath.Resolve();
            Directory.CreateDirectory(Path.GetDirectoryName(layout)!);

            EditorStartupTarget startup = EditorStartupTargetResolver.Resolve(
                options,
                recentProjects,
                Environment.CurrentDirectory,
                AppContext.BaseDirectory);
            if (startup.Warning is not null)
                log.Add(startup.Warning);
            if (startup.Kind == EditorStartupTargetKind.Project)
                LoadProject(startup.Path, recordRecent: true);
            else
                LoadDocument(startup.Path, startup.RequiresSaveAs);

            gpu = SdlGpuDevice.Create(host.Window!);
            target = gpu.CreateOffscreenTarget(1, 1);
            imgui = SdlGpuImGuiBackend.Create(
                host.Window!,
                gpu,
                new SdlGpuImGuiSettings(true, layout));

            if (!File.Exists(layout))
                workspace.RequestLayoutReset();

            log.Add("Rendering and ImGui docking initialized.");
            logger.LogInformation(
                "Loaded editor map {MapId} and {AssetCount} manifest assets.",
                document!.Map.Id,
                manifest!.Assets.Count);
        }
        catch (Exception ex)
        {
            log.Add($"Initialization failed: {ex.Message}");
            logger.LogError(ex, "Editor initialization failed for map {MapId}.", options.MapId);
            throw;
        }
    }

    public void Update(SdlFrameTime time)
    {
        ProcessDialogResults();
        ProcessShortcuts();
        UpdateWindowTitle();

        bool escape = host.Input.WasKeyPressed((int)SDL_Keycode.SDLK_ESCAPE);
        bool right = host.Input.IsMouseButtonDown(SDL3.SDL_BUTTON_RIGHT);
        ViewportInputState viewportState =
            (viewportHovered ? ViewportInputState.Hovered : 0) |
            (workspace.ViewportVisible ? ViewportInputState.Visible : 0) |
            (windowFocused ? ViewportInputState.WindowFocused : 0) |
            (right ? ViewportInputState.RightMouseDown : 0) |
            (escape ? ViewportInputState.EscapePressed : 0);
        inputOwnership.Update(viewportState);
        camera.SetCaptured(inputOwnership.Captured);
        imgui?.NewFrame(time.DeltaSeconds, inputOwnership.ImGuiMouseInputEnabled);
        host.Window?.RelativeMouseMode.SetEnabled(inputOwnership.Captured);
        EditorCameraActions actions =
            (Down(SDL_Keycode.SDLK_W) ? EditorCameraActions.MoveForward : 0) |
            (Down(SDL_Keycode.SDLK_S) ? EditorCameraActions.MoveBackward : 0) |
            (Down(SDL_Keycode.SDLK_A) ? EditorCameraActions.MoveLeft : 0) |
            (Down(SDL_Keycode.SDLK_D) ? EditorCameraActions.MoveRight : 0) |
            (Down(SDL_Keycode.SDLK_E) ? EditorCameraActions.MoveUp : 0) |
            (Down(SDL_Keycode.SDLK_Q) ? EditorCameraActions.MoveDown : 0) |
            (Down(SDL_Keycode.SDLK_LSHIFT) ? EditorCameraActions.LeftBoost : 0) |
            (Down(SDL_Keycode.SDLK_RSHIFT) ? EditorCameraActions.RightBoost : 0);
        camera.Update(
            new EditorCameraInput(
                actions,
                host.Input.MouseDeltaX,
                host.Input.MouseDeltaY,
                host.Input.MouseWheelY,
                viewportHovered && workspace.ViewportVisible && windowFocused),
            time.DeltaSeconds);
    }

    private bool Down(SDL_Keycode key) => host.Input.IsKeyDown((int)key);

    public void FixedUpdate(SdlFixedTickTime time)
    {
    }

    public void ProcessEvent(in SDL_Event e)
    {
        bool consumeViewportWheel =
            e.Type == SDL_EventType.SDL_EVENT_MOUSE_WHEEL &&
            viewportHovered &&
            workspace.ViewportVisible &&
            windowFocused;
        bool consumeShortcut = QueueShortcut(e);

        if (!consumeViewportWheel && !consumeShortcut)
        {
            SDL_Event value = e;
            imgui?.ProcessEvent(&value);
        }

        switch (e.Type)
        {
            case SDL_EventType.SDL_EVENT_KEY_DOWN:
                if (!e.key.repeat)
                    host.Input.SetKeyDown((int)e.key.key);
                break;
            case SDL_EventType.SDL_EVENT_KEY_UP:
                host.Input.SetKeyUp((int)e.key.key);
                break;
            case SDL_EventType.SDL_EVENT_MOUSE_BUTTON_DOWN:
                host.Input.SetMouseButtonDown(e.button.button);
                break;
            case SDL_EventType.SDL_EVENT_MOUSE_BUTTON_UP:
                host.Input.SetMouseButtonUp(e.button.button);
                break;
            case SDL_EventType.SDL_EVENT_MOUSE_MOTION:
                host.Input.AddMouseDelta(e.motion.xrel, e.motion.yrel);
                break;
            case SDL_EventType.SDL_EVENT_MOUSE_WHEEL when consumeViewportWheel:
                bool flipped = e.wheel.direction == SDL_MouseWheelDirection.SDL_MOUSEWHEEL_FLIPPED;
                host.Input.AddMouseWheel(
                    EditorMouseWheel.Normalize(e.wheel.x, flipped),
                    EditorMouseWheel.Normalize(e.wheel.y, flipped));
                break;
            case SDL_EventType.SDL_EVENT_WINDOW_FOCUS_GAINED:
                windowFocused = true;
                break;
            case SDL_EventType.SDL_EVENT_WINDOW_FOCUS_LOST:
                windowFocused = false;
                camera.CancelDolly();
                ReleaseViewportInput();
                break;
            case SDL_EventType.SDL_EVENT_QUIT:
            case SDL_EventType.SDL_EVENT_WINDOW_CLOSE_REQUESTED:
                if (document?.IsDirty == true)
                {
                    host.CancelExit();
                    RequestPending(PendingOperation.Close);
                }
                break;
        }
    }

    public void Render(SdlFrameTime time)
    {
        if (gpu is null || imgui is null || target is null || scene is null || document is null || manifest is null || assetBrowserRenderer is null)
            return;

        if (target.Width != requestedSize.Width || target.Height != requestedSize.Height)
            target.Resize(requestedSize.Width, requestedSize.Height);

        gpu.RenderOffscreen(target, new RenderFrame(camera.ToRenderCamera(), scene, RenderViewMode.Normal));
        BuildWorkspace(target, document.Map);

        frames++;
        bool capture = options.ScreenshotPath is not null && frames == options.ScreenshotAfterFrames;
        GpuImageReadback? image = gpu.PresentFrame(
            new RenderFrame(camera.ToRenderCamera(), new StaticMeshScene([], []), RenderViewMode.Normal),
            imgui,
            capture);

        if (capture && image is not null)
        {
            BmpScreenshotWriter.Save(
                options.ScreenshotPath!,
                image.RgbaBytes,
                image.Width,
                image.Height,
                SDL_GPUTextureFormat.SDL_GPU_TEXTUREFORMAT_R8G8B8A8_UNORM);
            host.RequestExit();
        }
    }

    private void BuildWorkspace(SdlGpuOffscreenTarget viewport, GameMap loadedMap)
    {
        BuildMenu();
        ImGuiViewport* main = ImguiNative.igGetMainViewport();
        uint dockId = ImguiNative.igGetID_Str("RoyaleEditorDockspace");
        if (workspace.ConsumeLayoutReset())
            ResetLayout(dockId, main->Size);

        ImguiNative.igDockSpaceOverViewport(dockId, main, ImGuiDockNodeFlags.PassthruCentralNode, null);

        if (workspace.HierarchyVisible)
            Window(HierarchyName, () => BuildHierarchy(loadedMap));
        if (workspace.InspectorVisible)
            Window(InspectorName, () => BuildInspector(loadedMap));
        if (workspace.AssetBrowserVisible)
            Window(AssetsName, assetBrowserRenderer!.Render);
        if (workspace.ValidationVisible)
            Window(ValidationName, () => BuildValidation(loadedMap));
        if (workspace.LogVisible)
            Window(LogName, BuildLog);

        if (workspace.ViewportVisible)
            Window(ViewportName, () => BuildViewport(viewport));
        else
            viewportHovered = false;

        BuildUnsavedModal();
        BuildNewProjectModal();
    }

    private static void BuildHierarchy(GameMap map)
    {
        Group("Static Boxes", map.StaticBoxes.Select(x => x.Id));
        Group("Static Models", map.StaticModels.Select(x => x.Id));
        Group("Spawn Points", map.SpawnPoints.Select(x => x.Id));
        Group("Loot Points", map.LootPoints.Select(x => x.Id));
        Group("Navigation Nodes", map.Navigation.Waypoints.Select(x => x.Id));
    }

    private void BuildInspector(GameMap map)
    {
        Text($"Map ID: {map.Id}");
        fixed (byte* buffer = nameBuffer)
        {
            bool submitted = ImguiNative.igInputText(
                    "Display name",
                    buffer,
                    (uint)nameBuffer.Length,
                    ImGuiInputTextFlags.EnterReturnsTrue,
                    null,
                    null);
            if (submitted || ImguiNative.igIsItemDeactivatedAfterEdit())
                CommitMapName();
        }

        MapVector3 min = map.WorldBounds.Min;
        MapVector3 max = map.WorldBounds.Max;
        MapVector3 zone = map.SafeZone.Center;
        Text($"Bounds min: {min.X:0.##}, {min.Y:0.##}, {min.Z:0.##}");
        Text($"Bounds max: {max.X:0.##}, {max.Y:0.##}, {max.Z:0.##}");
        Text($"Safe zone: {zone.X:0.##}, {zone.Y:0.##}, {zone.Z:0.##}; r {map.SafeZone.Radius:0.##}");

        EditorMapSummary summary = EditorMapSummary.Create(map);
        Text($"Boxes {summary.StaticBoxes}; models {summary.StaticModels}");
        Text($"Spawns {summary.SpawnPoints}; loot {summary.LootPoints}; nav {summary.NavigationNodes}");
        if (projectSession is not null)
        {
            Text($"Project root: {projectSession.Project.Paths.Root}");
            Text($"Map: {projectSession.Project.Paths.Map}");
            Text($"Asset manifest: {projectSession.Project.Paths.AssetManifest}");
            Text($"Generated client: {projectSession.Project.Paths.GeneratedClient}");
            Text($"Generated server: {projectSession.Project.Paths.GeneratedServer}");
            Text($"Cache: {projectSession.Project.Paths.ThumbnailCache}");
        }
    }

    private void BuildValidation(GameMap map)
    {
        Text(validationMessage);
        Text("Model manifest loading: successful");
        Text($"Map content: {map.StaticBoxes.Count + map.StaticModels.Count} static objects");
    }

    private void BuildLog()
    {
        foreach (string entry in log.Entries)
            Text(entry);
    }

    private void BuildViewport(SdlGpuOffscreenTarget viewport)
    {
        Vector2 available = ImguiNative.igGetContentRegionAvail();
        ImGuiIO* io = ImguiNative.igGetIO_Nil();
        requestedSize = ViewportPixelSize.FromLogical(
            available.X,
            available.Y,
            io->DisplayFramebufferScale.X,
            io->DisplayFramebufferScale.Y);
        viewportHovered = ImguiNative.igIsWindowHovered(ImGuiHoveredFlags.RootAndChildWindows);
        ImguiNative.igImage(
            new ImTextureRef { _TexID = (ulong)viewport.NativeTextureHandle },
            available,
            new Vector2(0, 0),
            new Vector2(1, 1));
    }

    private void BuildMenu()
    {
        if (!ImguiNative.igBeginMainMenuBar())
            return;

        BuildFileMenu();
        BuildEditMenu();
        BuildViewMenu();
        BuildWindowMenu();
        ImguiNative.igEndMainMenuBar();
    }

    private void BuildFileMenu()
    {
        if (!ImguiNative.igBeginMenu("File", true))
            return;

        if (ImguiNative.igMenuItem_Bool("New Project", "", false, true))
            RequestTransition(PendingOperation.NewProject);
        if (ImguiNative.igMenuItem_Bool("Open Project", "Cmd/Ctrl+O", false, true))
            RequestTransition(PendingOperation.OpenProject);
        if (ImguiNative.igMenuItem_Bool("Open Map JSON", "", false, true))
            RequestTransition(PendingOperation.OpenMap);
        if (ImguiNative.igMenuItem_Bool("Convert Map to Project", "", false, projectSession is null && document is not null))
            RequestTransition(PendingOperation.Convert);
        if (ImguiNative.igMenuItem_Bool("Save", "Cmd/Ctrl+S", false, document is not null))
            Save(false);
        if (ImguiNative.igMenuItem_Bool("Save As", "Cmd/Ctrl+Shift+S", false, document is not null && projectSession is null))
            Save(true);
        if (ImguiNative.igMenuItem_Bool("Exit", "", false, true))
            RequestClose();

        ImguiNative.igEndMenu();
    }

    private void BuildEditMenu()
    {
        if (!ImguiNative.igBeginMenu("Edit", true))
            return;

        if (ImguiNative.igMenuItem_Bool("Undo", "Cmd/Ctrl+Z", false, document?.CanUndo == true))
            Undo();
        if (ImguiNative.igMenuItem_Bool("Redo", "Cmd/Ctrl+Shift+Z", false, document?.CanRedo == true))
            Redo();

        ImguiNative.igEndMenu();
    }

    private void BuildViewMenu()
    {
        if (!ImguiNative.igBeginMenu("View", true))
            return;

        workspace.HierarchyVisible = Toggle("Hierarchy", workspace.HierarchyVisible);
        workspace.InspectorVisible = Toggle("Inspector", workspace.InspectorVisible);
        workspace.AssetBrowserVisible = Toggle("Asset Browser", workspace.AssetBrowserVisible);
        workspace.ValidationVisible = Toggle("Validation", workspace.ValidationVisible);
        workspace.LogVisible = Toggle("Log", workspace.LogVisible);
        workspace.ViewportVisible = Toggle("Viewport", workspace.ViewportVisible);
        ImguiNative.igEndMenu();
    }

    private void BuildWindowMenu()
    {
        if (!ImguiNative.igBeginMenu("Window", true))
            return;

        if (ImguiNative.igMenuItem_Bool("Reset Default Layout", "", false, true))
            workspace.RequestLayoutReset();

        ImguiNative.igEndMenu();
    }

    private static bool Toggle(string name, bool value) => ImguiNative.igMenuItem_Bool(name, "", value, true) ? !value : value;

    private static void Window(string name, Action body)
    {
        if (ImguiNative.igBegin(name, null, ImGuiWindowFlags.None))
            body();

        ImguiNative.igEnd();
    }

    private static void Text(string value) => ImguiNative.igTextUnformatted(value, null);

    private static void Group(string name, IEnumerable<string> entries)
    {
        if (!ImguiNative.igCollapsingHeader_TreeNodeFlags(name, ImGuiTreeNodeFlags.DefaultOpen))
            return;

        foreach (string entry in entries)
            Text(entry);
    }

    private static void ResetLayout(uint root, Vector2 size)
    {
        ImGuiDockBuilder.RemoveNode(root);
        ImGuiDockBuilder.AddDockSpace(root);
        ImGuiDockBuilder.SetNodeSize(root, size);
        uint left = ImGuiDockBuilder.Split(root, ImGuiDir.Left, .20f, out uint rest);
        uint right = ImGuiDockBuilder.Split(rest, ImGuiDir.Right, .25f, out uint center);
        uint bottom = ImGuiDockBuilder.Split(center, ImGuiDir.Down, .25f, out uint viewport);
        ImGuiDockBuilder.DockWindow(HierarchyName, left);
        ImGuiDockBuilder.DockWindow(InspectorName, right);
        ImGuiDockBuilder.DockWindow(AssetsName, bottom);
        ImGuiDockBuilder.DockWindow(ValidationName, bottom);
        ImGuiDockBuilder.DockWindow(LogName, bottom);
        ImGuiDockBuilder.DockWindow(ViewportName, viewport);
        ImGuiDockBuilder.Finish(root);
    }

    private void ReleaseViewportInput()
    {
        inputOwnership.Release();
        camera.SetCaptured(false);
        imgui?.SetMouseInputEnabled(true);
        host.Window?.RelativeMouseMode.SetEnabled(false);
    }

    private void LoadDocument(string path, bool requiresSaveAs)
    {
        EditorMapDocument candidateDocument = EditorMapPersistence.Load(path, requiresSaveAs);
        string manifestPath = Path.Combine(AppContext.BaseDirectory, "assets", ContentCatalog.ModelAssetManifestFileName);
        ModelAssetManifest candidateManifest = ModelAssetManifestLoader.LoadGenerated(manifestPath);
        StaticMeshAssetCache candidateCache = StaticMeshAssetCache.Load(AppContext.BaseDirectory);
        StaticMeshScene candidateScene = CreateScene(candidateDocument.Map, candidateCache);
        projectSession = null;
        document = candidateDocument;
        manifest = candidateManifest;
        meshCache = candidateCache;
        scene = candidateScene;
        ReloadAssetBrowser();
        camera.Frame(document.Map.WorldBounds);
        SetNameBuffer(document.Map.Name);
        log.Add($"Loaded map {document.Map.Id} from {document.SourcePath}{(requiresSaveAs ? " (Save As required)" : string.Empty)}.");
        validationMessage = "Runtime map loading: successful";
    }

    private void LoadProject(string root, bool recordRecent)
    {
        EditorProjectSession candidate = EditorProjectSession.Load(root);
        StaticMeshAssetCache candidateCache = StaticMeshAssetCache.LoadSource(candidate.Project.Paths.Sources, candidate.Project.AssetManifest);
        StaticMeshScene candidateScene = CreateScene(candidate.Document.Map, candidateCache);
        projectSession = candidate;
        document = candidate.Document;
        manifest = candidate.Project.AssetManifest;
        meshCache = candidateCache;
        scene = candidateScene;
        ReloadAssetBrowser();
        camera.Frame(document.Map.WorldBounds);
        SetNameBuffer(document.Map.Name);
        validationMessage = "Project and source assets: successful";
        log.Add($"Loaded project {candidate.Project.Manifest.Id} from {candidate.Project.Paths.Root}.");
        if (recordRecent)
        {
            try
            {
                recentProjects.Write(candidate.Project.Paths.Root);
            }
            catch (Exception ex)
            {
                log.Add($"Could not record recent project: {ex.Message}");
                logger.LogWarning(ex, "Could not record recent editor project.");
            }
        }
    }

    private void ReloadAssetBrowser()
    {
        assetBrowser = new AssetBrowserModel(manifest!);
        assetBrowserRenderer = new AssetBrowserRenderer(assetBrowser);
    }

    private void RebuildScene()
    {
        if (document is null || meshCache is null)
            return;

        scene = CreateScene(document.Map, meshCache);
    }

    private static StaticMeshScene CreateScene(GameMap map, StaticMeshAssetCache cache)
    {
        var assets = map.StaticModels
            .Select(x => x.AssetId)
            .Distinct(StringComparer.Ordinal)
            .ToDictionary(x => x, cache.GetRequired, StringComparer.Ordinal);
        return MapStaticMeshScene.CreateScene(map, assets);
    }

    private void SetNameBuffer(string value)
    {
        Array.Clear(nameBuffer);
        int count = System.Text.Encoding.UTF8.GetBytes(value, nameBuffer);
        if (count == nameBuffer.Length)
            nameBuffer[^1] = 0;
    }

    private void CommitMapName()
    {
        if (document is null)
            return;

        int length = Array.IndexOf(nameBuffer, (byte)0);
        if (length < 0)
            length = nameBuffer.Length;

        string value = System.Text.Encoding.UTF8.GetString(nameBuffer, 0, length);
        if (string.IsNullOrWhiteSpace(value))
        {
            validationMessage = "Map name must be non-empty.";
            log.Add(validationMessage);
            SetNameBuffer(document.Map.Name);
            return;
        }

        if (string.Equals(value.Trim(), document.Map.Name, StringComparison.Ordinal))
            return;

        document.Execute(new SetMapNameCommand(document.Map.Name, value));
        SetNameBuffer(document.Map.Name);
        validationMessage = "In-memory map validation: successful";
        log.Add("Renamed map.");
    }

    private void Undo()
    {
        if (document?.Undo() != true)
            return;

        SetNameBuffer(document.Map.Name);
        log.Add($"Undo: {document.RedoDescription}.");
    }

    private void Redo()
    {
        if (document?.Redo() != true)
            return;

        SetNameBuffer(document.Map.Name);
        log.Add($"Redo: {document.UndoDescription}.");
    }

    private void RequestTransition(PendingOperation operation)
    {
        if (document?.IsDirty == true)
            RequestPending(operation);
        else
            ContinueOperation(operation);
    }

    private void RequestClose()
    {
        if (document?.IsDirty == true)
        {
            host.CancelExit();
            RequestPending(PendingOperation.Close);
        }
        else
        {
            host.RequestExit();
        }
    }

    private void RequestPending(PendingOperation operation)
    {
        pendingOperation = operation;
        modalOpenRequested = true;
    }

    private void ShowOpenMapDialog() => dialogs.ShowOpenJsonDialog(host.Window!.NativeHandle);

    private void Save(bool saveAs)
    {
        if (document is null)
            return;

        if (projectSession is not null)
        {
            try
            {
                projectSession.Save();
                validationMessage = "Project map validation and persistence: successful";
                log.Add($"Saved project map to {projectSession.Project.Paths.Map}.");
                ContinuePending();
            }
            catch (Exception ex)
            {
                validationMessage = ex.Message;
                log.Add($"Save failed: {ex.Message}");
                logger.LogError(ex, "Editor project save failed.");
            }
            return;
        }

        if (saveAs || document.RequiresSaveAs || document.SourcePath is null)
        {
            dialogs.ShowSaveJsonDialog(host.Window!.NativeHandle, document.SourcePath);
            return;
        }

        TrySave(document.SourcePath, true);
    }

    private bool TrySave(string path, bool checkExternal)
    {
        try
        {
            EditorMapPersistence.Save(document!, path, checkExternal);
            validationMessage = "Map validation and persistence: successful";
            log.Add($"Saved map to {path}.");
            ContinuePending();
            return true;
        }
        catch (Exception ex)
        {
            validationMessage = ex.Message;
            log.Add($"Save failed: {ex.Message}");
            logger.LogError(ex, "Editor map save failed.");
            return false;
        }
    }

    private void ProcessDialogResults()
    {
        while (dialogs.TryDequeue(out EditorFileDialogResult result))
        {
            if (result.Error is not null)
            {
                validationMessage = result.Error;
                log.Add($"File dialog failed: {result.Error}");
                continue;
            }

            if (result.Path is null)
                continue;

            if (result.Kind == EditorFileDialogKind.SaveMap)
            {
                TrySave(result.Path, false);
                continue;
            }

            try
            {
                if (result.Kind == EditorFileDialogKind.OpenProject)
                    LoadProject(result.Path, recordRecent: true);
                else if (result.Kind == EditorFileDialogKind.DestinationParent)
                {
                    LoadedRoyaleProject created = pendingOperation == PendingOperation.NewProjectDestination
                        ? RoyaleProjectFactory.Create(result.Path, BufferText(newProjectIdBuffer), BufferText(newProjectNameBuffer))
                        : RoyaleProjectFactory.Convert(document!.SourcePath!, result.Path);
                    LoadProject(created.Paths.Root, recordRecent: true);
                }
                else
                    LoadDocument(result.Path, false);
                pendingOperation = PendingOperation.None;
            }
            catch (Exception ex)
            {
                validationMessage = ex.Message;
                log.Add($"Project operation failed: {ex.Message}");
                logger.LogError(ex, "Editor project operation failed.");
            }
        }
    }

    private void ContinuePending()
    {
        PendingOperation operation = pendingOperation;
        pendingOperation = PendingOperation.None;
        ContinueOperation(operation);
    }

    private void ContinueOperation(PendingOperation operation)
    {
        switch (operation)
        {
            case PendingOperation.OpenProject:
                dialogs.ShowOpenProjectDialog(host.Window!.NativeHandle);
                break;
            case PendingOperation.OpenMap:
                ShowOpenMapDialog();
                break;
            case PendingOperation.Convert:
                pendingOperation = PendingOperation.ConvertDestination;
                dialogs.ShowDestinationParentDialog(host.Window!.NativeHandle);
                break;
            case PendingOperation.NewProject:
                newProjectModalRequested = true;
                break;
            case PendingOperation.Close:
                host.RequestExit();
                break;
        }
    }

    private void BuildNewProjectModal()
    {
        if (newProjectModalRequested)
        {
            Array.Clear(newProjectIdBuffer);
            Array.Clear(newProjectNameBuffer);
            ImguiNative.igOpenPopup_Str("New Project", ImGuiPopupFlags.None);
            newProjectModalRequested = false;
        }

        if (!ImguiNative.igBeginPopupModal("New Project", null, ImGuiWindowFlags.AlwaysAutoResize))
            return;

        fixed (byte* id = newProjectIdBuffer)
            ImguiNative.igInputText("Project ID", id, (uint)newProjectIdBuffer.Length, ImGuiInputTextFlags.None, null, null);
        fixed (byte* name = newProjectNameBuffer)
            ImguiNative.igInputText("Display name", name, (uint)newProjectNameBuffer.Length, ImGuiInputTextFlags.None, null, null);

        bool valid = BufferText(newProjectIdBuffer).Length > 0 && BufferText(newProjectNameBuffer).Length > 0;
        if (ImguiNative.igButton("Choose Parent", new Vector2(120, 0)) && valid)
        {
            pendingOperation = PendingOperation.NewProjectDestination;
            dialogs.ShowDestinationParentDialog(host.Window!.NativeHandle);
            ImguiNative.igCloseCurrentPopup();
        }
        ImguiNative.igSameLine(0, -1);
        if (ImguiNative.igButton("Cancel", new Vector2(90, 0)))
        {
            pendingOperation = PendingOperation.None;
            ImguiNative.igCloseCurrentPopup();
        }
        ImguiNative.igEndPopup();
    }

    private static string BufferText(byte[] buffer)
    {
        int length = Array.IndexOf(buffer, (byte)0);
        return System.Text.Encoding.UTF8.GetString(buffer, 0, length < 0 ? buffer.Length : length).Trim();
    }

    private void BuildUnsavedModal()
    {
        if (pendingOperation == PendingOperation.None)
            return;

        if (modalOpenRequested)
        {
            ImguiNative.igOpenPopup_Str("Unsaved Changes", ImGuiPopupFlags.None);
            modalOpenRequested = false;
        }

        if (!ImguiNative.igBeginPopupModal("Unsaved Changes", null, ImGuiWindowFlags.AlwaysAutoResize))
            return;

        Text("Save changes before continuing?");
        if (ImguiNative.igButton("Save", new Vector2(90, 0)))
            Save(false);

        ImguiNative.igSameLine(0, -1);
        if (ImguiNative.igButton("Discard", new Vector2(90, 0)))
        {
            ImguiNative.igCloseCurrentPopup();
            ContinuePending();
        }

        ImguiNative.igSameLine(0, -1);
        if (ImguiNative.igButton("Cancel", new Vector2(90, 0)))
        {
            pendingOperation = PendingOperation.None;
            ImguiNative.igCloseCurrentPopup();
        }

        ImguiNative.igEndPopup();
    }

    private void ProcessShortcuts()
    {
        EditorKeyboardShortcut shortcut = pendingShortcut;
        pendingShortcut = EditorKeyboardShortcut.None;
        if (shortcut == EditorKeyboardShortcut.None)
            return;

        CommitMapName();
        ImGuiEditorNative.ClearActiveId();
        switch (shortcut)
        {
            case EditorKeyboardShortcut.Open:
                RequestTransition(PendingOperation.OpenProject);
                break;
            case EditorKeyboardShortcut.Save:
                Save(false);
                break;
            case EditorKeyboardShortcut.SaveAs:
                Save(true);
                break;
            case EditorKeyboardShortcut.Undo:
                Undo();
                break;
            case EditorKeyboardShortcut.Redo:
                Redo();
                break;
        }
    }

    private bool QueueShortcut(in SDL_Event e)
    {
        if (e.Type != SDL_EventType.SDL_EVENT_KEY_DOWN || e.key.repeat)
            return false;

        EditorKeyboardShortcut shortcut = EditorKeyboardShortcutResolver.Resolve(e.key.key, e.key.mod);
        if (shortcut == EditorKeyboardShortcut.None)
            return false;

        pendingShortcut = shortcut;
        return true;
    }

    private void UpdateWindowTitle()
    {
        if (document is null || host.Window is null)
            return;

        string filename = projectSession is not null
            ? Path.GetFileName(projectSession.Project.Paths.Root)
            : document.SourcePath is null
            ? document.Map.Id + ".json"
            : Path.GetFileName(document.SourcePath);
        host.Window.SetTitle($"Royale Editor - {filename}{(document.IsDirty ? "*" : string.Empty)}");
    }

    public void Dispose()
    {
        ReleaseViewportInput();
        target?.Dispose();
        imgui?.Dispose();
        gpu?.Dispose();
        host.Dispose();
    }

    private enum PendingOperation
    {
        None,
        OpenProject,
        OpenMap,
        Convert,
        ConvertDestination,
        NewProject,
        NewProjectDestination,
        Close,
    }
}
