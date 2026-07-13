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
using Royale.Editor.Projects.Assets;
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
    private readonly EditorSelectionState selection = new();
    private readonly EditorTransformManipulation manipulation = new();
    private readonly EditorTransformSettingsStore transformSettingsStore = new();
    private readonly IEditorFileDialogService dialogs;
    private readonly EditorUtf8InputBuffer nameInput = new(256);
    private readonly byte[] newProjectIdBuffer = new byte[128];
    private readonly byte[] newProjectNameBuffer = new byte[256];
    private readonly byte[] assetFolderNameBuffer = new byte[128];
    private readonly byte[] assetFolderParentBuffer = new byte[256];

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
    private ProjectAssetPreviewProvider? assetPreviewProvider;
    private StaticMeshAssetCache? meshCache;
    private int frames;
    private ViewportPixelSize requestedSize = new(1, 1);
    private bool viewportHovered;
    private bool windowFocused = true;
    private bool modalOpenRequested;
    private bool newProjectModalRequested;
    private bool importModalRequested;
    private bool folderModalRequested;
    private readonly List<PendingAssetImportState> pendingImports = [];
    private int pendingCollisionRow = -1;
    private Action? pendingAssetOperation;
    private string? importError;
    private bool closeImportModalRequested;
    private readonly EditorDocumentWorkflow documentWorkflow = new();
    private ProjectDestinationOperation projectDestinationOperation;
    private EditorKeyboardShortcut pendingShortcut;
    private string validationMessage = "Runtime map loading: successful";
    private EditorTransformSettings transformSettings;
    private EditorViewportPresentation? viewportPresentation;
    private bool viewportFocused;
    private bool gizmoUsing;
    private bool gizmoHovered;
    private bool mapNameEdited;

    public EditorApplication(EditorLaunchOptions options, ILogger<EditorApplication> logger, IEditorFileDialogService? dialogs = null)
    {
        this.options = options;
        this.logger = logger;
        this.dialogs = dialogs ?? new SdlEditorFileDialogService();
        transformSettings = transformSettingsStore.Read();
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
            ReloadAssetBrowser();

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
        ProcessAssetOperation();
        ProcessDialogResults();
        ProcessShortcuts();
        UpdateWindowTitle();

        bool escape = host.Input.WasKeyPressed((int)SDL_Keycode.SDLK_ESCAPE);
        if (escape && document is not null && manipulation.Cancel(document))
        {
            ImGuiEditorNative.ClearActiveId();
            gizmoUsing = false;
            RebuildScene();
            log.Add("Cancelled entity transform.");
        }
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
                host.CancelExit();
                RequestTransition(EditorDocumentTransition.Close);
                break;
        }
    }

    public void Render(SdlFrameTime time)
    {
        if (gpu is null || imgui is null || target is null || scene is null || document is null || manifest is null || assetBrowserRenderer is null)
            return;

        if (target.Width != requestedSize.Width || target.Height != requestedSize.Height)
            target.Resize(requestedSize.Width, requestedSize.Height);

        viewportPresentation = EditorViewportPresentationBuilder.Build(
            document,
            meshCache!,
            selection.SelectedEditorId,
            transformSettings.GridVisible,
            transformSettings.GridSpacing);
        gpu.RenderOffscreen(target, new RenderFrame(
            camera.ToRenderCamera(),
            scene,
            RenderViewMode.WorldAndDebug,
            viewportPresentation.DebugPrimitives));
        ImGuizmoViewportAdapter.BeginFrame();
        BuildWorkspace(target, document.Map);

        frames++;
        bool capture = options.ScreenshotPath is not null && frames == options.ScreenshotAfterFrames;
        GpuImageReadback? image = gpu.PresentFrame(
            new RenderFrame(camera.ToRenderCamera(), new StaticMeshScene([], []), RenderViewMode.Normal),
            imgui,
            capture);
        assetPreviewProvider?.ProcessFrame();

        if (capture && image is not null)
        {
            PngScreenshotWriter.Save(
                options.ScreenshotPath!,
                image.RgbaBytes,
                image.Width,
                image.Height);
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
            Window(HierarchyName, BuildHierarchy);
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
        {
            viewportHovered = false;
            viewportFocused = false;
        }

        BuildUnsavedModal();
        BuildNewProjectModal();
        BuildImportModal();
        BuildFolderModal();
    }

    private void BuildHierarchy()
    {
        if (document is null)
            return;
        Group("Static Boxes", EditorEntityKind.StaticBox);
        Group("Static Models", EditorEntityKind.StaticModel);
        Group("Spawn Points", EditorEntityKind.SpawnPoint);
        Group("Loot Points", EditorEntityKind.LootPoint);
        Group("Navigation Nodes", EditorEntityKind.NavigationWaypoint);
    }

    private void BuildInspector(GameMap map)
    {
        EditorEntityIdentity? selected = document is null ? null : selection.Resolve(document);
        if (selected is EditorEntityIdentity identity)
        {
            EditorEntityTransform transform = EditorEntityTransforms.Get(document!, identity);
            Text($"Selected: {identity.Kind}");
            Text($"ID: {EditorEntityTransforms.GetDisplayId(document!, identity)}");
            Text($"Position: {Format(transform.Position)}");
            if (EditorEntityTransforms.GetCapabilities(identity.Kind).HasFlag(EditorTransformCapabilities.Rotate))
                Text($"Rotation: {Format(transform.RotationDegrees)} degrees");
            if (identity.Kind == EditorEntityKind.StaticBox)
                Text($"Size: {Format(transform.ScaleOrSize)}");
            else if (identity.Kind == EditorEntityKind.StaticModel)
                Text($"Scale: {Format(transform.ScaleOrSize)}");
            ImguiNative.igSeparator();
        }

        Text($"Map ID: {map.Id}");
        string valueBeforeInput = nameInput.Value;
        fixed (byte* buffer = nameInput.Buffer)
        {
            bool submitted = ImguiNative.igInputText(
                    "Display name",
                    buffer,
                    (uint)nameInput.Capacity,
                    ImGuiInputTextFlags.EnterReturnsTrue,
                    null,
                    null);
            if (!string.Equals(valueBeforeInput, nameInput.Value, StringComparison.Ordinal))
                mapNameEdited = true;
            if (mapNameEdited && (submitted || ImguiNative.igIsItemDeactivatedAfterEdit()))
                CommitMapName();
        }
        if (nameInput.WasTruncated)
            TextWrapped(MapNameTruncationWarning);

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
        if (nameInput.WasTruncated)
            TextWrapped(MapNameTruncationWarning);
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
        BuildViewportToolbar();
        Vector2 available = ImguiNative.igGetContentRegionAvail();
        Vector2 imagePosition = ImguiNative.igGetCursorScreenPos();
        ImGuiIO* io = ImguiNative.igGetIO_Nil();
        requestedSize = ViewportPixelSize.FromLogical(
            available.X,
            available.Y,
            io->DisplayFramebufferScale.X,
            io->DisplayFramebufferScale.Y);
        ImguiNative.igImage(
            new ImTextureRef { _TexID = (ulong)viewport.NativeTextureHandle },
            available,
            new Vector2(0, 0),
            new Vector2(1, 1));
        viewportHovered = ImguiNative.igIsItemHovered(ImGuiHoveredFlags.None);
        viewportFocused = ImguiNative.igIsWindowFocused(ImGuiFocusedFlags.RootAndChildWindows);
        HandleTransformHotkeys();

        gizmoHovered = false;
        EditorEntityIdentity? selected = selection.Resolve(document!);
        if (selected is EditorEntityIdentity identity && !inputOwnership.Captured)
            BuildTransformGizmo(viewport, imagePosition, available, identity);

        if (viewportHovered && ImguiNative.igIsMouseClicked_Bool(ImGuiMouseButton.Left, false) &&
            !gizmoHovered && !gizmoUsing && !manipulation.IsActive)
            PickViewport(io->MousePos, imagePosition, available);
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
            RequestTransition(EditorDocumentTransition.NewProject);
        if (ImguiNative.igMenuItem_Bool("Open Project", "Cmd/Ctrl+O", false, true))
            RequestTransition(EditorDocumentTransition.OpenProject);
        if (ImguiNative.igMenuItem_Bool("Open Map JSON", "", false, true))
            RequestTransition(EditorDocumentTransition.OpenMap);
        if (ImguiNative.igMenuItem_Bool("Convert Map to Project", "", false, projectSession is null && document is not null))
            RequestTransition(EditorDocumentTransition.Convert);
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

    private static void TextWrapped(string value) => ImguiNative.igTextWrapped(value);

    private void Group(string name, EditorEntityKind kind)
    {
        if (!ImguiNative.igCollapsingHeader_TreeNodeFlags(name, ImGuiTreeNodeFlags.DefaultOpen))
            return;

        foreach (EditorEntityIdentity identity in document!.Identities.Where(candidate => candidate.Kind == kind))
        {
            string label = $"{EditorEntityTransforms.GetDisplayId(document, identity)}##{identity.EditorId:N}";
            bool isSelected = selection.SelectedEditorId == identity.EditorId;
            if (ImguiNative.igSelectable_Bool(label, isSelected, ImGuiSelectableFlags.None, default))
                selection.Select(document, identity.EditorId);
        }
    }

    private void HandleTransformHotkeys()
    {
        if (!viewportHovered || !viewportFocused || inputOwnership.Captured || manipulation.IsActive)
            return;
        ImGuiIO* io = ImguiNative.igGetIO_Nil();
        if (io->WantTextInput != 0 || ImguiNative.igIsPopupOpen_Str(null!, ImGuiPopupFlags.AnyPopup))
            return;

        EditorEntityIdentity? selected = selection.Resolve(document!);
        if (selected is not EditorEntityIdentity identity)
            return;
        EditorTransformCapabilities capabilities = EditorEntityTransforms.GetCapabilities(identity.Kind);
        EditorTransformOperation? operation =
            host.Input.WasKeyPressed((int)SDL_Keycode.SDLK_W) ? EditorTransformOperation.Translate :
            host.Input.WasKeyPressed((int)SDL_Keycode.SDLK_E) ? EditorTransformOperation.Rotate :
            host.Input.WasKeyPressed((int)SDL_Keycode.SDLK_R) ? EditorTransformOperation.Scale : null;
        if (operation is null)
            return;
        EditorTransformCapabilities required = operation.Value switch
        {
            EditorTransformOperation.Rotate => EditorTransformCapabilities.Rotate,
            EditorTransformOperation.Scale => EditorTransformCapabilities.Scale,
            _ => EditorTransformCapabilities.Translate,
        };
        if (capabilities.HasFlag(required))
            UpdateTransformSettings(transformSettings with { Operation = operation.Value });
    }

    private void BuildViewportToolbar()
    {
        EditorEntityIdentity? selected = selection.Resolve(document!);
        EditorTransformCapabilities capabilities = selected is EditorEntityIdentity identity
            ? EditorEntityTransforms.GetCapabilities(identity.Kind)
            : EditorTransformCapabilities.None;
        EditorTransformOperation effective = ImGuizmoViewportAdapter.ResolveOperation(transformSettings.Operation, capabilities);

        ToolbarOperation("Translate", EditorTransformOperation.Translate, EditorTransformCapabilities.Translate, effective, capabilities);
        ImguiNative.igSameLine(0, 4);
        ToolbarOperation("Rotate", EditorTransformOperation.Rotate, EditorTransformCapabilities.Rotate, effective, capabilities);
        ImguiNative.igSameLine(0, 4);
        ToolbarOperation("Scale", EditorTransformOperation.Scale, EditorTransformCapabilities.Scale, effective, capabilities);
        ImguiNative.igSameLine(0, 10);

        if (ImguiNative.igButton(transformSettings.Orientation == EditorTransformOrientation.Local ? "Local" : "World", new Vector2(58, 0)))
            UpdateTransformSettings(transformSettings with
            {
                Orientation = transformSettings.Orientation == EditorTransformOrientation.Local
                    ? EditorTransformOrientation.World
                    : EditorTransformOrientation.Local,
            });
        ImguiNative.igSameLine(0, 8);

        byte grid = transformSettings.GridVisible ? (byte)1 : (byte)0;
        if (ImguiNative.igCheckbox("Grid", &grid))
            UpdateTransformSettings(transformSettings with { GridVisible = grid != 0 });
        ImguiNative.igSameLine(0, 8);
        byte snap = transformSettings.SnappingEnabled ? (byte)1 : (byte)0;
        if (ImguiNative.igCheckbox("Snap", &snap))
            UpdateTransformSettings(transformSettings with { SnappingEnabled = snap != 0 });
        ImguiNative.igSameLine(0, 8);

        float increment = transformSettings.GetSnapIncrement(effective);
        ImguiNative.igSetNextItemWidth(90);
        if (ImguiNative.igDragFloat("Increment", &increment, 0.05f, IncrementMinimum(effective), IncrementMaximum(effective), "%.2f", ImGuiSliderFlags.AlwaysClamp))
        {
            EditorTransformSettings updated = effective switch
            {
                EditorTransformOperation.Rotate => transformSettings with { RotationIncrementDegrees = increment },
                EditorTransformOperation.Scale => transformSettings with { ScaleIncrement = increment },
                _ => transformSettings with { GridSpacing = increment },
            };
            UpdateTransformSettings(updated);
        }
    }

    private void ToolbarOperation(
        string label,
        EditorTransformOperation operation,
        EditorTransformCapabilities required,
        EditorTransformOperation effective,
        EditorTransformCapabilities available)
    {
        bool disabled = !available.HasFlag(required) || manipulation.IsActive;
        ImguiNative.igBeginDisabled(disabled);
        if (ImguiNative.igRadioButton_Bool(label, effective == operation))
            UpdateTransformSettings(transformSettings with { Operation = operation });
        ImguiNative.igEndDisabled();
    }

    private void BuildTransformGizmo(
        SdlGpuOffscreenTarget viewport,
        Vector2 imagePosition,
        Vector2 imageSize,
        EditorEntityIdentity identity)
    {
        EditorEntityTransform current = EditorEntityTransforms.Get(document!, identity);
        EditorTransformCapabilities capabilities = EditorEntityTransforms.GetCapabilities(identity.Kind);
        bool wasUsing = gizmoUsing;
        ImGuizmoFrameResult result = ImGuizmoViewportAdapter.Manipulate(
            camera.ToRenderCamera(),
            (uint)viewport.Width,
            (uint)viewport.Height,
            imagePosition,
            imageSize,
            current,
            transformSettings,
            capabilities);

        gizmoUsing = result.IsUsing;
        gizmoHovered = result.IsHovered;
        if (result.IsUsing && !manipulation.IsActive)
            manipulation.Begin(document!, identity);
        if (result.Changed)
        {
            if (!manipulation.IsActive)
                manipulation.Begin(document!, identity);
            manipulation.Preview(document!, result.Transform);
            RebuildScene();
        }
        if (wasUsing && !result.IsUsing && manipulation.IsActive)
        {
            if (manipulation.Complete(document!, out string? error))
                log.Add($"Transformed {EditorEntityTransforms.GetDisplayId(document!, identity)}.");
            else if (error is not null)
            {
                validationMessage = error;
                log.Add($"Transform rejected: {error}");
            }
            RebuildScene();
        }
    }

    private void PickViewport(Vector2 mousePosition, Vector2 imagePosition, Vector2 imageSize)
    {
        if (viewportPresentation is null)
            return;
        EditorRay ray = EditorViewportPicking.CreateRay(
            camera.ToRenderCamera(),
            mousePosition.X - imagePosition.X,
            mousePosition.Y - imagePosition.Y,
            imageSize.X,
            imageSize.Y);
        EditorPickResult? result = EditorViewportPicking.Pick(ray, viewportPresentation.PickTargets);
        if (result is EditorPickResult hit)
            selection.Select(document!, hit.Identity.EditorId);
        else
            selection.Clear();
    }

    private void UpdateTransformSettings(EditorTransformSettings value)
    {
        transformSettings = value;
        try
        {
            transformSettingsStore.Write(value);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            log.Add($"Could not persist editor transform settings: {ex.Message}");
            logger.LogWarning(ex, "Could not persist editor transform settings to {Path}.", transformSettingsStore.Path);
        }
    }

    private static float IncrementMinimum(EditorTransformOperation operation) => operation switch
    {
        EditorTransformOperation.Rotate => EditorTransformSettings.MinimumRotationIncrement,
        EditorTransformOperation.Scale => EditorTransformSettings.MinimumScaleIncrement,
        _ => EditorTransformSettings.MinimumGridSpacing,
    };

    private static float IncrementMaximum(EditorTransformOperation operation) => operation switch
    {
        EditorTransformOperation.Rotate => EditorTransformSettings.MaximumRotationIncrement,
        EditorTransformOperation.Scale => EditorTransformSettings.MaximumScaleIncrement,
        _ => EditorTransformSettings.MaximumGridSpacing,
    };

    private static string Format(System.Numerics.Vector3 value) => $"{value.X:0.###}, {value.Y:0.###}, {value.Z:0.###}";

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
        DisposeAssetPreviewProvider();
        projectSession = null;
        document = candidateDocument;
        manifest = candidateManifest;
        meshCache = candidateCache;
        scene = candidateScene;
        selection.Clear();
        gizmoUsing = false;
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
        DisposeAssetPreviewProvider();
        projectSession = candidate;
        document = candidate.Document;
        manifest = candidate.Project.AssetManifest;
        meshCache = candidateCache;
        scene = candidateScene;
        selection.Clear();
        gizmoUsing = false;
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
        if (assetBrowser is null)
            assetBrowser = projectSession is null
                ? new AssetBrowserModel(manifest!)
                : new AssetBrowserModel(projectSession.Project.Paths.Sources, manifest!);
        else
            assetBrowser.Reload(manifest!);

        DisposeAssetPreviewProvider();
        if (projectSession is not null && gpu is not null && meshCache is not null)
        {
            assetPreviewProvider = new ProjectAssetPreviewProvider(
                gpu,
                meshCache,
                manifest!,
                projectSession.Project.Paths.Sources,
                projectSession.Project.Paths.ThumbnailCache,
                ReportThumbnailFailure);
        }
        assetBrowserRenderer = new AssetBrowserRenderer(
            assetBrowser,
            assetPreviewProvider,
            RequestAssetImport,
            () => folderModalRequested = true);
    }

    private void ReportThumbnailFailure(string message)
    {
        validationMessage = message;
        log.Add(message);
        logger.LogWarning("{ThumbnailFailure}", message);
    }

    private void DisposeAssetPreviewProvider()
    {
        assetPreviewProvider?.Dispose();
        assetPreviewProvider = null;
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

    private const string MapNameTruncationWarning =
        "Warning: The map name exceeds the 255-byte editor field. The original is preserved; editing and committing this field will replace it with the visible UTF-8 prefix.";

    private void SetNameBuffer(string value)
    {
        nameInput.SetValue(value);
        mapNameEdited = false;
    }

    private void CommitMapName()
    {
        if (document is null)
            return;
        if (!mapNameEdited)
            return;

        string value = nameInput.Value;
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
        RebuildScene();
        log.Add($"Undo: {document.RedoDescription}.");
    }

    private void Redo()
    {
        if (document?.Redo() != true)
            return;

        SetNameBuffer(document.Map.Name);
        RebuildScene();
        log.Add($"Redo: {document.UndoDescription}.");
    }

    private void RequestTransition(EditorDocumentTransition transition)
    {
        ApplyDocumentWorkflowResult(documentWorkflow.Request(transition, document?.IsDirty == true));
    }

    private void RequestClose()
    {
        host.CancelExit();
        RequestTransition(EditorDocumentTransition.Close);
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
                ApplyDocumentWorkflowResult(documentWorkflow.SaveSucceeded());
            }
            catch (Exception ex)
            {
                validationMessage = ex.Message;
                log.Add($"Save failed: {ex.Message}");
                logger.LogError(ex, "Editor project save failed.");
                ApplyDocumentWorkflowResult(documentWorkflow.SaveFailed());
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
            ApplyDocumentWorkflowResult(documentWorkflow.SaveSucceeded());
            return true;
        }
        catch (Exception ex)
        {
            validationMessage = ex.Message;
            log.Add($"Save failed: {ex.Message}");
            logger.LogError(ex, "Editor map save failed.");
            ApplyDocumentWorkflowResult(documentWorkflow.SaveFailed());
            return false;
        }
    }

    private void ProcessDialogResults()
    {
        while (dialogs.TryDequeue(out EditorFileDialogResult result))
        {
            if (result.Error is not null)
            {
                if (result.Kind == EditorFileDialogKind.DestinationParent)
                    projectDestinationOperation = ProjectDestinationOperation.None;
                if (result.Kind == EditorFileDialogKind.SaveMap)
                    ApplyDocumentWorkflowResult(documentWorkflow.SaveFailed());
                validationMessage = result.Error;
                log.Add($"File dialog failed: {result.Error}");
                continue;
            }

            if (result.Path is null)
            {
                if (result.Kind == EditorFileDialogKind.DestinationParent)
                    projectDestinationOperation = ProjectDestinationOperation.None;
                if (result.Kind == EditorFileDialogKind.SaveMap)
                    ApplyDocumentWorkflowResult(documentWorkflow.SaveAsCancelled());
                continue;
            }

            if (result.Kind == EditorFileDialogKind.ImportModels)
            {
                IEnumerable<string> paths = result.Paths ?? [result.Path];
                var reserved = manifest!.Assets.Select(asset => asset.Id)
                    .Concat(pendingImports.Select(row => row.AssetId))
                    .ToList();
                foreach (string path in paths)
                {
                    var row = new PendingAssetImportState(path, reserved);
                    pendingImports.Add(row);
                    reserved.Add(row.AssetId);
                }
                importModalRequested = true;
                continue;
            }
            if (result.Kind == EditorFileDialogKind.CollisionModel)
            {
                if (pendingCollisionRow >= 0 && pendingCollisionRow < pendingImports.Count)
                    pendingImports[pendingCollisionRow].SeparateCollisionPath = result.Path;
                pendingCollisionRow = -1;
                importModalRequested = true;
                continue;
            }

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
                    ProjectDestinationOperation destinationOperation = projectDestinationOperation;
                    projectDestinationOperation = ProjectDestinationOperation.None;
                    LoadedRoyaleProject created = destinationOperation switch
                    {
                        ProjectDestinationOperation.NewProject => RoyaleProjectFactory.Create(
                            result.Path,
                            BufferText(newProjectIdBuffer),
                            BufferText(newProjectNameBuffer)),
                        ProjectDestinationOperation.Convert => RoyaleProjectFactory.Convert(document!.SourcePath!, result.Path),
                        _ => throw new InvalidOperationException("A project destination operation was not pending."),
                    };
                    LoadProject(created.Paths.Root, recordRecent: true);
                }
                else
                    LoadDocument(result.Path, false);
            }
            catch (Exception ex)
            {
                if (result.Kind == EditorFileDialogKind.DestinationParent)
                    projectDestinationOperation = ProjectDestinationOperation.None;
                validationMessage = ex.Message;
                log.Add($"Project operation failed: {ex.Message}");
                logger.LogError(ex, "Editor project operation failed.");
            }
        }
    }

    private void ApplyDocumentWorkflowResult(EditorDocumentWorkflowResult result)
    {
        switch (result.Action)
        {
            case EditorDocumentWorkflowAction.ShowUnsavedPrompt:
                modalOpenRequested = true;
                break;
            case EditorDocumentWorkflowAction.SaveDocument:
                Save(saveAs: false);
                break;
            case EditorDocumentWorkflowAction.ContinueTransition:
                ContinueTransition(result.Transition
                    ?? throw new InvalidOperationException("The workflow did not specify a transition to continue."));
                break;
        }
    }

    private void ContinueTransition(EditorDocumentTransition transition)
    {
        switch (transition)
        {
            case EditorDocumentTransition.OpenProject:
                dialogs.ShowOpenProjectDialog(host.Window!.NativeHandle);
                break;
            case EditorDocumentTransition.OpenMap:
                ShowOpenMapDialog();
                break;
            case EditorDocumentTransition.Convert:
                projectDestinationOperation = ProjectDestinationOperation.Convert;
                dialogs.ShowDestinationParentDialog(host.Window!.NativeHandle);
                break;
            case EditorDocumentTransition.NewProject:
                newProjectModalRequested = true;
                break;
            case EditorDocumentTransition.Close:
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
            projectDestinationOperation = ProjectDestinationOperation.NewProject;
            dialogs.ShowDestinationParentDialog(host.Window!.NativeHandle);
            ImguiNative.igCloseCurrentPopup();
        }
        ImguiNative.igSameLine(0, -1);
        if (ImguiNative.igButton("Cancel", new Vector2(90, 0)))
        {
            projectDestinationOperation = ProjectDestinationOperation.None;
            ImguiNative.igCloseCurrentPopup();
        }
        ImguiNative.igEndPopup();
    }

    private void RequestAssetImport()
    {
        if (projectSession is null)
            return;
        pendingImports.Clear();
        importError = null;
        importModalRequested = true;
    }

    private void BuildImportModal()
    {
        if (importModalRequested)
        {
            ImguiNative.igOpenPopup_Str("Import Assets", ImGuiPopupFlags.None);
            importModalRequested = false;
        }
        if (!ImguiNative.igBeginPopupModal("Import Assets", null, ImGuiWindowFlags.AlwaysAutoResize))
            return;

        if (closeImportModalRequested)
        {
            closeImportModalRequested = false;
            ImguiNative.igCloseCurrentPopup();
            ImguiNative.igEndPopup();
            return;
        }

        string destination = assetBrowser?.CurrentFolder ?? string.Empty;
        Text($"Destination: assets/{destination}");
        if (ImguiNative.igButton("Add Files...", new Vector2(110, 0)))
            dialogs.ShowOpenGlbDialog(host.Window!.NativeHandle);

        var ids = manifest!.Assets.Select(asset => asset.Id).ToHashSet(StringComparer.Ordinal);
        bool valid = pendingImports.Any(row => row.Include);
        for (int index = 0; index < pendingImports.Count; index++)
        {
            PendingAssetImportState row = pendingImports[index];
            ImguiNative.igPushID_Int(index);
            byte include = row.Include ? (byte)1 : (byte)0;
            if (ImguiNative.igCheckbox("##include", &include))
                row.Include = include != 0;
            ImguiNative.igSameLine(0, 6);
            Text(Path.GetFileName(row.SourcePath));
            ImguiNative.igSameLine(0, 10);
            ImguiNative.igSetNextItemWidth(180);
            fixed (byte* id = row.AssetIdBuffer)
                ImguiNative.igInputText("Asset ID", id, (uint)row.AssetIdBuffer.Length, ImGuiInputTextFlags.None, null, null);

            string collisionLabel = row.CollisionMode switch
            {
                ModelCollisionMode.None => "None",
                ModelCollisionMode.Convex => "Convex",
                ModelCollisionMode.TriangleMesh => "Triangle Mesh",
                _ => "Separate Mesh",
            };
            if (ImguiNative.igBeginCombo("Collision", collisionLabel, ImGuiComboFlags.None))
            {
                foreach ((ModelCollisionMode mode, string label) in new[]
                {
                    (ModelCollisionMode.None, "None"),
                    (ModelCollisionMode.Convex, "Convex"),
                    (ModelCollisionMode.TriangleMesh, "Triangle Mesh"),
                    (ModelCollisionMode.SeparateMesh, "Separate Mesh"),
                })
                    if (ImguiNative.igSelectable_Bool(label, row.CollisionMode == mode, ImGuiSelectableFlags.None, default))
                        row.CollisionMode = mode;
                ImguiNative.igEndCombo();
            }
            if (row.CollisionMode == ModelCollisionMode.SeparateMesh
                && ImguiNative.igButton("Choose Collision GLB...", new Vector2(160, 0)))
            {
                pendingCollisionRow = index;
                dialogs.ShowOpenGlbDialog(host.Window!.NativeHandle, collisionOnly: true);
            }
            Text($"External resources: {row.ExternalResourceCount}");
            row.Validate(ids);
            if (row.Diagnostic is not null)
            {
                valid = false;
                Text(row.Diagnostic);
            }
            if (ImguiNative.igSmallButton("Remove"))
            {
                pendingImports.RemoveAt(index--);
                ImguiNative.igPopID();
                continue;
            }
            ImguiNative.igSeparator();
            ImguiNative.igPopID();
        }

        if (importError is not null)
            Text(importError);

        bool operationPending = pendingAssetOperation is not null;
        if (ImguiNative.igButton("Import", new Vector2(90, 0)) && valid && !operationPending)
        {
            IReadOnlyList<PendingAssetImport> commands = pendingImports.Select(row => row.ToCommand()).ToList();
            importError = null;
            QueueAssetOperation("Asset import", () =>
            {
                projectSession!.ImportAssets(destination, commands);
                ReloadProjectAssets();
                pendingImports.Clear();
                closeImportModalRequested = true;
            }, keepImportModalOnFailure: true);
        }
        ImguiNative.igSameLine(0, 6);
        if (ImguiNative.igButton("Cancel", new Vector2(90, 0)) && !operationPending)
        {
            pendingImports.Clear();
            ImguiNative.igCloseCurrentPopup();
        }
        ImguiNative.igEndPopup();
    }

    private void ReloadProjectAssets()
    {
        manifest = projectSession!.Project.AssetManifest;
        meshCache = StaticMeshAssetCache.LoadSource(projectSession.Project.Paths.Sources, manifest);
        scene = CreateScene(document!.Map, meshCache);
        ReloadAssetBrowser();
        validationMessage = "Project assets reloaded successfully.";
    }

    private void BuildFolderModal()
    {
        if (folderModalRequested)
        {
            Array.Clear(assetFolderNameBuffer);
            Array.Clear(assetFolderParentBuffer);
            ImguiNative.igOpenPopup_Str("Asset Folders", ImGuiPopupFlags.None);
            folderModalRequested = false;
        }
        if (!ImguiNative.igBeginPopupModal("Asset Folders", null, ImGuiWindowFlags.AlwaysAutoResize))
            return;

        string current = assetBrowser?.CurrentFolder ?? string.Empty;
        Text($"Current: assets/{current}");
        fixed (byte* name = assetFolderNameBuffer)
            ImguiNative.igInputTextWithHint("Name", "lowercase-folder", name, (uint)assetFolderNameBuffer.Length, ImGuiInputTextFlags.None, null, null);
        fixed (byte* parent = assetFolderParentBuffer)
            ImguiNative.igInputTextWithHint("Move parent", "folder/path (empty = assets)", parent, (uint)assetFolderParentBuffer.Length, ImGuiInputTextFlags.None, null, null);

        string nameValue = BufferText(assetFolderNameBuffer);
        string parentValue = BufferText(assetFolderParentBuffer);
        if (ImguiNative.igButton("Create", new Vector2(82, 0)))
            RunFolderCommand(() => projectSession!.CreateAssetFolder(current, nameValue), current);
        ImguiNative.igSameLine(0, 5);
        if (ImguiNative.igButton("Rename", new Vector2(82, 0)) && current.Length > 0)
            RunFolderCommand(() => projectSession!.MoveAssetFolder(current, Path.GetDirectoryName(current)?.Replace('\\', '/') ?? string.Empty, nameValue), null);
        ImguiNative.igSameLine(0, 5);
        if (ImguiNative.igButton("Move", new Vector2(82, 0)) && current.Length > 0)
            RunFolderCommand(() => projectSession!.MoveAssetFolder(current, parentValue), null);
        ImguiNative.igSameLine(0, 5);
        if (ImguiNative.igButton("Delete Empty", new Vector2(105, 0)) && current.Length > 0)
            RunFolderCommand(() => projectSession!.DeleteAssetFolder(current), null);

        Text("Rename/move reject merges. Delete only accepts empty, unreferenced folders.");
        if (ImguiNative.igButton("Close", new Vector2(90, 0)))
            ImguiNative.igCloseCurrentPopup();
        ImguiNative.igEndPopup();
    }

    private void RunFolderCommand(Action command, string? navigateTo)
    {
        QueueAssetOperation("Asset folder operation", () =>
        {
            command();
            ReloadProjectAssets();
            if (navigateTo is not null)
                assetBrowser?.Navigate(navigateTo);
        });
    }

    private void QueueAssetOperation(string name, Action operation, bool keepImportModalOnFailure = false)
    {
        if (pendingAssetOperation is not null)
            return;
        pendingAssetOperation = () =>
        {
            try
            {
                operation();
                validationMessage = $"{name}: successful";
                log.Add($"{name} completed successfully.");
            }
            catch (Exception ex)
            {
                validationMessage = ex.Message;
                log.Add($"{name} failed: {ex.Message}");
                logger.LogError(ex, "{AssetOperation} failed.", name);
                if (keepImportModalOnFailure)
                    importError = ex.Message;
            }
        };
    }

    private void ProcessAssetOperation()
    {
        Action? operation = pendingAssetOperation;
        if (operation is null)
            return;
        pendingAssetOperation = null;
        operation();
    }

    private static string BufferText(byte[] buffer)
    {
        int length = Array.IndexOf(buffer, (byte)0);
        return System.Text.Encoding.UTF8.GetString(buffer, 0, length < 0 ? buffer.Length : length).Trim();
    }

    private void BuildUnsavedModal()
    {
        if (documentWorkflow.State != EditorDocumentWorkflowState.AwaitingUnsavedDecision)
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
        {
            ImguiNative.igCloseCurrentPopup();
            ApplyDocumentWorkflowResult(documentWorkflow.Save());
        }

        ImguiNative.igSameLine(0, -1);
        if (ImguiNative.igButton("Discard", new Vector2(90, 0)))
        {
            ImguiNative.igCloseCurrentPopup();
            ApplyDocumentWorkflowResult(documentWorkflow.Discard());
        }

        ImguiNative.igSameLine(0, -1);
        if (ImguiNative.igButton("Cancel", new Vector2(90, 0)))
        {
            ImguiNative.igCloseCurrentPopup();
            ApplyDocumentWorkflowResult(documentWorkflow.Cancel());
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
                RequestTransition(EditorDocumentTransition.OpenProject);
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
        DisposeAssetPreviewProvider();
        target?.Dispose();
        imgui?.Dispose();
        gpu?.Dispose();
        host.Dispose();
    }

    private enum ProjectDestinationOperation
    {
        None,
        NewProject,
        Convert,
    }
}
