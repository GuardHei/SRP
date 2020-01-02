Shader "SRP/StandardBlit" {

    Properties {
        _MainTex ("Main Texture", 2D) = "" { }
    }
    
    SubShader {

        Tags { 
            "RenderType"="Opaque"
        }

        Pass {

            Name "COPY"

            ZTest Always
            ZWrite Off
			Cull Off

            HLSLPROGRAM
			#pragma target 5.0

			#pragma vertex Vertex
			#pragma fragment Fragment

            #include "SRPInclude.hlsl"

            ImageVertexOutput Vertex(ImageVertexInput input) {
                ImageVertexOutput output;
                // output.clipPos = float4(input.pos.xy, 0, 1);
                // output.uv = TransformTriangleVertexToUV(input.pos)
                output.clipPos = float4(input.pos.xy * 2.0 - 1.0, 0, 1);
                output.uv = input.uv;

#if UNITY_UV_STARTS_AT_TOP
                output.uv = output.uv * float2(1.0, -1.0) + float2(0.0, 1.0);
#endif

                output.uv = output.uv * _MainTex_ST.xy + _MainTex_ST.zw;

                return output;
            }

            float4 Fragment(ImageVertexOutput input) : SV_TARGET {
                return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
            }

		    ENDHLSL
        }

        Pass {

            Name "VERTICALFLIP"

            ZTest Always
            ZWrite Off
			Cull Off

            HLSLPROGRAM
			#pragma target 3.5

			#pragma vertex Vertex
			#pragma fragment Fragment

            #include "SRPInclude.hlsl"

            ImageVertexOutput Vertex(ImageVertexInput input) {
                ImageVertexOutput output;
                output.clipPos = float4(input.pos.xy, 0, 1);
                output.uv = (input.pos.xy + 1) * 0.5;
                output.uv.y = 1 - output.uv.y;

#if UNITY_UV_STARTS_AT_TOP
                output.uv = output.uv * float2(1, -1) + float2(0, 1);
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