/*
 *	Created by Martin Reintges
 */

Shader "DepthMap/ProjectionShader"
{
    Properties
    {
		_MainTex("Main", 2D) = "white" {}
    }
    SubShader
    {
		Tags {"IgnoreProjector" = "True" "RenderType" = "Opaque" "LightMode" = "ForwardBase"}
		Cull back

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
			#pragma multi_compile_fwdbase nolightmap nodirlightmap nodynlightmap novertexlight

            #include "UnityCG.cginc"
			#include "AutoLight.cginc"
			#include "Lighting.cginc"

            struct VertexInput
            {
                float4 vertex : POSITION;
            };

            struct Vert2Frag
            {
                float4 pos : SV_POSITION;
				half4 posScreen : TEXCOORD0;
				SHADOW_COORDS(1)
            };

			uniform sampler2D _MainTex;
			uniform float4 _MainTex_ST;

			Vert2Frag vert (VertexInput vertIn)
            {
				Vert2Frag output;
				output.pos = UnityObjectToClipPos(vertIn.vertex);
				output.posScreen = ComputeScreenPos(output.pos);
				TRANSFER_SHADOW(output)
                return output;
            }

			float4 frag (Vert2Frag fragIn) : SV_Target
            {
				return tex2Dproj(_MainTex, UNITY_PROJ_COORD(fragIn.posScreen)) * (SHADOW_ATTENUATION(fragIn) + UNITY_LIGHTMODEL_AMBIENT);
			}
            ENDCG
        }

		// shadow casting support
		UsePass "Legacy Shaders/VertexLit/SHADOWCASTER"
    }
}
