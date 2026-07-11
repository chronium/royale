struct FragmentInput
{
    float4 Position : SV_Position;
    float3 WorldNormal : TEXCOORD0;
    float2 TextureCoordinate : TEXCOORD1;
};

Texture2D<float4> BaseColorTexture : register(t0, space2);
SamplerState BaseColorSampler : register(s0, space2);

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
    float4 sampled = BaseColorTexture.Sample(BaseColorSampler, input.TextureCoordinate);
    return float4(AlbedoAmbient.xyz * sampled.rgb * intensity, sampled.a);
}
