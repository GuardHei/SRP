Shader "SRP/Unlit" {
	
	SubShader {
		
		Pass {
		    HLSLPROGRAM
		    
			#pragma target 5.0

			#pragma multi_compile_instancing

			#pragma vertex UnlitVertex
			#pragma fragment UnlitFragment

			#include "SRPInclude.hlsl"

		    ENDHLSL
		}
	}
}