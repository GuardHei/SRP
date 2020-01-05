Shader "SRP/DitherTransparentDepthNormal" {

    SubShader {

        Tags {
            "RenderType" = "Opaque"
            "Queue" = "AlphaTest"
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

            #pragma vertex AlphaTestDepthVertex
            #pragma fragment AlphaTestDepthFragment
            #pragma multi_compile_instancing

			#include "SRPInclude.hlsl"

            struct AlphaTestDepthVertexInput {
                float4 pos : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct AlphaTestDepthVertexOutput {
                float4 clipPos : SV_POSITION;
                float3 normal : TEXCOORD0;
                float2 uv : TEXCOORD1;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float4 _AlphaTexture_ST;
            CBUFFER_END

            TEXTURE2D(_AlphaTexture);
            SAMPLER(sampler_AlphaTexture);

            AlphaTestDepthVertexOutput AlphaTestDepthVertex(AlphaTestDepthVertexInput input) {
                AlphaTestDepthVertexOutput output;
                UNITY_SETUP_INSTANCE_ID(input);
                output.clipPos = GetClipPosition(GetWorldPosition(input.pos));
                output.normal = GetWorldNormal(input.normal);
                output.uv = TRANSFORM_TEX(input.uv, _AlphaTexture);
                return output;
            }

            float4 AlphaTestDepthFragment(AlphaTestDepthVertexOutput input, float4 screenPos : SV_POSITION) : SV_TARGET {
                float alpha = _Color.a * _AlphaTexture.Sample(sampler_AlphaTexture, input.uv).r;
                DitherClip64((uint2) screenPos.xy, alpha);
                float3 normal = normalize(input.normal);
                return float4(normal, 1);
            }

            ENDHLSL
        }

        Pass {

            Name "SHADOWCASTER"

            Tags {
                "LightMode"="ShadowCaster"
            }

            HLSLPROGRAM
			
			#pragma target 3.5
			
            #pragma vertex DitherTransparentShadowCasterVertex
			#pragma fragment DitherTransparentShadowCasterFragment

            #pragma multi_compile_instancing
			#pragma instancing_options assumeuniformscaling

            #include "SRPInclude.hlsl"

            struct DitherTransparentShadowCasterVertexInput {
                float4 pos : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct DitherTransparentShadowCasterVertexOutput {
                float4 clipPos : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _AlphaTexture_ST;
            CBUFFER_END

            TEXTURE2D(_AlphaTexture);
            SAMPLER(sampler_AlphaTexture);

            DitherTransparentShadowCasterVertexOutput DitherTransparentShadowCasterVertex(DitherTransparentShadowCasterVertexInput input) {
                DitherTransparentShadowCasterVertexOutput output;
                UNITY_SETUP_INSTANCE_ID(input);
                float3 worldNormal = GetWorldNormal(input.normal);
                float4 worldPos = GetWorldPosition(input.pos.xyz);
                output.clipPos = ClipSpaceShadowBias(ShadowNormalBias(worldPos, worldNormal));
                output.uv = TRANSFORM_TEX(input.uv, _AlphaTexture);
                return output;
            }

            float4 DitherTransparentShadowCasterFragment(DitherTransparentShadowCasterVertexOutput input, float4 screenPos : SV_POSITION) : SV_TARGET {
                float alpha = _AlphaTexture.Sample(sampler_AlphaTexture, input.uv).r;
                DitherClip64((uint2) screenPos.xy, alpha);
                return float4(0, 0, 0, 0);
            }

			ENDHLSL
        }
    }
}
