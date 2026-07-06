using Royale.Content;

namespace Royale.Content.Tests;

public sealed class WeaponCatalogTests
{
    [Fact]
    public void DefaultWeaponIdResolvesToRifle()
    {
        Assert.Equal("rifle", ContentCatalog.DefaultWeaponId);
        Assert.Equal(ContentCatalog.DefaultWeaponId, WeaponCatalog.DefaultWeaponId);
    }

    [Fact]
    public void DefaultRifleDefinesInitialStats()
    {
        WeaponDefinition rifle = WeaponCatalog.DefaultRifle;

        Assert.Equal("rifle", rifle.Id);
        Assert.Equal("Rifle", rifle.Name);
        Assert.Equal(WeaponFireModel.Hitscan, rifle.FireModel);
        Assert.True(rifle.IsAutomatic);
        Assert.Equal(25, rifle.Damage);
        Assert.Equal(30, rifle.MagazineSize);
        Assert.Equal(10.0, rifle.ShotsPerSecond);
        Assert.Equal(TimeSpan.FromMilliseconds(100), rifle.FireInterval);
        Assert.Equal(TimeSpan.FromSeconds(2.0), rifle.ReloadTime);
        Assert.Equal(120.0f, rifle.RangeMeters);
    }

    [Fact]
    public void RifleLookupReturnsDefaultDefinition()
    {
        WeaponDefinition rifle = WeaponCatalog.GetById("rifle");

        Assert.Same(WeaponCatalog.DefaultRifle, rifle);
    }

    [Fact]
    public void MissingWeaponIdFailsWithClearMessage()
    {
        KeyNotFoundException exception = Assert.Throws<KeyNotFoundException>(() => WeaponCatalog.GetById("missing-rifle"));

        Assert.Contains("Weapon 'missing-rifle' is not defined", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void InvalidWeaponIdFailsWithClearMessage()
    {
        ArgumentException exception = Assert.Throws<ArgumentException>(() => WeaponCatalog.GetById("../rifle"));

        Assert.Contains("must contain only ASCII letters", exception.Message, StringComparison.Ordinal);
    }
}
