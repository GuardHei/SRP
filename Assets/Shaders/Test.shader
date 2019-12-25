Shader "SRP/Test" {

    Properties {
        _TestInt ("Test Int", Int) = -1
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

            float _TestInt;

            ImageVertexOutput Vertex(ImageVertexInput input) {
                ImageVertexOutput output;
                output.clipPos = GetClipPosition(GetWorldPosition(input.pos.xyz));
                output.uv = input.uv;
                return output;
            }

            float4 Fragment(ImageVertexOutput input) : SV_TARGET {

                if (_TestInt >= 0 && _TestInt <= 5) {
                    float3 dir = float3(0, 0, 0);
                    float a1 = (input.uv.x - .5) * 2;
                    float a2 = (input.uv.y - .5) * 2;
                    // return float4(input.uv, 0, 1);
                    switch (_TestInt) {
                        case 0: dir = float3(1, a1, a2); break;
                        case 1: dir = float3(-1, a1, a2); break;
                        case 2: dir = float3(a1, 1, a2); break;
                        case 3: dir = float3(a1, -1, a2); break;
                        case 4: dir = float3(a1, a2, 1); break;
                        case 5: dir = float3(a1, a2, -1); break;
                    }

                    dir = normalize(dir);
                    return SAMPLE_TEXTURECUBE(_PointLightShadowmap, sampler_PointLightShadowmap, dir) > 0;
                }

                float4 c = SAMPLE_TEXTURE2D(_OpaqueDepthTexture, sampler_OpaqueDepthTexture, input.uv);
                return c;
                float t = c.r + c.g + c.b;
                float a = 1 - c.r;
                // return float4(a, a , a, 1);
                
                uint2 lightTextureIndex = uint2(_ScreenParams.x * input.uv.x / 16.0, _ScreenParams.y * input.uv.y / 16.0);
                uint lightCount = _CulledPointLightTexture[uint3(lightTextureIndex, 0)];
                lightCount += _CulledSpotLightTexture[uint3(lightTextureIndex, 0)];
                // lightCount = min(lightCount, 1);

                float4 color = float4(0, 0, 0, 1);

/*
                [loop]
                for (uint i = 0; i < lightCount; ++i) {
                    PointLight light = _PointLightBuffer[_CulledPointLightTexture[uint3(lightTextureIndex, i + 1)]];
                    color.rgb = light.color;
                }

                // color = float4(lightTextureIndex.x / 160.0, lightTextureIndex.y / 90.0, 0, 1);

                // return float4(input.uv, 0, 1);
*/

                switch (lightCount) {
                    case 1: color.r = 1; break;
                    case 2: color.g = 1; break;
                    case 3: color.b = 1; break;
                    case 4: color.rg = float2(.5, .5); break;
                    case 5: color.rb = float2(.5, .5); break;
                    case 6: color.gb = float2(.5, .5); break;
                    case 7: color.rgb = float3(.5, .5, .5); break;
                    case 8: color.rg = float2(1, 1); break;
                    case 9: color.rb = float2(1, 1); break;
                    case 10: color.gb = float2(1, 1); break;
                }

/*
                if (lightCount == 1) color.r = 1;
                else if (lightCount == 2) color.g = 1;
                else if (lightCount == 3) color.b = 1;
                else if (lightCount == 4) color.rg = float2(.5, .5);
*/
                
                return color;
            }

		    ENDHLSL
        }
    }
}