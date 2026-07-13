using Royale.Content.Maps;

namespace Royale.Editor.Documents;

public sealed class SetWorldBoundsCommand : IEditorDocumentCommand
{
    private readonly MapBounds before;
    private readonly MapBounds after;

    public SetWorldBoundsCommand(MapBounds before, MapBounds after)
    {
        EditorMapEditing.ValidateBounds(after);
        this.before = before;
        this.after = after;
    }

    public string Description => "Edit world bounds";
    public void Apply(EditorMapDocument document) => document.Map = document.Map with { WorldBounds = after };
    public void Revert(EditorMapDocument document) => document.Map = document.Map with { WorldBounds = before };
}

public sealed class SetSafeZoneCommand : IEditorDocumentCommand
{
    private readonly SafeZoneDefinition before;
    private readonly SafeZoneDefinition after;

    public SetSafeZoneCommand(SafeZoneDefinition before, SafeZoneDefinition after)
    {
        EditorMapEditing.ValidateSafeZone(after);
        this.before = before;
        this.after = after;
    }

    public string Description => "Edit safe zone";
    public void Apply(EditorMapDocument document) => document.Map = document.Map with { SafeZone = after };
    public void Revert(EditorMapDocument document) => document.Map = document.Map with { SafeZone = before };
}
