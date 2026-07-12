using System.Text;
using Evergine.Bindings.Imgui;
using Evergine.Mathematics;

namespace Royale.Editor.Workspace.Assets;

public sealed unsafe class AssetBrowserRenderer
{
    private const float TileHeight = 124f;
    private const float PreviewInset = 8f;
    private const float LabelTop = 106f;
    private readonly AssetBrowserModel model;
    private readonly IAssetPreviewProvider? previews;
    private readonly byte[] filterBuffer = new byte[128];
    private readonly Action? requestImport;
    private readonly Action? requestFolderMenu;

    public AssetBrowserRenderer(
        AssetBrowserModel model,
        IAssetPreviewProvider? previews = null,
        Action? requestImport = null,
        Action? requestFolderMenu = null)
    {
        this.model = model;
        this.previews = previews;
        this.requestImport = requestImport;
        this.requestFolderMenu = requestFolderMenu;
        int count = Encoding.UTF8.GetBytes(model.Filter, filterBuffer);
        if (count == filterBuffer.Length)
            filterBuffer[^1] = 0;
    }

    public void Render()
    {
        if (ImguiNative.igButton("Import Assets...", new Vector2(118f, 0f)))
            requestImport?.Invoke();
        ImguiNative.igSameLine(0, 6f);
        if (ImguiNative.igButton("Folders...", new Vector2(90f, 0f)))
            requestFolderMenu?.Invoke();
        ImguiNative.igSameLine(0, 8f);
        ImguiNative.igSetNextItemWidth(MathF.Min(260f, ImguiNative.igGetContentRegionAvail().X));
        fixed (byte* buffer = filterBuffer)
        {
            if (ImguiNative.igInputTextWithHint(
                    "##asset-search",
                    "Search assets",
                    buffer,
                    (uint)filterBuffer.Length,
                    ImGuiInputTextFlags.None,
                    null,
                    null))
            {
                model.SetFilter(ReadBuffer(buffer, filterBuffer.Length));
            }
        }

        ImguiNative.igSeparator();
        if (model.Tree is not null)
        {
            ImguiNative.igBeginChild_Str("##asset-folders", new Vector2(180f, 0f), ImGuiChildFlags.Borders, ImGuiWindowFlags.None);
            RenderFolderTree(model.Tree);
            ImguiNative.igEndChild();
            ImguiNative.igSameLine(0, 8f);
            ImguiNative.igBeginChild_Str("##asset-grid", default, ImGuiChildFlags.None, ImGuiWindowFlags.None);
        }
        foreach (string breadcrumb in model.Breadcrumbs)
        {
            string label = breadcrumb.Length == 0 ? "assets" : Path.GetFileName(breadcrumb);
            if (ImguiNative.igSmallButton($"{label}##crumb-{breadcrumb}"))
                model.Navigate(breadcrumb);
            ImguiNative.igSameLine(0, 4f);
        }
        ImguiNative.igNewLine();
        float availableWidth = ImguiNative.igGetContentRegionAvail().X;
        int columns = AssetBrowserModel.CalculateColumns(availableWidth);

        for (int index = 0; index < model.FilteredEntries.Count; index++)
        {
            RenderTile(model.FilteredEntries[index]);
            if ((index + 1) % columns != 0)
                ImguiNative.igSameLine(0, AssetBrowserModel.TileSpacing);
        }
        if (model.Tree is not null)
            ImguiNative.igEndChild();
    }

    private void RenderFolderTree(ProjectAssetNode folder)
    {
        bool hasChildren = folder.Children.Any(child => child.Kind == ProjectAssetNodeKind.Folder);
        ImGuiTreeNodeFlags flags = ImGuiTreeNodeFlags.OpenOnArrow | ImGuiTreeNodeFlags.SpanAvailWidth;
        if (!hasChildren)
            flags |= ImGuiTreeNodeFlags.Leaf;
        if (model.CurrentFolder == folder.RelativePath)
            flags |= ImGuiTreeNodeFlags.Selected;
        bool open = ImguiNative.igTreeNodeEx_Str($"{folder.Name}##{folder.RelativePath}", flags);
        if (ImguiNative.igIsItemClicked(ImGuiMouseButton.Left))
            model.Navigate(folder.RelativePath);
        if (open)
        {
            foreach (ProjectAssetNode child in folder.Children.Where(child => child.Kind == ProjectAssetNodeKind.Folder))
                RenderFolderTree(child);
            ImguiNative.igTreePop();
        }
    }

