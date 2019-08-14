Shader "SRP/LitColor" {

    Properties {
	    _Color("Color", Color) = (0, 0, 0, 1)
	}
    
    SubShader {

        Tags { 
            "RenderType"="Opaque"
        }

        Pass {

            HLSLPROGRAM
			#pragma target 3.5

			#pragma vertex Vertex
			#pragma fragment Fragment
            #pragma multi_compile_instancing

			#include "SRPInclude.hlsl"
            #include "ComputeUtils.hlsl"

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
                UNITY_DEFINE_INSTANCED_PROP(float4, _Color)
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
                float3 color = UNITY_ACCESS_INSTANCED_PROP(PerInstance, _Color).rgb;
                float3 normal = normalize(input.normal);

                // uint2 lightTextureIndex = uint2(_ScreenParams.x * input.screenPos.x / 16, _ScreenParams.y * input.screenPos.y / 16);
                float2 screenUV = input.screenPos.xy / input.screenPos.w;
                uint2 lightTextureIndex = uint2(_ScreenParams.x * screenUV.x / 16.0, _ScreenParams.y * screenUV.y / 16.0);
                uint lightCount = _CulledPointLightTexture[uint3(lightTextureIndex, 0)];
                float3 litColor = DefaultDirectionLit(normal);
                // litColor = float3(0, 0, 0);

/*
                lightCount = min(lightCount, 1);

                [loop]
                for (uint i = 0; i < lightCount; ++i) {
                    PointLight light = _PointLightBuffer[_CulledPointLightTexture[uint3(lightTextureIndex, i + 1)]];
                    litColor = light.color;
                }

                return float4(litColor, 1);

                return float4(input.screenPos.xy, 0, 1);

                // return float4(lightTextureIndex.x / 160.0, lightTextureIndex.y / 90.0, 0, 1);
*/

                [loop]
                for (uint i = 0; i < lightCount; ++i) litColor += DefaultPointLit(input.worldPos, normal, uint3(lightTextureIndex, i + 1));

                return float4(litColor * color, 1);

            }

		    ENDHLSL
        }
    }
}