using System.Numerics;
using System.Runtime.InteropServices;

namespace Royale.Client.Rendering.Meshes;

[StructLayout(LayoutKind.Sequential)]
public readonly struct StaticMeshLightingConstants
{
    public const float DefaultAmbientIntensity = 0.35f;
    public const float DefaultDiffuseIntensity = 0.65f;

    public static readonly Vector3 DefaultAlbedo = new(0.68f, 0.68f, 0.68f);
    public static readonly Vector3 DefaultLightDirection = Vector3.Normalize(new Vector3(-0.45f, -1.0f, -0.35f));

    public StaticMeshLightingConstants(Vector3 albedo, Vector3 lightDirection, float ambientIntensity, float diffuseIntensity)
    {
        AlbedoAmbient = new Vector4(albedo, ambientIntensity);
        LightDirectionDiffuse = new Vector4(Vector3.Normalize(lightDirection), diffuseIntensity);
    }

    public readonly Vector4 AlbedoAmbient;

    public readonly Vector4 LightDirectionDiffuse;

    public Vector3 Albedo => new(AlbedoAmbient.X, AlbedoAmbient.Y, AlbedoAmbient.Z);

    public float AmbientIntensity => AlbedoAmbient.W;

    public Vector3 LightDirection => new(LightDirectionDiffuse.X, LightDirectionDiffuse.Y, LightDirectionDiffuse.Z);

    public float DiffuseIntensity => LightDirectionDiffuse.W;

    public static StaticMeshLightingConstants CreateDefault() =>
        new(DefaultAlbedo, DefaultLightDirection, DefaultAmbientIntensity, DefaultDiffuseIntensity);
}
