#ifndef SRP_INCLUDE
#define SRP_INCLUDE

#define unity_MatrixM unity_ObjectToWorld

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/UnityInstancing.hlsl"

#include "ComputeUtils.hlsl"

StructuredBuffer<PointLight> _PointLightBuffer;
StructuredBuffer<SpotLight> _SpotLightBuffer;

Texture3D<uint> _CulledPointLightTexture;
Texture3D<uint> _CulledSpotLightTexture;

TEXTURE2D(_OpaqueDepthTexture);
SAMPLER(sampler_OpaqueDepthTexture);

/*
TEXTURE3D(_CulledPointLightTexture);
SAMPLER(sampler_CulledPointLightTexture);

TEXTURE3D(_CulledSpotLightTexture);
SAMPLER(sampler_CulledSpotLightTexture);
*/

CBUFFER_START(UnityPerFrame)
    float4x4 unity_MatrixVP;
    float4 _WorldSpaceCameraPos;
    float4 _ScreenParams;
    float4 _ProjectionParams;
    float4 _OpaqueDepthTexture_ST;
    float4 _CulledPointLightTexture_ST;
    float4 _CulledSpotLightTexture_ST;
CBUFFER_END

CBUFFER_START(UnityPerDraw)
    float4x4 unity_ObjectToWorld;
CBUFFER_END

CBUFFER_START(UnityPerMaterial)
    float4 _MainTex_ST;
CBUFFER_END

struct BasicVertexInput {
    float4 pos : POSITION;
};

struct SimpleVertexInput {
    float4 pos : POSITION;
    float3 normal : NORMAL;
};

struct ImageVertexInput {
    float4 pos : POSITION;
    float2 uv : TEXCOORD0;
};

struct BasicVertexOutput {
    float4 clipPos : SV_POSITION;
};

struct SimpleVertexOutput {
    float4 clipPos : SV_POSITION;
    float3 normal : TEXCOORD0;
};

struct ImageVertexOutput {
    float4 clipPos : SV_POSITION;
    float2 uv : TEXCOORD0;
};

inline float4 GetWorldPosition(float3 pos) {
    return mul(unity_MatrixM, float4(pos, 1.0));
}

inline float4 GetClipPosition(float4 worldPos) {
    return mul(unity_MatrixVP, worldPos);
}

inline float3 GetWorldNormal(float3 normal) {
    return mul((float3x3) unity_MatrixM, normal);
}

inline float4 ComputeScreenPosition(float4 clipPos) {
    float4 output = clipPos * .5;
    output.xy = float2(output.x, output.y * _ProjectionParams.x) + output.w;
    output.zw = clipPos.zw;
    return output;
}

inline float3 WorldSpaceViewDirection(float4 localPos) {
    return _WorldSpaceCameraPos.xyz - mul(unity_MatrixM, localPos).xyz;
}

inline float3 WorldSpaceViewDirection(float3 worldPos) {
    return _WorldSpaceCameraPos.xyz - worldPos;
}

inline float LightSmoothFalloff(float sqrDist, float range) {
    float a = sqrDist * range * range;
    a *= a;
    a = saturate(1 - a);
    return a * a;
}

inline float LightDistanceFalloff(float3 lightDiff, float range) {
    float sqrDist = dot(lightDiff, lightDiff);
    return 1 / (max(sqrDist, .000001)) * LightSmoothFalloff(sqrDist, range);
}

inline float LightDistanceFalloff(float sqrDist, float range) {
    return 1 / (max(sqrDist, .000001)) * LightSmoothFalloff(sqrDist, range);
}

BasicVertexOutput UnlitVertex(BasicVertexInput input) {
    BasicVertexOutput output;
	output.clipPos = GetClipPosition(GetWorldPosition(input.pos.xyz));
	return output;
}

ImageVertexOutput ImageVertex(ImageVertexInput input) {
    ImageVertexOutput output;
    output.clipPos = GetClipPosition(GetWorldPosition(input.pos.xyz));
    output.uv = TRANSFORM_TEX(input.uv, _MainTex);
    return output;
}

float4 UnlitFragment(BasicVertexOutput input) : SV_TARGET {
    return 1;
}

float4 NoneFragment(BasicVertexOutput input) : SV_TARGET {
    return 0;
}

#endif // SRP_INCLUDE