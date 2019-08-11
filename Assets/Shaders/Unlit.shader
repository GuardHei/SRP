Shader "SRP/Unlit" {
	
	SubShader {
		
		Pass {
		    HLSLPROGRAM
		    
			#pragma target 3.5

			#pragma multi_compile_instancing

			#pragma vertex UnlitVertex
			#pragma fragment UnlitFragment

			#include "SRPInclude.hlsl"

		    ENDHLSL
		}
	}
}