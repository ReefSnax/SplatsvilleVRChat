/*
 *	Created by Martin Reintges
 */

Shader "DepthMap/LinearDepthShader"
{
	Properties {}

	SubShader 
	{
		Tags { "RenderType"="Opaque" }
		Pass
		{
			CGPROGRAM
			#pragma target 2.0
			#pragma vertex vert
			#pragma fragment frag

			#include "UnityCG.cginc"

			uniform sampler2D _LastCameraDepthTexture;

		////// Input structs
			struct VertexInput 
			{
				half4 vertex : POSITION;
			};

			struct Vert2Frag
			{
				half4 pos : SV_POSITION;
				half4 posScreen : TEXCOORD0;
			};

			Vert2Frag vert( VertexInput vertIn )
			{
				Vert2Frag output;
				output.pos = UnityObjectToClipPos (vertIn.vertex);
				output.posScreen = ComputeScreenPos(output.pos);
				return output;
			}    

			float4 frag (Vert2Frag fragIn) : COLOR
			{
				float depth = Linear01Depth(SAMPLE_DEPTH_TEXTURE_PROJ(_LastCameraDepthTexture, UNITY_PROJ_COORD(fragIn.posScreen)));
				return float4(depth, depth, depth, 1);
			}
			ENDCG
		}
	} 
	FallBack "Diffuse"
}