Shader "SRP/DepthFlip" {
    
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
            #include "ComputeUtils.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            ImageVertexOutput Vertex(ImageVertexInput input) {
                ImageVertexOutput output;
                output.clipPos = GetClipPosition(GetWorldPosition(input.pos.xyz));
                output.uv = input.uv;
#if UNITY_UV_STARTS_AT_TOP
                if (_MainTex_TexelSize.y < 0) output.uv.y = 1 - output.uv.y;
#endif
                return output;
            }

            float4 Fragment(ImageVertexOutput input) : SV_TARGET {
                return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
            }

		    ENDHLSL
        }
    }
}