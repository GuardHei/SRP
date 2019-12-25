Shader "SRP/AlphaTestDepthNormal" {

    SubShader {

        Tags {
            "RenderType" = "Opaque"
            "Queue" = "AlphaTest"
        }

        Pass {

            Name "DEPTHNORMAL"

            Tags {
                "LightMode" = "DepthNormal"
            }

            ZTest Less
            ZWrite On
			Cull Back

            HLSLPROGRAM

            #pragma target 5.0

            #pragma vertex AlphaTestDepthVertex
            #pragma fragment AlphaTestDepthFragment
            #pragma multi_compile_instancing

			#include "SRPInclude.hlsl"

            struct AlphaTestDepthVertexInput {
                float4 pos : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct AlphaTestDepthVertexOutput {
                float4 clipPos : POSITION;
                float3 normal : TEXCOORD0;
                float2 uv : TEXCOORD1;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _AlphaTexture_ST;
            CBUFFER_END

            TEXTURE2D(_AlphaTexture);
            SAMPLER(sampler_AlphaTexture);

            AlphaTestDepthVertexOutput AlphaTestDepthVertex(AlphaTestDepthVertexInput input) {
                AlphaTestDepthVertexOutput output;
                UNITY_SETUP_INSTANCE_ID(input);
                output.clipPos = GetClipPosition(GetWorldPosition(input.pos));
                output.normal = GetWorldNormal(input.normal);
                output.uv = TRANSFORM_TEX(input.uv, _AlphaTexture);
                return output;
            }

            float4 AlphaTestDepthFragment(AlphaTestDepthVertexOutput input) : SV_TARGET {
                float alpha = _AlphaTexture.Sample(sampler_AlphaTexture, input.uv).r;
                clip(alpha - _AlphaTestDepthCutoff);
                float3 normal = normalize(input.normal);
                return float4(normal, 1);
            }

            ENDHLSL
        }
    }
}
