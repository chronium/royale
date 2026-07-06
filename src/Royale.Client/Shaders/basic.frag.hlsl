struct FragmentInput
{
    float4 Position : SV_Position;
    float3 WorldNormal : TEXCOORD0;
};

cbuffer LightingConstants : register(b0, space3)
{
    float4 AlbedoAmbient;
    float4 LightDirectionDiffuse;
};

float4 main(FragmentInput input) : SV_Target0
{
    float3 normal = normalize(input.WorldNormal);
    float diffuse = saturate(dot(normal, -LightDirectionDiffuse.xyz));
    float intensity = AlbedoAmbient.w + LightDirectionDiffuse.w * diffuse;
    return float4(AlbedoAmbient.xyz * intensity, 1.0);
}
