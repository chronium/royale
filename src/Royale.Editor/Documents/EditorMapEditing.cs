using System.Numerics;
using Royale.Content.Maps;

namespace Royale.Editor.Documents;

public static class EditorMapEditing
{
    public static string CreateUniqueId(EditorMapDocument document, EditorEntityKind kind, string requested)
    {
        ArgumentNullException.ThrowIfNull(document);
        string stem = ToSlug(requested);
        HashSet<string> used = GetIds(document, kind).ToHashSet(StringComparer.Ordinal);
        if (!used.Contains(stem))
            return stem;
        for (int suffix = 2; ; suffix++)
        {
            string candidate = $"{stem}-{suffix}";
            if (!used.Contains(candidate))
                return candidate;
        }
    }

    public static object DuplicateDefinition(EditorMapDocument document, EditorEntityIdentity identity)
    {
        object value = document.GetDefinition(identity.EditorId);
        string unique = CreateUniqueId(document, identity.Kind, EditorEntityTransforms.GetDisplayId(document, identity));
        return value switch
        {
            StaticBoxDefinition box => box with { Id = unique },
            StaticModelDefinition model => model with { Id = unique },
            MapSpawnPoint spawn => spawn with { Id = unique },
            MapLootPoint loot => loot with { Id = unique },
            MapNavigationWaypoint waypoint => waypoint with { Id = unique },
            MapNavigationLink => throw new InvalidOperationException("Navigation links cannot be duplicated."),
            _ => throw new ArgumentOutOfRangeException(nameof(identity)),
        };
    }

    public static void ValidateDefinition(EditorMapDocument document, EditorEntityKind kind, object definition, Guid? replacedEditorId = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        string? id = definition switch
        {
            StaticBoxDefinition value => value.Id,
            StaticModelDefinition value => value.Id,
            MapSpawnPoint value => value.Id,
            MapLootPoint value => value.Id,
            MapNavigationWaypoint value => value.Id,
            _ => null,
        };
        if (id is not null)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("Entity IDs must be non-empty.", nameof(definition));
            bool duplicate = document.Identities
                .Where(identity => replacedEditorId is null || identity.EditorId != replacedEditorId)
                .Where(identity => SharesIdNamespace(kind, identity.Kind))
                .Any(identity => string.Equals(EditorEntityTransforms.GetDisplayId(document, identity), id, StringComparison.Ordinal));
            if (duplicate)
                throw new ArgumentException($"Entity ID '{id}' is already in use.", nameof(definition));
        }

        switch (definition)
        {
            case StaticBoxDefinition box:
                ValidateFinite(box.Position, box.RotationEuler, box.Size);
                if (box.Size.X <= 0 || box.Size.Y <= 0 || box.Size.Z <= 0)
                    throw new ArgumentException("Static box sizes must be positive.", nameof(definition));
                break;
            case StaticModelDefinition model:
                if (string.IsNullOrWhiteSpace(model.AssetId))
                    throw new ArgumentException("Static model asset IDs must be non-empty.", nameof(definition));
                ValidateFinite(model.Position, model.RotationEuler, model.Scale);
                if (model.Scale.X == 0 || model.Scale.Y == 0 || model.Scale.Z == 0)
                    throw new ArgumentException("Static model scales cannot contain zero.", nameof(definition));
                break;
            case MapSpawnPoint spawn:
                ValidateFinite(spawn.Position, spawn.RotationEuler);
                break;
            case MapLootPoint loot:
                ValidateFinite(loot.Position);
                break;
            case MapNavigationWaypoint waypoint:
                ValidateNavigationId(waypoint.Id);
                ValidateFinite(waypoint.Position);
                break;
            case MapNavigationLink link:
                ValidateLink(document, link, replacedEditorId);
                break;
            default:
                throw new ArgumentException($"Definition does not match entity kind '{kind}'.", nameof(definition));
        }
    }

    public static void ValidateBounds(MapBounds bounds)
    {
        ValidateFinite(bounds.Min, bounds.Max);
    }

    public static void ValidateSafeZone(SafeZoneDefinition safeZone)
    {
        ValidateFinite(safeZone.Center);
        if (!float.IsFinite(safeZone.Radius) || safeZone.Radius <= 0)
            throw new ArgumentException("Safe-zone radius must be positive and finite.", nameof(safeZone));
    }

    private static IEnumerable<string> GetIds(EditorMapDocument document, EditorEntityKind kind) => document.Identities
        .Where(identity => SharesIdNamespace(kind, identity.Kind))
        .Select(identity => EditorEntityTransforms.GetDisplayId(document, identity));

    private static bool SharesIdNamespace(EditorEntityKind requested, EditorEntityKind candidate) => requested switch
    {
        EditorEntityKind.StaticBox or EditorEntityKind.StaticModel =>
            candidate is EditorEntityKind.StaticBox or EditorEntityKind.StaticModel,
        _ => requested == candidate,
    };

    private static string ToSlug(string value)
    {
        string slug = new(value.Trim().ToLowerInvariant()
            .Select(character => character is >= 'a' and <= 'z' or >= '0' and <= '9' or '-' or '_'
                ? character
                : '-')
            .ToArray());
        slug = slug.Trim('-');
        return string.IsNullOrEmpty(slug) ? "entity" : slug;
    }

    private static void ValidateNavigationId(string id)
    {
        if (id.Any(character => !(character is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' or '-' or '_')))
            throw new ArgumentException("Navigation IDs may contain only ASCII letters, digits, '-' and '_'.", nameof(id));
    }

    private static void ValidateLink(EditorMapDocument document, MapNavigationLink link, Guid? replacedEditorId)
    {
        HashSet<string> waypointIds = document.Map.Navigation.Waypoints.Select(value => value.Id).ToHashSet(StringComparer.Ordinal);
        if (!waypointIds.Contains(link.From) || !waypointIds.Contains(link.To))
            throw new ArgumentException("Navigation link endpoints must reference existing waypoints.", nameof(link));
        if (string.Equals(link.From, link.To, StringComparison.Ordinal))
            throw new ArgumentException("Navigation links cannot connect a waypoint to itself.", nameof(link));
        bool duplicate = document.Identities
            .Where(identity => identity.Kind == EditorEntityKind.NavigationLink)
            .Where(identity => replacedEditorId is null || identity.EditorId != replacedEditorId)
            .Select(identity => document.Map.Navigation.Links[identity.Index])
            .Any(existing =>
                string.Equals(existing.From, link.From, StringComparison.Ordinal) && string.Equals(existing.To, link.To, StringComparison.Ordinal) ||
                string.Equals(existing.From, link.To, StringComparison.Ordinal) && string.Equals(existing.To, link.From, StringComparison.Ordinal));
        if (duplicate)
            throw new ArgumentException("The undirected navigation link already exists.", nameof(link));
    }

    private static void ValidateFinite(params MapVector3[] values)
    {
        if (values.Any(value => !float.IsFinite(value.X) || !float.IsFinite(value.Y) || !float.IsFinite(value.Z)))
            throw new ArgumentException("Map vectors must contain only finite values.", nameof(values));
    }
}
