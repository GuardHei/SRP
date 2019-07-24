Shader "SRP/TestShader" {
    
    SubShader {

        Tags { 
            "RenderType"="Opaque"
        }

        Pass {

            ZTest Always
            ZWrite Off
			Cull Off

            HLSLPROGRAM
			#pragma target 3.5

			#pragma vertex Vertex
			#pragma fragment Fragment

			#include "SRPInclude.hlsl"

            TEXTURE2D(_OpaqueDepthTexture);
            SAMPLER(sampler_OpaqueDepthTexture);

            CBUFFER_START(UnityPerMaterial)
                float4 _OpaqueDepthTexture_ST;
            CBUFFER_END

            ImageVertexOutput Vertex(ImageVertexInput input) {
                ImageVertexOutput output;
                output.clipPos = GetClipPosition(GetWorldPosition(input.pos.xyz));
                output.uv = TRANSFORM_TEX(input.uv, _OpaqueDepthTexture);
                return output;
            }

            float4 Fragment(ImageVertexOutput input) : SV_TARGET {
                float4 c = SAMPLE_TEXTURE2D(_OpaqueDepthTexture, sampler_OpaqueDepthTexture, input.uv);
                // return c;
                float t = c.r + c.g + c.b;
                float a = 1 - c.r;
                return float4(a * 3, a * 3 , a * 3, 1);
            }

		    ENDHLSL
        }
    }
}