    private void RenderTile(AssetBrowserEntry entry)
    {
        ImguiNative.igPushID_Str(entry.Id);
        Vector2 origin = ImguiNative.igGetCursorScreenPos();
        ImGuiSelectableFlags flags = entry.IsEnabled
            ? ImGuiSelectableFlags.None
            : ImGuiSelectableFlags.Disabled;
        bool selected = model.SelectedPath == entry.RelativePath;
        bool activated = ImguiNative.igSelectable_Bool(
            "##tile",
            selected,
            flags,
            new Vector2(AssetBrowserModel.TileWidth, TileHeight));
        bool hovered = ImguiNative.igIsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled);
        bool focused = ImguiNative.igIsItemFocused();

        if (activated)
            model.SelectPath(entry.RelativePath);
        if (hovered && entry.Kind == AssetBrowserEntryKind.Folder
            && ImguiNative.igIsMouseDoubleClicked_Nil(ImGuiMouseButton.Left))
            model.Navigate(entry.RelativePath);

        DrawPreview(entry, origin, selected, hovered, focused);
        DrawLabel(entry, origin, hovered);
        ImguiNative.igPopID();
    }

    private void DrawPreview(
        AssetBrowserEntry entry,
        Vector2 origin,
        bool selected,
        bool hovered,
        bool focused)
    {
        Vector2 min = origin + new Vector2(PreviewInset, 4f);
        Vector2 max = min + new Vector2(AssetBrowserModel.PreviewSize, AssetBrowserModel.PreviewSize);
        AssetBrowserPreview preview = entry.Kind == AssetBrowserEntryKind.Model
            ? AssetBrowserPreviewResolver.Resolve(entry, previews)
            : default;

        if (preview.HasTexture)
        {
            ImDrawList* drawList = ImguiNative.igGetWindowDrawList();
            ImguiNative.ImDrawList_AddImage(
                drawList,
                new ImTextureRef { _TexID = (ulong)preview.TextureHandle },
                min,
                max,
                new Vector2(0, 0),
                new Vector2(1, 1),
                uint.MaxValue);
        }
        else
        {
            ImDrawList* drawList = ImguiNative.igGetWindowDrawList();
            uint surface = ImguiNative.igGetColorU32_Col(
                ImGuiCol.FrameBg,
                entry.Kind == AssetBrowserEntryKind.Folder ? .9f : .6f);
            ImguiNative.ImDrawList_AddRectFilled(drawList, min, max, surface, 3f, ImDrawFlags.None);
        }

        if (selected || hovered || focused)
        {
            ImDrawList* drawList = ImguiNative.igGetWindowDrawList();
            ImGuiCol color = selected ? ImGuiCol.CheckMark : hovered ? ImGuiCol.HeaderHovered : ImGuiCol.Text;
            float thickness = selected ? 2f : 1f;
            ImguiNative.ImDrawList_AddRect(
                drawList,
                min,
                max,
                ImguiNative.igGetColorU32_Col(color, 1f),
                3f,
                ImDrawFlags.None,
                thickness);
        }
    }

    private static void DrawLabel(AssetBrowserEntry entry, Vector2 origin, bool hovered)
    {
        Vector2 min = origin + new Vector2(5f, LabelTop);
        Vector2 max = origin + new Vector2(AssetBrowserModel.TileWidth - 5f, TileHeight);
        ImDrawList* drawList = ImguiNative.igGetWindowDrawList();
        uint color = ImguiNative.igGetColorU32_Col(
            entry.IsEnabled ? ImGuiCol.Text : ImGuiCol.TextDisabled,
            1f);
        ImguiNative.ImDrawList_PushClipRect(drawList, min, max, true);
        ImguiNative.ImDrawList_AddText_Vec2(drawList, min, color, entry.Id, null);
        ImguiNative.ImDrawList_PopClipRect(drawList);

        Vector2 labelSize = ImguiNative.igCalcTextSize(entry.Id, null, false, -1f);
        if (hovered && labelSize.X > max.X - min.X && ImguiNative.igBeginTooltip())
        {
            ImguiNative.igTextUnformatted(entry.Id, null);
            ImguiNative.igEndTooltip();
        }
    }

    private static string ReadBuffer(byte* buffer, int capacity)
    {
        int length = 0;
        while (length < capacity && buffer[length] != 0)
            length++;

        return Encoding.UTF8.GetString(buffer, length);
    }
}
