struct VertexInput
{
    float3 Position : POSITION0;
    float3 Normal : TEXCOORD0;
    float2 TextureCoordinate : TEXCOORD1;
};

struct VertexOutput
{
    float4 Position : SV_Position;
    float3 WorldNormal : TEXCOORD0;
    float2 TextureCoordinate : TEXCOORD1;
};

cbuffer CameraConstants : register(b0, space1)
{
    float4x4 WorldViewProjection;
    float4x4 WorldInverse;
};

VertexOutput main(VertexInput input)
{
    VertexOutput output;
    output.Position = mul(float4(input.Position, 1.0), WorldViewProjection);
    output.WorldNormal = normalize(mul(float4(input.Normal, 0.0), WorldInverse).xyz);
    output.TextureCoordinate = input.TextureCoordinate;
    return output;
}
