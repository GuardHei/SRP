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
			Cull Front

            HLSLPROGRAM
		    
			#pragma target 3.5

			#pragma multi_compile_instancing

			#pragma vertex Vertex
			#pragma fragment Fragment

			#include "SRPInclude.hlsl"

            SimpleVertexOutput Vertex(SimpleVertexInput input) {
                SimpleVertexOutput output;
                output.clipPos = GetClipPosition(GetWorldPosition(input.pos.xyz));
                output.normal = GetWorldNormal(input.normal);
                return output;
            }

            float4 Fragment(SimpleVertexOutput input) : SV_TARGET {
                float3 normal = normalize(input.normal);
                // return float4(normal, 1);
                return float4(0, 1, .5, 1);
            }

		    ENDHLSL
        }
    }
}