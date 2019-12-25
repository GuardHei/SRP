Shader "SRP/GaussianBlur" {

    Properties {
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
			#pragma target 3.5

			#pragma vertex Vertex
			#pragma fragment Fragment

			#include "SRPInclude.hlsl"

            struct VertexOutput {
                float4 clipPos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 uv01 : TEXCOORD1;
                float4 uv23 : TEXCOORD2;
                float4 uv45 : TEXCOORD3;
            };

            float _BlurRadius;

            VertexOutput Vertex(ImageVertexInput input) {
                VertexOutput output;
                output.clipPos = GetClipPosition(GetWorldPosition(input.pos.xyz));
                output.uv = input.uv;
                float4 offsets = float4(_BlurRadius, 0, 0, 0) * _MainTex_TexelSize.xyxy;
                offsets = offsets.xyxy * float4(1, 1, -1, -1);
                output.uv01 = input.uv.xyxy + offsets.xyxy;
                output.uv23 = input.uv.xyxy + offsets.xyxy * 2.0;
                output.uv45 = input.uv.xyxy + offsets.xyxy * 3.0;
                return output;
            }

            float4 Fragment(VertexOutput input) : SV_TARGET {
                // return float4(1, 0, 1, 1);
                float4 color = float4(0, 0, 0, 0);
                color += 0.4 * SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                color += 0.15 * SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv01.xy);
                color += 0.15 * SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv01.zw);
                color += 0.1 * SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv23.xy);
                color += 0.1 * SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv23.zw);
                color += 0.05 * SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv45.xy);
                color += 0.05 * SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv45.zw);
                return color;
            }

		    ENDHLSL
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

            struct VertexOutput {
                float4 clipPos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 uv01 : TEXCOORD1;
                float4 uv23 : TEXCOORD2;
                float4 uv45 : TEXCOORD3;
            };

            float _BlurRadius;

            VertexOutput Vertex(ImageVertexInput input) {
                VertexOutput output;
                output.clipPos = GetClipPosition(GetWorldPosition(input.pos.xyz));
                output.uv = input.uv;
                float4 offsets = float4(0, _BlurRadius, 0, 0) * _MainTex_TexelSize.xyxy;
                offsets = offsets.xyxy * float4(1, 1, -1, -1);
                output.uv01 = input.uv.xyxy + offsets.xyxy;
                output.uv23 = input.uv.xyxy + offsets.xyxy * 2.0;
                output.uv45 = input.uv.xyxy + offsets.xyxy * 3.0;
                return output;
            }

            float4 Fragment(VertexOutput input) : SV_TARGET {
                float4 color = float4(0, 0, 0, 0);
                color += 0.4 * SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                color += 0.15 * SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv01.xy);
                color += 0.15 * SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv01.zw);
                color += 0.1 * SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv23.xy);
                color += 0.1 * SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv23.zw);
                color += 0.05 * SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv45.xy);
                color += 0.05 * SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv45.zw);
                return color;
            }

		    ENDHLSL
        }

        Pass {

            ZTest Always
            ZWrite Off
			Cull Off

            Stencil {
                Ref 1
                Comp Equal
                ReadMask 1
            }

            HLSLPROGRAM
			#pragma target 5.0

			#pragma vertex Vertex
			#pragma fragment Fragment

			#include "SRPInclude.hlsl"

            ImageVertexOutput Vertex(ImageVertexInput input) {
                ImageVertexOutput output;
                output.clipPos = float4(input.pos.xy, 0, 1);
                output.uv = TransformTriangleVertexToUV(input.pos);
                // output.clipPos = float4(input.pos.xy * 2.0 - 1.0, 0, 1);
                // output.uv = input.uv;

#if UNITY_UV_STARTS_AT_TOP
                output.uv = output.uv * float2(1.0, -1.0) + float2(0.0, 1.0);
#endif

                output.uv = output.uv * _MainTex_ST.xy + _MainTex_ST.zw;

                return output;
            }

            float4 Fragment(ImageVertexOutput input) : SV_TARGET {
                float2 uv = input.uv;
                // uv.y = 1 - uv.y;
                return float4(1, 0, 0, 1);
                return SAMPLE_TEXTURE2D(_OpaqueDepthTexture, sampler_OpaqueDepthTexture, uv);
                return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv);
            }

            ENDHLSL
        }
    }
}