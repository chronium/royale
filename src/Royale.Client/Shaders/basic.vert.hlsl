struct VertexInput
{
    float3 Position : POSITION0;
    float3 Color : COLOR0;
};

struct VertexOutput
{
    float4 Position : SV_Position;
    float3 Color : TEXCOORD0;
};

cbuffer CameraConstants : register(b0, space1)
{
    float4x4 WorldViewProjection;
};

VertexOutput main(VertexInput input)
{
    VertexOutput output;
    output.Position = mul(WorldViewProjection, float4(input.Position, 1.0));
    output.Color = input.Color;
    return output;
}
