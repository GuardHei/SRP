Shader "SRP/DitherTransparentStencil" {

    SubShader {

        Tags {
            "RenderType" = "Opaque"
        }
        
        Stencil {
            Ref 1
            Comp Always
            Pass Replace
        }

        Pass {

            Name "STENCIL"

            Tags {
                "LightMode" = "Stencil"
            }

            ZTest LEqual
            ZWrite Off
			Cull Back

            HLSLPROGRAM

            #pragma target 5.0

            #pragma vertex UnlitVertex
            #pragma fragment NoneFragment
            #pragma multi_compile_instancing
            
            #include "SRPInclude.hlsl"

            ENDHLSL
        }
    }
}
