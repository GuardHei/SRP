#ifndef SRP_INCLUDE
#define SRP_INCLUDE

#define UNITY_MATRIX_M unity_ObjectToWorld

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/UnityInstancing.hlsl"

#include "ComputeUtils.hlsl"

StructuredBuffer<PointLight> _PointLightBuffer;
StructuredBuffer<SpotLight> _SpotLightBuffer;

Texture2D _OpaqueNormalTexture;

// Texture2DArray<float> _SunlightShadowmap;
// SamplerState sampler_SunlightShadowmap;

Texture3D<uint> _CulledPointLightTexture;
Texture3D<uint> _CulledSpotLightTexture;

TEXTURE2D(_OpaqueDepthTexture);
SAMPLER(sampler_OpaqueDepthTexture);

TEXTURE2D(_SunlightShadowmap);
SAMPLER_CMP(sampler_SunlightShadowmap);
SamplerState linear_clamp_sampler;

/*
TEXTURE2D(_OpaqueNormalTexture);
SAMPLER(sampler_OpaqueNormalTexture);
*/

CBUFFER_START(UnityPerFrame)
    float4x4 unity_MatrixVP;
    float4x4 sunlight_MatrixVP;
    float4 _WorldSpaceCameraPos;
    float4 _ScreenParams;
    float4 _ProjectionParams;
    float4 _OpaqueDepthTexture_ST;
    float4 _SunlightShadowmap_ST;
    // float4 _OpaqueNormalTexture_ST;
    float3 _SunlightColor;
    float3 _SunlightDirection;
    float _SunlightShadowBias;
    float _SunlightShadowStrength;
CBUFFER_END

CBUFFER_START(UnityPerDraw)
    float4x4 unity_ObjectToWorld;
CBUFFER_END

CBUFFER_START(UnityPerMaterial)
    float4 _MainTex_ST;
CBUFFER_END

//////////////////////////////////////////
// Built-in Vertex Input/Output Structs //
//////////////////////////////////////////

struct BasicVertexInput {
    float4 pos : POSITION;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct SimpleVertexInput {
    float4 pos : POSITION;
    float3 normal : NORMAL;
    UNITY_VERTEX_INPUT_INSTANCE_ID
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

///////////////////////////////
// Built-in Helper Functions //
///////////////////////////////

inline float4 GetWorldPosition(float3 pos) {
    return mul(UNITY_MATRIX_M, float4(pos, 1.0));
}

inline float4 GetClipPosition(float4 worldPos) {
    return mul(unity_MatrixVP, worldPos);
}

inline float3 GetWorldNormal(float3 normal) {
    return mul((float3x3) UNITY_MATRIX_M, normal);
}

inline float4 ComputeScreenPosition(float4 clipPos) {
    float4 output = clipPos * .5;
    output.xy = float2(output.x, output.y * _ProjectionParams.x) + output.w;
    output.zw = clipPos.zw;
    return output;
}

inline float3 WorldSpaceViewDirection(float4 localPos) {
    return _WorldSpaceCameraPos.xyz - mul(UNITY_MATRIX_M, localPos).xyz;
}

inline float3 WorldSpaceViewDirection(float3 worldPos) {
    return _WorldSpaceCameraPos.xyz - worldPos;
}

//////////////////////////////////////
// Built-in Vertex/Fragment Shaders //
//////////////////////////////////////

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

////////////////////////
// Lighting Functions //
////////////////////////

inline float SlopeScaleShadowBias(float3 worldNormal, float constantBias, float maxBias) {
    float cos = saturate(dot(worldNormal, _SunlightDirection));
    float sin = sqrt(1 - cos * cos);
    float tan = sin / cos;
    float bias = constantBias + clamp(tan, 0, maxBias);
    return bias;
}

inline float3 DefaultDirectionLit(float3 worldNormal) {
    float diffuse = saturate(dot(worldNormal, _SunlightDirection));
    return diffuse * _SunlightColor;
}

inline float DefaultDirectionShadow(float3 worldPos) {
    float4 shadowPos = mul(sunlight_MatrixVP, float4(worldPos, 1.0));
    shadowPos.xyz /= shadowPos.w;
    return lerp(1, SAMPLE_TEXTURE2D_SHADOW(_SunlightShadowmap, sampler_SunlightShadowmap, shadowPos.xyz), _SunlightShadowStrength);
}

inline float3 DefaultPointLit(float3 worldPos, float3 worldNormal, uint3 lightIndex) {
    PointLight light = _PointLightBuffer[_CulledPointLightTexture[lightIndex]];
    float3 lightDiff = light.sphere.xyz - worldPos;
    float3 lightDiffDot = dot(lightDiff, lightDiff);
    float3 lightDir = normalize(lightDiff);
    float distanceSqr = max(lightDiffDot, .00001);
    float rangeFade = lightDiffDot * 1.0 / max(light.sphere.w * light.sphere.w, .00001);
    rangeFade = saturate(1.0 - rangeFade * rangeFade);
    rangeFade *= rangeFade;
    float diffuse = saturate(dot(worldNormal, lightDir));
    diffuse *= rangeFade / distanceSqr;
    return diffuse * light.color;
}

inline float3 DefaultSpotLit(float3 worldPos, float3 worldNormal, uint3 lightIndex) {
    SpotLight light = _SpotLightBuffer[_CulledSpotLightTexture[lightIndex]];
    float3 lightDiff = light.cone.vertex.xyz - worldPos;
    float3 lightDiffDot = dot(lightDiff, lightDiff);
    float3 lightDir = normalize(lightDiff);
    float distanceSqr = max(lightDiffDot, .00001);
    float rangeFade = lightDiffDot * 1.0 / max(light.cone.height * light.cone.height, .00001);
    rangeFade = saturate(1.0 - rangeFade * rangeFade);
    rangeFade *= rangeFade;
    float cosAngle = cos(light.cone.angle);
    float angleRangeInv = 1 / max(cos(light.smallAngle) - cosAngle, .00001);
    float spotFade = dot(light.cone.direction, lightDir);
    spotFade = saturate((spotFade - cosAngle) * angleRangeInv);
    float diffuse = saturate(dot(worldNormal, lightDir));
    diffuse *= rangeFade * spotFade / distanceSqr;
    return diffuse * light.color;
}

#endif // SRP_INCLUDE