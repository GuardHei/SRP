Shader "SRP/OpaqueDepthNormal" {

    SubShader {

        Tags {
            "RenderType" = "Opaque"
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

            #pragma vertex OpaqueDepthVertex
            #pragma fragment OpaqueDepthFragment
            #pragma multi_compile_instancing

			#include "SRPInclude.hlsl"

            SimpleVertexOutput OpaqueDepthVertex(SimpleVertexInput input) {
                SimpleVertexOutput output;
                UNITY_SETUP_INSTANCE_ID(input);
                output.clipPos = GetClipPosition(GetWorldPosition(input.pos));
                output.normal = GetWorldNormal(input.normal);
                return output;
            }

            float4 OpaqueDepthFragment(SimpleVertexOutput input) : SV_TARGET {
                float3 normal = normalize(input.normal);
                return float4(normal, 1);
            }

            ENDHLSL
        }
    }
}
