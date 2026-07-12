namespace Royale.Editor.Workspace;

public sealed class EditorWorkspaceState
{
    public bool HierarchyVisible { get; set; } = true;
    public bool InspectorVisible { get; set; } = true;
    public bool AssetBrowserVisible { get; set; } = true;
    public bool ValidationVisible { get; set; } = true;
    public bool LogVisible { get; set; } = true;
    public bool ViewportVisible { get; set; } = true;
    public bool LayoutResetPending { get; private set; }

    public void RequestLayoutReset() => LayoutResetPending = true;

    public bool ConsumeLayoutReset()
    {
        bool value = LayoutResetPending;
        LayoutResetPending = false;
        return value;
    }
}
