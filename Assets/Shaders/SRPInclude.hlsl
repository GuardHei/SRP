#ifndef SRP_INCLUDE
#define SRP_INCLUDE

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"

CBUFFER_START(UnityPerFrame)
    float4x4 unity_MatrixVP;
CBUFFER_END

CBUFFER_START(UnityPerDraw)
    float4x4 unity_ObjectToWorld;
CBUFFER_END

struct BasicVertexInput {
    float4 pos : POSITION;
};

struct SimpleVertexInput {
    float4 pos : POSITION;
    float3 normal : NORMAL;
};

struct BasicVertexOutput {
    float4 clipPos : SV_POSITION;
};

struct SimpleVertexOutput {
    float4 clipPos : SV_POSITION;
    float3 normal : TEXCOORD0;
};

float4 GetWorldPosition(float3 pos) {
    return mul(unity_ObjectToWorld, float4(pos, 1.0));
}

float4 GetClipPosition(float4 worldPos) {
    return mul(unity_MatrixVP, worldPos);
}

float3 GetWorldNormal(float3 normal) {
    return mul((float3x3) unity_ObjectToWorld, normal);
}

BasicVertexOutput UnlitVertex(BasicVertexInput input) {
    BasicVertexOutput output;
	output.clipPos = GetClipPosition(GetWorldPosition(input.pos.xyz));
	return output;
}

float4 UnlitFragment(BasicVertexOutput input) : SV_TARGET {
    return 1;
}

float4 NoneFragment(BasicVertexOutput input) : SV_TARGET {
    return 0;
}

#endif // SRP_INCLUDE