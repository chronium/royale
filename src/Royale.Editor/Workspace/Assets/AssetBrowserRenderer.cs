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

    public AssetBrowserRenderer(AssetBrowserModel model, IAssetPreviewProvider? previews = null)
    {
        this.model = model;
        this.previews = previews;
        int count = Encoding.UTF8.GetBytes(model.Filter, filterBuffer);
        if (count == filterBuffer.Length)
            filterBuffer[^1] = 0;
    }

    public void Render()
    {
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
        float availableWidth = ImguiNative.igGetContentRegionAvail().X;
        int columns = AssetBrowserModel.CalculateColumns(availableWidth);

        for (int index = 0; index < model.FilteredEntries.Count; index++)
        {
            RenderTile(model.FilteredEntries[index]);
            if ((index + 1) % columns != 0)
                ImguiNative.igSameLine(0, AssetBrowserModel.TileSpacing);
        }
    }

    private void RenderTile(AssetBrowserEntry entry)
    {
        ImguiNative.igPushID_Str(entry.Id);
        Vector2 origin = ImguiNative.igGetCursorScreenPos();
        ImGuiSelectableFlags flags = entry.IsEnabled
            ? ImGuiSelectableFlags.None
            : ImGuiSelectableFlags.Disabled;
        bool selected = model.SelectedAssetId == entry.Id;
        bool activated = ImguiNative.igSelectable_Bool(
            "##tile",
            selected,
            flags,
            new Vector2(AssetBrowserModel.TileWidth, TileHeight));
        bool hovered = ImguiNative.igIsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled);
        bool focused = ImguiNative.igIsItemFocused();

        if (activated)
            model.Select(entry.Id);

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
        AssetBrowserPreview preview = AssetBrowserPreviewResolver.Resolve(entry, previews);

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
            uint surface = ImguiNative.igGetColorU32_Col(ImGuiCol.FrameBg, entry.IsEnabled ? 1f : .55f);
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
