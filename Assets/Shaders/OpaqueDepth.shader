Shader "SRP/OpaqueDepth" {

    SubShader {

        Tags {
            "RenderType" = "Opaque"
        }

        Pass {

            Name "DEPTH"

            Tags {
                "LightMode" = "Depth"
            }

            ZTest LEqual
            ZWrite On
			Cull Back

            HLSLPROGRAM

            #pragma target 5.0

            #pragma vertex OpaqueDepthVertex
            #pragma fragment OpaqueDepthFragment
            #pragma multi_compile_instancing

			#include "SRPInclude.hlsl"

            BasicVertexOutput OpaqueDepthVertex(BasicVertexInput input) {
                BasicVertexOutput output;
                UNITY_SETUP_INSTANCE_ID(input);
                output.clipPos = GetClipPosition(GetWorldPosition(input.pos));
                return output;
            }

            float4 OpaqueDepthFragment(BasicVertexOutput input) : SV_TARGET {
                return 0;
            }

            ENDHLSL
        }
    }
}
