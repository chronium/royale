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
using Royale.Editor.Viewport.FaceSnap;
using Royale.Editor.Workspace;
using Royale.Editor.Workspace.Assets;
using Royale.Platform.Desktop;
using Royale.Rendering;
using Royale.Rendering.Cameras;
using Royale.Rendering.Meshes;
using Royale.Rendering.Platform;
using Royale.Rendering.Screenshots;
using Royale.Rendering.UI;
using Royale.Simulation.World;
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
    private readonly EditorUtf8InputBuffer entityIdInput = new(256);
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
    private EditorFaceSnapSession? faceSnapSession;
    private EditorGizmoFaceSnapSession? gizmoFaceSnapSession;
    private EditorFaceSnapSettings faceSnapSettings = new();
    private bool mapNameEdited;
    private Guid? inspectorEditorId;
    private long inspectorRevision = -1;
    private long rootInspectorRevision = -1;
    private object? inspectorDefinition;
    private MapBounds inspectorBounds = new();
    private SafeZoneDefinition inspectorSafeZone = new();
    private Guid? pendingDeleteEditorId;
    private bool deleteModalRequested;
    private Guid? deletedSelectionForUndo;

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

        if (faceSnapSession is not null &&
            (document is null ||
             selection.SelectedEditorId != faceSnapSession.EditorId ||
             !faceSnapSession.IsDocumentCurrent))
            CancelFaceSnap("Cancelled face snap because the selection or document changed.");

        bool escape = host.Input.WasKeyPressed((int)SDL_Keycode.SDLK_ESCAPE);
        bool rightPressed = host.Input.WasMouseButtonPressed(SDL3.SDL_BUTTON_RIGHT);
        bool faceSnapWasActive = faceSnapSession is not null || gizmoFaceSnapSession is not null;
        if (faceSnapWasActive &&
            (escape || rightPressed))
            CancelFaceSnap(escape ? "Cancelled face snap." : "Cancelled face snap with right click.");
        if (escape && document is not null && manipulation.Cancel(document))
        {
            DisposeGizmoFaceSnap();
            ImGuiEditorNative.ClearActiveId();
            gizmoUsing = false;
            RebuildScene();
            log.Add("Cancelled entity transform.");
        }
        bool right = !faceSnapWasActive && host.Input.IsMouseButtonDown(SDL3.SDL_BUTTON_RIGHT);
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
        if (faceSnapSession?.Hit is MapStaticRayHit faceSnapHit)
            EditorFaceSnapDebug.Add(viewportPresentation.DebugPrimitives, faceSnapHit);
        if (gizmoFaceSnapSession?.Hit is MapStaticRayHit gizmoFaceSnapHit)
            EditorFaceSnapDebug.Add(viewportPresentation.DebugPrimitives, gizmoFaceSnapHit);
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
        BuildDeleteModal();
    }

    private void BuildHierarchy()
    {
        if (document is null)
            return;
        EditorEntityIdentity? selected = selection.Resolve(document);
        ImguiNative.igBeginDisabled(selected is null || selected.Value.Kind == EditorEntityKind.NavigationLink);
        if (ImguiNative.igButton("Duplicate", new Vector2(82, 0)) && selected is EditorEntityIdentity duplicate)
            DuplicateEntity(duplicate);
        ImguiNative.igEndDisabled();
        ImguiNative.igSameLine(0, 6);
        ImguiNative.igBeginDisabled(selected is null);
        if (ImguiNative.igButton("Delete", new Vector2(70, 0)) && selected is EditorEntityIdentity deleted)
        {
            pendingDeleteEditorId = deleted.EditorId;
            deleteModalRequested = true;
        }
        ImguiNative.igEndDisabled();
        Group("Static Boxes", EditorEntityKind.StaticBox);
        Group("Static Models", EditorEntityKind.StaticModel);
        Group("Spawn Points", EditorEntityKind.SpawnPoint);
        Group("Loot Points", EditorEntityKind.LootPoint);
        Group("Navigation Nodes", EditorEntityKind.NavigationWaypoint);
        Group("Navigation Links", EditorEntityKind.NavigationLink);
    }

    private void BuildInspector(GameMap map)
    {
        EditorEntityIdentity? selected = document is null ? null : selection.Resolve(document);
        if (selected is EditorEntityIdentity identity)
        {
            SynchronizeInspector(identity);
            Text($"Selected: {identity.Kind}");
            BuildEntityInspector(identity);
            ImguiNative.igSeparator();
        }
        else
        {
            inspectorEditorId = null;
            inspectorDefinition = null;
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

        SynchronizeRootInspector();
        Vector3 boundsMin = ToUi(inspectorBounds.Min);
        if (ImguiNative.igDragFloat3("Bounds minimum", &boundsMin, .1f, 0, 0, "%.3f", ImGuiSliderFlags.None))
            inspectorBounds = inspectorBounds with { Min = ToMap(boundsMin) };
        if (ImguiNative.igIsItemDeactivatedAfterEdit())
            TryExecute(() => new SetWorldBoundsCommand(map.WorldBounds, inspectorBounds));
        Vector3 boundsMax = ToUi(inspectorBounds.Max);
        if (ImguiNative.igDragFloat3("Bounds maximum", &boundsMax, .1f, 0, 0, "%.3f", ImGuiSliderFlags.None))
            inspectorBounds = inspectorBounds with { Max = ToMap(boundsMax) };
        if (ImguiNative.igIsItemDeactivatedAfterEdit())
            TryExecute(() => new SetWorldBoundsCommand(map.WorldBounds, inspectorBounds));
        Vector3 safeCenter = ToUi(inspectorSafeZone.Center);
        if (ImguiNative.igDragFloat3("Safe-zone centre", &safeCenter, .1f, 0, 0, "%.3f", ImGuiSliderFlags.None))
            inspectorSafeZone = inspectorSafeZone with { Center = ToMap(safeCenter) };
        if (ImguiNative.igIsItemDeactivatedAfterEdit())
            TryExecute(() => new SetSafeZoneCommand(map.SafeZone, inspectorSafeZone));
        float safeRadius = inspectorSafeZone.Radius;
        if (ImguiNative.igDragFloat("Safe-zone radius", &safeRadius, .1f, 0, 0, "%.3f", ImGuiSliderFlags.None))
            inspectorSafeZone = inspectorSafeZone with { Radius = safeRadius };
        if (ImguiNative.igIsItemDeactivatedAfterEdit())
            TryExecute(() => new SetSafeZoneCommand(map.SafeZone, inspectorSafeZone));

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

    private void SynchronizeInspector(EditorEntityIdentity identity)
    {
        if (inspectorEditorId == identity.EditorId && inspectorRevision == document!.Revision)
            return;
        inspectorEditorId = identity.EditorId;
        inspectorRevision = document!.Revision;
        inspectorDefinition = document.GetDefinition(identity.EditorId);
        string id = inspectorDefinition switch
        {
            StaticBoxDefinition value => value.Id,
            StaticModelDefinition value => value.Id,
            MapSpawnPoint value => value.Id,
            MapLootPoint value => value.Id,
            MapNavigationWaypoint value => value.Id,
            _ => string.Empty,
        };
        entityIdInput.SetValue(id);
    }

    private void SynchronizeRootInspector()
    {
        if (rootInspectorRevision == document!.Revision)
            return;
        rootInspectorRevision = document.Revision;
        inspectorBounds = document.Map.WorldBounds;
        inspectorSafeZone = document.Map.SafeZone;
    }

    private void BuildEntityInspector(EditorEntityIdentity identity)
    {
        if (identity.Kind != EditorEntityKind.NavigationLink)
        {
            bool submitted;
            fixed (byte* buffer = entityIdInput.Buffer)
                submitted = ImguiNative.igInputText("ID", buffer, (uint)entityIdInput.Capacity, ImGuiInputTextFlags.EnterReturnsTrue, null, null);
            if (submitted || ImguiNative.igIsItemDeactivatedAfterEdit())
            {
                inspectorDefinition = inspectorDefinition switch
                {
                    StaticBoxDefinition value => value with { Id = entityIdInput.Value },
                    StaticModelDefinition value => value with { Id = entityIdInput.Value },
                    MapSpawnPoint value => value with { Id = entityIdInput.Value },
                    MapLootPoint value => value with { Id = entityIdInput.Value },
                    MapNavigationWaypoint value => value with { Id = entityIdInput.Value },
                    _ => inspectorDefinition,
                };
                CommitEntityDefinition(identity);
            }
        }

        switch (inspectorDefinition)
        {
            case StaticBoxDefinition box:
                EditVector("Position", box.Position, value => inspectorDefinition = box with { Position = value }, identity);
                box = (StaticBoxDefinition)inspectorDefinition;
                EditVector("Rotation", box.RotationEuler, value => inspectorDefinition = box with { RotationEuler = value }, identity);
                box = (StaticBoxDefinition)inspectorDefinition;
                EditVector("Size", box.Size, value => inspectorDefinition = box with { Size = value }, identity);
                break;
            case StaticModelDefinition model:
                if (ImguiNative.igBeginCombo("Asset", model.AssetId, ImGuiComboFlags.None))
                {
                    foreach (ModelAssetDefinition candidate in manifest!.Assets.Where(value => value.Render is not null).OrderBy(value => value.Id, StringComparer.Ordinal))
                    {
                        if (ImguiNative.igSelectable_Bool(candidate.Id, candidate.Id == model.AssetId, ImGuiSelectableFlags.None, default))
                        {
                            inspectorDefinition = model with { AssetId = candidate.Id };
                            CommitEntityDefinition(identity);
                        }
                    }
                    ImguiNative.igEndCombo();
                }
                model = (StaticModelDefinition)inspectorDefinition;
                EditVector("Position", model.Position, value => inspectorDefinition = model with { Position = value }, identity);
                model = (StaticModelDefinition)inspectorDefinition;
                EditVector("Rotation", model.RotationEuler, value => inspectorDefinition = model with { RotationEuler = value }, identity);
                model = (StaticModelDefinition)inspectorDefinition;
                EditVector("Scale", model.Scale, value => inspectorDefinition = model with { Scale = value }, identity);
                break;
            case MapSpawnPoint spawn:
                EditVector("Position", spawn.Position, value => inspectorDefinition = spawn with { Position = value }, identity);
                spawn = (MapSpawnPoint)inspectorDefinition;
                EditVector("Rotation", spawn.RotationEuler, value => inspectorDefinition = spawn with { RotationEuler = value }, identity);
                break;
            case MapLootPoint loot:
                EditVector("Position", loot.Position, value => inspectorDefinition = loot with { Position = value }, identity);
                break;
            case MapNavigationWaypoint waypoint:
                EditVector("Position", waypoint.Position, value => inspectorDefinition = waypoint with { Position = value }, identity);
                break;
            case MapNavigationLink link:
                BuildEndpointCombo("From", identity, link, true);
                link = (MapNavigationLink)inspectorDefinition;
                BuildEndpointCombo("To", identity, link, false);
                break;
        }
    }

    private void EditVector(string label, MapVector3 current, Action<MapVector3> update, EditorEntityIdentity identity)
    {
        Vector3 value = ToUi(current);
        if (ImguiNative.igDragFloat3(label, &value, .05f, 0, 0, "%.3f", ImGuiSliderFlags.None))
            update(ToMap(value));
        if (ImguiNative.igIsItemDeactivatedAfterEdit())
            CommitEntityDefinition(identity);
    }

    private void BuildEndpointCombo(string label, EditorEntityIdentity identity, MapNavigationLink link, bool from)
    {
        string current = from ? link.From : link.To;
        if (!ImguiNative.igBeginCombo(label, current, ImGuiComboFlags.None))
            return;
        foreach (MapNavigationWaypoint waypoint in document!.Map.Navigation.Waypoints)
        {
            if (ImguiNative.igSelectable_Bool(waypoint.Id, waypoint.Id == current, ImGuiSelectableFlags.None, default))
            {
                inspectorDefinition = from ? link with { From = waypoint.Id } : link with { To = waypoint.Id };
                CommitEntityDefinition(identity);
            }
        }
        ImguiNative.igEndCombo();
    }

    private void CommitEntityDefinition(EditorEntityIdentity identity)
    {
        object before = document!.GetDefinition(identity.EditorId);
        object after = inspectorDefinition!;
        if (Equals(before, after))
            return;
        if (before is MapNavigationWaypoint oldWaypoint && after is MapNavigationWaypoint newWaypoint &&
            !string.Equals(oldWaypoint.Id, newWaypoint.Id, StringComparison.Ordinal))
        {
            TryExecute(() => new RenameWaypointCommand(identity.EditorId, oldWaypoint.Id, newWaypoint.Id));
            return;
        }
        TryExecute(() => new ReplaceEntityCommand(identity.EditorId, before, after));
    }

    private void TryExecute(Func<IEditorDocumentCommand> commandFactory, Guid? select = null)
    {
        CancelFaceSnap("Cancelled face snap because the document changed.");
        try
        {
            IEditorDocumentCommand command = commandFactory();
            document!.Execute(command);
            if (select is Guid editorId)
                selection.Select(document, editorId);
            RebuildScene();
            RefreshValidation();
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or KeyNotFoundException)
        {
            validationMessage = ex.Message;
            log.Add($"Edit rejected: {ex.Message}");
            inspectorRevision = -1;
            rootInspectorRevision = -1;
        }
    }

    private static Vector3 ToUi(MapVector3 value) => new(value.X, value.Y, value.Z);
    private static MapVector3 ToMap(Vector3 value) => new(value.X, value.Y, value.Z);
    private static MapVector3 ToMap(System.Numerics.Vector3 value) => new(value.X, value.Y, value.Z);

    private void RefreshValidation()
    {
        try
        {
            MapCatalog.Validate(document!.Map);
            validationMessage = "In-memory map validation: successful";
        }
        catch (Exception ex) when (ex is InvalidDataException or ArgumentException)
        {
            validationMessage = ex.Message;
        }
    }

    private void BuildLog()
    {
        foreach (string entry in log.Entries)
            Text(entry);
    }

    private void BuildViewport(SdlGpuOffscreenTarget viewport)
    {
        if (faceSnapSession is not null &&
            (selection.SelectedEditorId != faceSnapSession.EditorId || !faceSnapSession.IsDocumentCurrent))
            CancelFaceSnap("Cancelled face snap because the selection or document changed.");
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
        if (ImguiNative.igBeginDragDropTarget())
        {
            ImGuiPayload* payload = ImguiNative.igAcceptDragDropPayload("ROYALE_MODEL_ASSET", ImGuiDragDropFlags.None);
            if (payload is not null && payload->Delivery != 0)
            {
                int length = Math.Max(0, payload->DataSize - 1);
                string assetId = System.Text.Encoding.UTF8.GetString(new ReadOnlySpan<byte>(payload->Data, length));
                EditorRay ray = EditorViewportPicking.CreateRay(
                    camera.ToRenderCamera(),
                    io->MousePos.X - imagePosition.X,
                    io->MousePos.Y - imagePosition.Y,
                    available.X,
                    available.Y);
                PlaceModel(assetId, ray);
            }
            ImguiNative.igEndDragDropTarget();
        }
        if (faceSnapSession is null)
            HandleTransformHotkeys();

        if (faceSnapSession is not null)
        {
            HandleFaceSnap(io->MousePos, imagePosition, available);
            return;
        }

        gizmoHovered = false;
        EditorEntityIdentity? selected = selection.Resolve(document!);
        if (selected is EditorEntityIdentity identity &&
            EditorEntityTransforms.HasSpatialTransform(identity.Kind) &&
            !inputOwnership.Captured)
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

        if (ImguiNative.igSmallButton($"+ Add##{kind}"))
            AddEntity(kind);

        foreach (EditorEntityIdentity identity in document!.Identities.Where(candidate => candidate.Kind == kind))
        {
            string label = $"{EditorEntityTransforms.GetDisplayId(document, identity)}##{identity.EditorId:N}";
            bool isSelected = selection.SelectedEditorId == identity.EditorId;
            if (ImguiNative.igSelectable_Bool(label, isSelected, ImGuiSelectableFlags.None, default))
            {
                CancelFaceSnap("Cancelled face snap because the selection changed.");
                selection.Select(document, identity.EditorId);
            }
        }
    }

    private void AddEntity(EditorEntityKind kind)
    {
        if (document is null)
            return;
        MapVector3 position = ToMap(ResolvePlacement(null));
        object? definition = kind switch
        {
            EditorEntityKind.StaticBox => new StaticBoxDefinition
            {
                Id = EditorMapEditing.CreateUniqueId(document, kind, "box"),
                Position = position,
                Size = new MapVector3(1, 1, 1),
            },
            EditorEntityKind.StaticModel => CreateModelDefinition(
                assetBrowser?.SelectedAssetId ?? manifest?.Assets.FirstOrDefault(asset => asset.Render is not null)?.Id,
                position),
            EditorEntityKind.SpawnPoint => new MapSpawnPoint
            {
                Id = EditorMapEditing.CreateUniqueId(document, kind, "spawn"),
                Position = position,
            },
            EditorEntityKind.LootPoint => new MapLootPoint
            {
                Id = EditorMapEditing.CreateUniqueId(document, kind, "loot"),
                Position = position,
            },
            EditorEntityKind.NavigationWaypoint => new MapNavigationWaypoint
            {
                Id = EditorMapEditing.CreateUniqueId(document, kind, "waypoint"),
                Position = position,
            },
            EditorEntityKind.NavigationLink => CreateAvailableLink(),
            _ => null,
        };
        if (definition is null)
        {
            validationMessage = kind == EditorEntityKind.StaticModel
                ? "No render-capable model asset is available."
                : "No valid pair of unlinked navigation waypoints is available.";
            log.Add(validationMessage);
            return;
        }

        Guid editorId = Guid.NewGuid();
        int index = EntityCount(kind);
        TryExecute(() => new AddEntityCommand(kind, index, definition, editorId), editorId);
    }

    private StaticModelDefinition? CreateModelDefinition(string? assetId, MapVector3 position)
    {
        if (assetId is null || manifest?.Assets.Any(asset => asset.Id == assetId && asset.Render is not null) != true)
            return null;
        return new StaticModelDefinition
        {
            Id = EditorMapEditing.CreateUniqueId(document!, EditorEntityKind.StaticModel, assetId),
            AssetId = assetId,
            Position = position,
            Scale = new MapVector3(1, 1, 1),
        };
    }

    private void PlaceModelAtViewportCenter(string assetId)
    {
        float width = Math.Max(1, requestedSize.Width);
        float height = Math.Max(1, requestedSize.Height);
        EditorRay ray = EditorViewportPicking.CreateRay(camera.ToRenderCamera(), width * .5f, height * .5f, width, height);
        PlaceModel(assetId, ray);
    }

    private void PlaceModel(string assetId, EditorRay ray)
    {
        StaticModelDefinition? definition = CreateModelDefinition(assetId, ToMap(ResolvePlacement(ray)));
        if (definition is null)
        {
            validationMessage = $"Asset '{assetId}' is not render-capable.";
            log.Add(validationMessage);
            return;
        }
        Guid editorId = Guid.NewGuid();
        TryExecute(
            () => new AddEntityCommand(EditorEntityKind.StaticModel, document!.Map.StaticModels.Count, definition, editorId),
            editorId);
    }

    private System.Numerics.Vector3 ResolvePlacement(EditorRay? ray)
    {
        float width = Math.Max(1, requestedSize.Width);
        float height = Math.Max(1, requestedSize.Height);
        EditorRay placementRay = ray ?? EditorViewportPicking.CreateRay(
            camera.ToRenderCamera(),
            width * .5f,
            height * .5f,
            width,
            height);
        return EditorPlacementResolver.Resolve(
            placementRay,
            document!.Map.WorldBounds,
            transformSettings.SnappingEnabled,
            transformSettings.GridSpacing);
    }

    private MapNavigationLink? CreateAvailableLink()
    {
        IReadOnlyList<MapNavigationWaypoint> waypoints = document!.Map.Navigation.Waypoints;
        for (int first = 0; first < waypoints.Count; first++)
        for (int second = first + 1; second < waypoints.Count; second++)
        {
            var candidate = new MapNavigationLink { From = waypoints[first].Id, To = waypoints[second].Id };
            try
            {
                EditorMapEditing.ValidateDefinition(document, EditorEntityKind.NavigationLink, candidate);
                return candidate;
            }
            catch (ArgumentException)
            {
            }
        }
        return null;
    }

    private void DuplicateEntity(EditorEntityIdentity identity)
    {
        object duplicate = EditorMapEditing.DuplicateDefinition(document!, identity);
        Guid editorId = Guid.NewGuid();
        TryExecute(() => new AddEntityCommand(identity.Kind, identity.Index + 1, duplicate, editorId), editorId);
    }

    private int EntityCount(EditorEntityKind kind) => kind switch
    {
        EditorEntityKind.StaticBox => document!.Map.StaticBoxes.Count,
        EditorEntityKind.StaticModel => document!.Map.StaticModels.Count,
        EditorEntityKind.SpawnPoint => document!.Map.SpawnPoints.Count,
        EditorEntityKind.LootPoint => document!.Map.LootPoints.Count,
        EditorEntityKind.NavigationWaypoint => document!.Map.Navigation.Waypoints.Count,
        EditorEntityKind.NavigationLink => document!.Map.Navigation.Links.Count,
        _ => 0,
    };

    private void BuildDeleteModal()
    {
        if (deleteModalRequested)
        {
            ImguiNative.igOpenPopup_Str("Delete Entity", ImGuiPopupFlags.None);
            deleteModalRequested = false;
        }
        if (!ImguiNative.igBeginPopupModal("Delete Entity", null, ImGuiWindowFlags.AlwaysAutoResize))
            return;
        EditorEntityIdentity? identity = pendingDeleteEditorId is Guid editorId && document!.TryGetIdentity(editorId, out EditorEntityIdentity found)
            ? found
            : null;
        if (identity is EditorEntityIdentity value)
        {
            Text($"Delete {EditorEntityTransforms.GetDisplayId(document!, value)}?");
            if (value.Kind == EditorEntityKind.NavigationWaypoint)
            {
                string id = document!.Map.Navigation.Waypoints[value.Index].Id;
                int incident = document.Map.Navigation.Links.Count(link => link.From == id || link.To == id);
                Text($"This also removes {incident} incident link{(incident == 1 ? string.Empty : "s")}.");
            }
            if (ImguiNative.igButton("Delete", new Vector2(90, 0)))
            {
                Guid deletedId = value.EditorId;
                TryExecute(() => new RemoveEntityCommand(deletedId));
                deletedSelectionForUndo = deletedId;
                selection.Clear();
                pendingDeleteEditorId = null;
                ImguiNative.igCloseCurrentPopup();
            }
            ImguiNative.igSameLine(0, 8);
        }
        if (ImguiNative.igButton("Cancel", new Vector2(90, 0)))
        {
            pendingDeleteEditorId = null;
            ImguiNative.igCloseCurrentPopup();
        }
        ImguiNative.igEndPopup();
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
        bool canFaceSnap = selected is EditorEntityIdentity selectedIdentity &&
            EditorEntityTransforms.HasSpatialTransform(selectedIdentity.Kind);

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
        byte face = transformSettings.FaceSnappingEnabled ? (byte)1 : (byte)0;
        ImguiNative.igBeginDisabled(
            effective != EditorTransformOperation.Translate ||
            !canFaceSnap ||
            manipulation.IsActive ||
            faceSnapSession is not null);
        if (ImguiNative.igCheckbox("Face", &face))
            UpdateTransformSettings(transformSettings with { FaceSnappingEnabled = face != 0 });
        ImguiNative.igEndDisabled();
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

        ImguiNative.igSameLine(0, 10);
        ImguiNative.igBeginDisabled(!canFaceSnap || manipulation.IsActive);
        if (ImguiNative.igButton(faceSnapSession is null ? "Face Snap" : "Cancel Face Snap", new Vector2(116, 0)))
        {
            if (faceSnapSession is null && selected is EditorEntityIdentity targetIdentity)
                BeginFaceSnap(targetIdentity);
            else
                CancelFaceSnap("Cancelled face snap.");
        }
        ImguiNative.igEndDisabled();

        if (faceSnapSession is not null && selected is EditorEntityIdentity faceSnapIdentity)
            BuildFaceSnapAlignmentControls(faceSnapIdentity);
    }

    private void BuildFaceSnapAlignmentControls(EditorEntityIdentity identity)
    {
        bool supportsAlignment = identity.Kind is not EditorEntityKind.LootPoint and not EditorEntityKind.NavigationWaypoint;
        ImguiNative.igSameLine(0, 8);
        byte align = faceSnapSettings.AlignmentEnabled && supportsAlignment ? (byte)1 : (byte)0;
        ImguiNative.igBeginDisabled(!supportsAlignment);
        if (ImguiNative.igCheckbox("Align", &align))
            faceSnapSettings = faceSnapSettings with { AlignmentEnabled = align != 0 };
        ImguiNative.igSameLine(0, 4);
        string axisLabel = FaceSnapAxisLabel(faceSnapSettings.AlignmentAxis);
        ImguiNative.igSetNextItemWidth(62);
        if (ImguiNative.igBeginCombo("##FaceSnapAxis", axisLabel, ImGuiComboFlags.None))
        {
            foreach (EditorFaceSnapAxis axis in Enum.GetValues<EditorFaceSnapAxis>())
            {
                if (ImguiNative.igSelectable_Bool(
                    FaceSnapAxisLabel(axis),
                    faceSnapSettings.AlignmentAxis == axis,
                    ImGuiSelectableFlags.None,
                    default))
                    faceSnapSettings = faceSnapSettings with { AlignmentAxis = axis };
            }
            ImguiNative.igEndCombo();
        }
        ImguiNative.igEndDisabled();
    }

    private static string FaceSnapAxisLabel(EditorFaceSnapAxis axis) => axis switch
    {
        EditorFaceSnapAxis.PositiveX => "+X",
        EditorFaceSnapAxis.NegativeX => "-X",
        EditorFaceSnapAxis.PositiveY => "+Y",
        EditorFaceSnapAxis.NegativeY => "-Y",
        EditorFaceSnapAxis.PositiveZ => "+Z",
        EditorFaceSnapAxis.NegativeZ => "-Z",
        _ => throw new ArgumentOutOfRangeException(nameof(axis)),
    };

    private void BeginFaceSnap(EditorEntityIdentity identity)
    {
        MapStaticCollisionWorld? collisionWorld = null;
        try
        {
            EditorPickTarget bounds = EditorViewportPresentationBuilder.CreatePickTarget(document!, meshCache!, identity);
            collisionWorld = EditorFaceSnapCollisionWorldFactory.Create(document!, projectSession);
            faceSnapSession = new EditorFaceSnapSession(document!, identity, bounds, collisionWorld);
            collisionWorld = null;
            ReleaseViewportInput();
            log.Add($"Face snap started for {EditorEntityTransforms.GetDisplayId(document!, identity)}.");
        }
        catch (Exception ex)
        {
            collisionWorld?.Dispose();
            ReportFaceSnapFailure("Could not start face snap", ex);
        }
    }

    private void HandleFaceSnap(Vector2 mousePosition, Vector2 imagePosition, Vector2 imageSize)
    {
        if (faceSnapSession is null || !viewportHovered)
            return;
        try
        {
            EditorRay ray = EditorViewportPicking.CreateRay(
                camera.ToRenderCamera(),
                mousePosition.X - imagePosition.X,
                mousePosition.Y - imagePosition.Y,
                imageSize.X,
                imageSize.Y);
            faceSnapSession.TryPreview(ray, faceSnapSettings);
            RebuildScene();
            if (ImguiNative.igIsMouseClicked_Bool(ImGuiMouseButton.Left, false) && faceSnapSession.HasPreview)
            {
                Guid editorId = faceSnapSession.EditorId;
                bool committed = faceSnapSession.Commit();
                faceSnapSession = null;
                RebuildScene();
                if (committed)
                {
                    EditorEntityIdentity identity = document!.GetIdentity(editorId);
                    log.Add($"Face snapped {EditorEntityTransforms.GetDisplayId(document, identity)}.");
                    RefreshValidation();
                }
            }
        }
        catch (Exception ex)
        {
            ReportFaceSnapFailure("Face snap failed", ex);
        }
    }

    private void CancelFaceSnap(string message)
    {
        bool cancelled = false;
        if (faceSnapSession is not null)
        {
            faceSnapSession.Cancel();
            faceSnapSession = null;
            cancelled = true;
        }
        if (gizmoFaceSnapSession is not null)
        {
            if (document is not null && manipulation.Cancel(document))
            {
                ImGuiEditorNative.ClearActiveId();
                gizmoUsing = false;
            }
            DisposeGizmoFaceSnap();
            cancelled = true;
        }
        if (!cancelled)
            return;

        RebuildScene();
        log.Add(message);
    }

    private void ReportFaceSnapFailure(string prefix, Exception exception)
    {
        faceSnapSession?.Cancel();
        faceSnapSession = null;
        RebuildScene();
        validationMessage = $"{prefix}: {exception.Message}";
        log.Add(validationMessage);
        logger.LogError(exception, "{FaceSnapFailure}", validationMessage);
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
        EditorEntityTransform current = gizmoFaceSnapSession?.Candidate ??
            EditorEntityTransforms.Get(document!, identity);
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
        EditorTransformOperation effectiveOperation =
            ImGuizmoViewportAdapter.ResolveOperation(transformSettings.Operation, capabilities);
        if (result.IsUsing && !manipulation.IsActive)
        {
            manipulation.Begin(document!, identity);
            if (transformSettings.FaceSnappingEnabled &&
                effectiveOperation == EditorTransformOperation.Translate &&
                result.TranslationConstraint != EditorTranslationConstraint.None)
            {
                if (!BeginGizmoFaceSnap(identity))
                {
                    manipulation.Cancel(document!);
                    ImGuiEditorNative.ClearActiveId();
                    gizmoUsing = false;
                    RebuildScene();
                    return;
                }
            }
        }
        else if (result.Changed && !manipulation.IsActive)
        {
            manipulation.Begin(document!, identity);
        }
        if (result.Changed && gizmoFaceSnapSession is not null)
            gizmoFaceSnapSession.UpdateCandidate(result.Transform);

        EditorEntityTransform preview = result.Transform;
        bool hasPreview = result.Changed;
        if ((result.IsUsing || wasUsing) && gizmoFaceSnapSession is not null)
        {
            try
            {
                ImGuiIO* io = ImguiNative.igGetIO_Nil();
                EditorRay ray = EditorViewportPicking.CreateRay(
                    camera.ToRenderCamera(),
                    io->MousePos.X - imagePosition.X,
                    io->MousePos.Y - imagePosition.Y,
                    imageSize.X,
                    imageSize.Y);
                gizmoFaceSnapSession.TrySnap(
                    ray,
                    result.TranslationConstraint,
                    transformSettings.Orientation,
                    out preview);
                hasPreview = true;
            }
            catch (Exception ex)
            {
                manipulation.Cancel(document!);
                DisposeGizmoFaceSnap();
                ImGuiEditorNative.ClearActiveId();
                gizmoUsing = false;
                UpdateTransformSettings(transformSettings with { FaceSnappingEnabled = false });
                validationMessage = $"Gizmo face snap failed: {ex.Message}";
                log.Add(validationMessage);
                logger.LogError(ex, "Gizmo face snap failed during a translate manipulation.");
                RebuildScene();
                return;
            }
        }
        if (hasPreview)
        {
            manipulation.Preview(document!, preview);
            RebuildScene();
        }
        if (wasUsing && !result.IsUsing && manipulation.IsActive)
        {
            if (manipulation.Complete(document!, out string? error))
            {
                log.Add($"Transformed {EditorEntityTransforms.GetDisplayId(document!, identity)}.");
                RefreshValidation();
            }
            else if (error is not null)
            {
                validationMessage = error;
                log.Add($"Transform rejected: {error}");
            }
            DisposeGizmoFaceSnap();
            RebuildScene();
        }
    }

    private bool BeginGizmoFaceSnap(EditorEntityIdentity identity)
    {
        MapStaticCollisionWorld? collisionWorld = null;
        try
        {
            EditorPickTarget bounds = EditorViewportPresentationBuilder.CreatePickTarget(document!, meshCache!, identity);
            collisionWorld = EditorFaceSnapCollisionWorldFactory.Create(document!, projectSession);
            gizmoFaceSnapSession = new EditorGizmoFaceSnapSession(document!, identity, bounds, collisionWorld);
            collisionWorld = null;
            return true;
        }
        catch (Exception ex)
        {
            collisionWorld?.Dispose();
            UpdateTransformSettings(transformSettings with { FaceSnappingEnabled = false });
            validationMessage = $"Could not start gizmo face snap: {ex.Message}";
            log.Add(validationMessage);
            logger.LogError(ex, "Gizmo face snap initialization failed.");
            return false;
        }
    }

    private void DisposeGizmoFaceSnap()
    {
        gizmoFaceSnapSession?.Dispose();
        gizmoFaceSnapSession = null;
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
        CancelFaceSnap("Cancelled face snap because the document changed.");
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
        CancelFaceSnap("Cancelled face snap because the document changed.");
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
            () => folderModalRequested = true,
            PlaceModelAtViewportCenter);
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

        CancelFaceSnap("Cancelled face snap because the document changed.");
        document.Execute(new SetMapNameCommand(document.Map.Name, value));
        SetNameBuffer(document.Map.Name);
        RefreshValidation();
        log.Add("Renamed map.");
    }

    private void Undo()
    {
        CancelFaceSnap("Cancelled face snap before undo.");
        if (document?.Undo() != true)
            return;

        SetNameBuffer(document.Map.Name);
        RebuildScene();
        if (deletedSelectionForUndo is Guid editorId && document.TryGetIdentity(editorId, out _))
            selection.Select(document, editorId);
        RefreshValidation();
        log.Add($"Undo: {document.RedoDescription}.");
    }

    private void Redo()
    {
        CancelFaceSnap("Cancelled face snap before redo.");
        if (document?.Redo() != true)
            return;

        SetNameBuffer(document.Map.Name);
        RebuildScene();
        if (selection.Resolve(document) is null)
            selection.Clear();
        RefreshValidation();
        log.Add($"Redo: {document.UndoDescription}.");
    }

    private void RequestTransition(EditorDocumentTransition transition)
    {
        CancelFaceSnap("Cancelled face snap because a document transition was requested.");
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

        CancelFaceSnap("Cancelled face snap before save.");

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
        faceSnapSession?.Dispose();
        faceSnapSession = null;
        DisposeGizmoFaceSnap();
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
