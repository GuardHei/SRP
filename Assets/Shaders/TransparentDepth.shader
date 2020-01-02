Shader "SRP/TransparentDepth" {

    SubShader {

        Tags {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
        }

        Pass {

            Name "DEPTH"

            Tags {
                "LightMode" = "Depth"
            }

            ZTest Less
            ZWrite On
			Cull Back

            HLSLPROGRAM

            #pragma target 5.0

            #pragma vertex UnlitVertex
            #pragma fragment Fragment
            #pragma multi_compile_instancing

			#include "SRPInclude.hlsl"

            float4 Fragment(BasicVertexOutput input) : SV_TARGET {
                return float4(1, 0, 0, 1);
            }

            ENDHLSL
        }
    }
}
