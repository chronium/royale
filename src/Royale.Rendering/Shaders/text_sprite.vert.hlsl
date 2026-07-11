struct VertexInput
{
    float2 Position : POSITION0;
    float2 TexCoord : TEXCOORD0;
    float4 Color : COLOR0;
};

struct VertexOutput
{
    float4 Position : SV_Position;
    float2 TexCoord : TEXCOORD0;
    float4 Color : COLOR0;
};

cbuffer TextScreenConstants : register(b0, space1)
{
    float2 SwapchainSize;
    float2 Padding;
};

VertexOutput main(VertexInput input)
{
    VertexOutput output;
    float2 clipPosition = float2(
        (input.Position.x / SwapchainSize.x * 2.0) - 1.0,
        1.0 - (input.Position.y / SwapchainSize.y * 2.0));
    output.Position = float4(clipPosition, 0.0, 1.0);
    output.TexCoord = input.TexCoord;
    output.Color = input.Color;
    return output;
}
