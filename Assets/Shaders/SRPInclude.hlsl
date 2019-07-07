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

struct BasicVertexOutput {
    float4 clipPos : SV_POSITION;
};

BasicVertexOutput UnlitVertex (BasicVertexInput input) {
    BasicVertexOutput output;
	float4 worldPos = mul(unity_ObjectToWorld, float4(input.pos.xyz, 1.0));
	output.clipPos = mul(unity_MatrixVP, worldPos);
	return output;
}

float4 UnlitFragment (BasicVertexOutput input) : SV_TARGET {
    return 1;
}

float4 NoneFragment (BasicVertexOutput input) : SV_TARGET {
    return 0;
}

#endif // SRP_INCLUDE