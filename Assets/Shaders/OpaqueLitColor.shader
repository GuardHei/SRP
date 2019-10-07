Shader "SRP/OpaqueLitColor" {

    Properties {
	    _Color("Color", Color) = (0, 0, 0, 1)
	}
    
    SubShader {

        UsePass "SRP/OpaqueDepthNormal/DEPTHNORMAL"

        Pass {

            Tags { 
                "RenderType"="Opaque"
            }

            ZTest Equal
            ZWrite On
			Cull Back

            HLSLPROGRAM
			#pragma target 3.5

			#pragma vertex Vertex
			#pragma fragment Fragment
            #pragma multi_compile_instancing
            #pragma multi_compile _ _SUNLIGHT_SHADOWS
            #pragma multi_compile _ _SUNLIGHT_SOFT_SHADOWS
            #pragma multi_compile _ _POINT_LIGHT_SHADOWS
            #pragma multi_compile _ _POINT_LIGHT_SOFT_SHADOWS
            #pragma multi_compile _ _SPOT_LIGHT_SHADOWS
            #pragma multi_compile _ _SPOT_LIGHT_SOFT_SHADOWS

			#include "SRPInclude.hlsl"

            struct VertexOutput {
                float4 clipPos : SV_POSITION;
                float4 worldPos : TEXCOORD0;
                float3 viewDir : TEXCOORD1;
                float4 screenPos : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            UNITY_INSTANCING_BUFFER_START(PerInstance)
                UNITY_DEFINE_INSTANCED_PROP(float3, _Color)
            UNITY_INSTANCING_BUFFER_END(PerInstance)

            VertexOutput Vertex(BasicVertexInput input) {
                VertexOutput output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                output.worldPos = GetWorldPosition(input.pos.xyz);
                output.clipPos = GetClipPosition(output.worldPos);
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
                float3 litColor = DefaultDirectionalLit(normal) * DefaultCascadedDirectionalShadow(input.worldPos);
                [loop]
                for (uint i = 0; i < pointLightCount; ++i) litColor += DefaultPointLit(input.worldPos, normal, uint3(lightTextureIndex, i + 1));
                [loop]
                for (i = 0; i < spotLightCount; ++i) litColor += DefaultSpotLit(input.worldPos, normal, uint3(lightTextureIndex, i + 1)) * DefaultSpotShadow(i, input.worldPos); 
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

            #include "SRPInclude.hlsl"
			
			#pragma vertex ShadowCasterVertex
			#pragma fragment ShadowCasterFragment

			ENDHLSL
        }
    }
}