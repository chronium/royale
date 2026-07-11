namespace Royale.Content.Weapons;

public static class WeaponCatalog
{
    private static readonly WeaponDefinition Rifle = new()
    {
        Id = ContentCatalog.DefaultWeaponId,
        Name = "Rifle",
        FireModel = WeaponFireModel.Hitscan,
        IsAutomatic = true,
        Damage = 25,
        MagazineSize = 30,
        ShotsPerSecond = 10.0,
        FireInterval = TimeSpan.FromSeconds(1.0 / 10.0),
        ReloadTime = TimeSpan.FromSeconds(2.0),
        RangeMeters = 120.0f,
    };

    private static readonly IReadOnlyDictionary<string, WeaponDefinition> WeaponsById =
        new Dictionary<string, WeaponDefinition>(StringComparer.Ordinal)
        {
            [Rifle.Id] = Rifle,
        };

    public static string DefaultWeaponId => ContentCatalog.DefaultWeaponId;

    public static WeaponDefinition DefaultRifle => Rifle;

    public static WeaponDefinition GetById(string weaponId)
    {
        ValidateWeaponId(weaponId);

        if (!WeaponsById.TryGetValue(weaponId, out WeaponDefinition? weapon))
            throw new KeyNotFoundException($"Weapon '{weaponId}' is not defined.");

        return weapon;
    }

    private static void ValidateWeaponId(string weaponId)
    {
        if (string.IsNullOrWhiteSpace(weaponId))
            throw new ArgumentException("Weapon id must be non-empty.", nameof(weaponId));

        foreach (char character in weaponId)
        {
            bool valid =
                character is >= 'a' and <= 'z' ||
                character is >= 'A' and <= 'Z' ||
                character is >= '0' and <= '9' ||
                character is '-' or '_';

            if (!valid)
                throw new ArgumentException($"Weapon id '{weaponId}' must contain only ASCII letters, digits, '-' or '_'.", nameof(weaponId));
        }
    }
}
