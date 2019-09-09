Shader "SRP/LitColor" {

    Properties {
	    _Color("Color", Color) = (0, 0, 0, 1)
	}
    
    SubShader {

        Pass {

            Tags { 
                "RenderType"="Opaque"
            }

            ZTest LEqual
            ZWrite On
			Cull Back

            HLSLPROGRAM
			#pragma target 3.5

			#pragma vertex Vertex
			#pragma fragment Fragment
            #pragma multi_compile_instancing

			#include "SRPInclude.hlsl"

            struct VertexInput {
                float4 pos : POSITION;
                float3 normal : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct VertexOutput {
                float4 clipPos : SV_POSITION;
                float3 normal : TEXCOORD0;
                float4 worldPos : TEXCOORD1;
                float3 viewDir : TEXCOORD2;
                float4 screenPos : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            UNITY_INSTANCING_BUFFER_START(PerInstance)
                UNITY_DEFINE_INSTANCED_PROP(float3, _Color)
            UNITY_INSTANCING_BUFFER_END(PerInstance)

            VertexOutput Vertex(VertexInput input) {
                VertexOutput output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                output.worldPos = GetWorldPosition(input.pos.xyz);
                output.clipPos = GetClipPosition(output.worldPos);
                output.normal = GetWorldNormal(input.normal);
                output.viewDir = normalize(WorldSpaceViewDirection(output.worldPos));
                output.screenPos = ComputeScreenPosition(output.clipPos);
                return output;
            }

            float4 Fragment(VertexOutput input) : SV_TARGET {
                UNITY_SETUP_INSTANCE_ID(input);

                float2 screenUV = input.screenPos.xy / input.screenPos.w;
                uint2 screenIndex = uint2(_ScreenParams.x * screenUV.x, _ScreenParams.y * screenUV.y);
                
                float3 color = UNITY_ACCESS_INSTANCED_PROP(PerInstance, _Color).rgb;
                float3 normal = _OpaqueNormalTexture[screenIndex];

                uint2 lightTextureIndex = screenIndex / 16;
                uint3 lightCountIndex = uint3(lightTextureIndex, 0);
                uint pointLightCount = _CulledPointLightTexture[lightCountIndex];
                uint spotLightCount = _CulledSpotLightTexture[lightCountIndex];
                float3 litColor = DefaultDirectionLit(normal)/* * DefaultDirectionShadow(input.worldPos)*/;
                // litColor = float3(0, 0, 0);

/*
                pointLightCount = min(pointLightCount, 1);

                [loop]
                for (uint i = 0; i < pointLightCount; ++i) {
                    PointLight light = _PointLightBuffer[_CulledPointLightTexture[uint3(lightTextureIndex, i + 1)]];
                    litColor = light.color;
                }

                return float4(litColor, 1);

                return float4(input.screenPos.xy, 0, 1);

                // return float4(lightTextureIndex.x / 160.0, lightTextureIndex.y / 90.0, 0, 1);
*/

                [loop]
                for (uint i = 0; i < pointLightCount; ++i) litColor += DefaultPointLit(input.worldPos, normal, uint3(lightTextureIndex, i + 1));

                [loop]
                for (i = 0; i < spotLightCount; ++i) litColor += DefaultSpotLit(input.worldPos, normal, uint3(lightTextureIndex, i + 1));

/*
                if (spotLightCount > 0) return float4(1, 0, 0, 1);
                else return float4(0, 1, 0, 1);
*/

                return float4(litColor * color, 1);

            }

		    ENDHLSL
        }

        Pass {

            Tags { 
                "LightMode"="ShadowCaster"
            }

            HLSLPROGRAM
			
			#pragma target 3.5
			
			#pragma multi_compile_instancing
			#pragma instancing_options assumeuniformscaling
			
			#pragma vertex Vertex
			#pragma fragment Fragment

			#include "SRPInclude.hlsl"

            BasicVertexOutput Vertex(BasicVertexInput input) {
                UNITY_SETUP_INSTANCE_ID(input);
                BasicVertexOutput output;
                output.clipPos = GetClipPosition(GetWorldPosition(input.pos.xyz));
#if UNITY_REVERSED_Z
		        output.clipPos.z -= _SunlightShadowBias;
		        output.clipPos.z = min(output.clipPos.z, output.clipPos.w * UNITY_NEAR_CLIP_VALUE);
#else
		        output.clipPos.z += _SunlightShadowBias;
		        output.clipPos.z = max(output.clipPos.z, output.clipPos.w * UNITY_NEAR_CLIP_VALUE);
#endif
                return output;
            }

            float4 Fragment(BasicVertexOutput input) : SV_TARGET {
                return 0;
            }

			ENDHLSL
        }
    }
}