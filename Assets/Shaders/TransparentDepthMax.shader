Shader "SRP/TransparentDepthMax" {

    SubShader {

        Tags {
            "RenderType"="Opaque"
        }

        Pass {

            Name "DepthMax"

            Tags {
                "LightMode"="DepthMax"
            }

            ZTest Greater
            ZWrite On
			Cull Front

            HLSLPROGRAM
		    
			#pragma target 3.5

			#pragma multi_compile_instancing

			#pragma vertex UnlitVertex
			#pragma fragment NoneFragment

			#include "SRPInclude.hlsl"

		    ENDHLSL
        }
    }
}
