struct FragmentInput
{
    float4 Position : SV_Position;
    float2 TexCoord : TEXCOORD0;
    float4 Color : COLOR0;
};

Texture2D<float4> TextTexture : register(t0, space2);
SamplerState TextSampler : register(s0, space2);

float4 main(FragmentInput input) : SV_Target0
{
    return input.Color * TextTexture.Sample(TextSampler, input.TexCoord);
}
