Shader "ImanTransmedia/FrostedGlassV3"
{
Properties
{
_BlurRadius ("Blur Radius", Range(0.0, 2.0)) = 0.2
_Sigma ("Blur Sigma", Range(0.1, 10.0)) = 8.75
_LODScale ("LOD Scale", Range(1.0, 4.0)) = 2.0
_DepthFadeDistance ("Depth Fade Distance", Range(0.0, 2.0)) = 0.5
_DepthFadePower ("Depth Fade Power", Range(0.1, 5.0)) = 1.0
[Toggle] _DebugNoBlur ("Debug: Disable Blur", Float) = 0
[Toggle] _DebugNoFade ("Debug: Disable Depth Fade", Float) = 0
}
SubShader
{
Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
LOD 100
Pass
{
Name "FrostedGlassPass"
ZWrite Off
Blend SrcAlpha OneMinusSrcAlpha
HLSLPROGRAM
#pragma vertex vert
#pragma fragment frag
#pragma target 3.0 // WebGL 2.0 compatibility
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
// Input properties
CBUFFER_START(UnityPerMaterial)
float _BlurRadius;
float _Sigma;
float _LODScale;
float _DepthFadeDistance;
float _DepthFadePower;
float _DebugNoBlur;
float _DebugNoFade;
CBUFFER_END
// Scene Color texture
TEXTURE2D(_CameraOpaqueTexture);
SAMPLER(sampler_CameraOpaqueTexture);
struct Attributes
{
float4 positionOS : POSITION;
float2 uv : TEXCOORD0;
};
struct Varyings
{
float4 positionCS : SV_POSITION;
float2 uv : TEXCOORD0;
float4 screenPos : TEXCOORD1;
float linearDepth : TEXCOORD2;
};
// Gaussian function
float Gaussian(float2 pos, float sigma)
{
return exp(-dot(pos, pos) / (2.0 * sigma * sigma)) / (2.0 * 3.14159265 * sigma * sigma);
}
Varyings vert(Attributes input)
{
Varyings output;
output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
output.uv = input.uv;
output.screenPos = ComputeScreenPos(output.positionCS);
output.linearDepth = -TransformWorldToView(TransformObjectToWorld(input.positionOS.xyz)).z;
return output;
}
half4 frag(Varyings input) : SV_Target
{
float2 screenUV = input.screenPos.xy / input.screenPos.w;
float2 texelSize = 1.0 / _ScreenParams.xy;
// Gaussian blur
half4 color;
if (_DebugNoBlur > 0.5)
{
color = SAMPLE_TEXTURE2D(_CameraOpaqueTexture, sampler_CameraOpaqueTexture, screenUV);
}
else
{
float radius = _BlurRadius;
float sigma = _Sigma;
float lodScale = _LODScale;
color = half4(0, 0, 0, 0);
float weightSum = 0.0;
// 7x7 kernel
for (int x = -3; x <= 3; x++)
{
for (int y = -3; y <= 3; y++)
{
float2 offset = float2(x, y) * radius * texelSize * lodScale * 4.0;
float2 sampledUV = screenUV + offset;
float sampledDepth = LinearEyeDepth(SampleSceneDepth(sampledUV), _ZBufferParams);
float weight = Gaussian(float2(x, y) * radius, sigma);
if (sampledDepth < input.linearDepth)
{
weight = 0.0;
}
half4 sampledColor = SAMPLE_TEXTURE2D(_CameraOpaqueTexture, sampler_CameraOpaqueTexture, sampledUV);
color += sampledColor * weight;
weightSum += weight;
}
}
if (weightSum > 0.0001)
{
color /= weightSum;
}
else
{
color = SAMPLE_TEXTURE2D(_CameraOpaqueTexture, sampler_CameraOpaqueTexture, screenUV);
}
}
// Depth Fade
float fade = 1.0;
if (_DebugNoFade < 0.5)
{
float sceneDepth = SampleSceneDepth(screenUV);
float depthDiff = abs(LinearEyeDepth(sceneDepth, _ZBufferParams) - input.linearDepth);
fade = saturate(depthDiff / _DepthFadeDistance);
fade = pow(fade, _DepthFadePower);
}
color.a = fade;
return color;
}
ENDHLSL
}
}
}