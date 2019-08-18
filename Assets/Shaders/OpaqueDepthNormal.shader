Shader "SRP/OpaqueDepthAndNormal" {
    
    SubShader {

        Tags { 
            "RenderType"="Opaque"
        }

        Pass {

            Name "DepthAndNormal"

            Tags {
                "LightMode"="DepthAndNormal"
            }

            ZTest LEqual
            ZWrite On
			Cull Back

            HLSLPROGRAM
			#pragma target 3.5

			#pragma vertex Vertex
			#pragma fragment Fragment
            #pragma multi_compile_instancing

			#include "SRPInclude.hlsl"

            struct VertexInput {
                float4 pos : POSITION;
                float3 normal : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct VertexOutput {
                float4 clipPos : SV_POSITION;
                float3 normal : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            VertexOutput Vertex(VertexInput input) {
                VertexOutput output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                output.clipPos = GetClipPosition(GetWorldPosition(input.pos.xyz));
                output.normal = GetWorldNormal(input.normal);
                return output;
            }

            float4 Fragment(VertexOutput input) : SV_TARGET {
                UNITY_SETUP_INSTANCE_ID(input);
                float3 normal = normalize(input.normal);
                // normal = normal / 2 + .5;
                // if (normal.x < 0) return float4(0, 0, 1, 1);
                return float4(normal, 1);
            }

		    ENDHLSL
        }
    }
}