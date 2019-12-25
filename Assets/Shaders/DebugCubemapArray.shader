Shader "SRP/DebugCubemapArray" {

    Properties {
	    _Index("Index", Int) = 0
        _Face("Face", Int) = 0
        _MainTex ("Main Texture", 2D) = "" { } 
	}
    
    SubShader {

        Tags { 
            "RenderType"="Opaque"
        }

        Pass {

            ZTest Always
            ZWrite Off
			Cull Off

            HLSLPROGRAM
			#pragma target 5.0

			#pragma vertex Vertex
			#pragma fragment Fragment

			#include "SRPInclude.hlsl"
            #include "ComputeUtils.hlsl"

            ImageVertexOutput Vertex(ImageVertexInput input) {
                ImageVertexOutput output;
                output.clipPos = GetClipPosition(GetWorldPosition(input.pos.xyz));
                output.uv = input.uv;
                return output;
            }

            int _Index;

            float4 Fragment(ImageVertexOutput input, float4 screenPos : SV_POSITION) : SV_TARGET {
                float3 dir = float3(screenPos.zw, 1);
                return _PointLightShadowmapArray.Sample(sampler_PointLightShadowmapArray, float4(dir, _Index));
            }

		    ENDHLSL
        }
    }
}