/*
 *	Created by Martin Reintges
 */

Shader "DepthMap/BackgroundDepthShader"
{
    Properties
    {
		_MainTex("Background", 2D) = "white" {}
		_DepthTex("Depth", 2D) = "white" {}
    }
    SubShader
    {
		Tags {"Queue" = "Transparent" "IgnoreProjector" = "True" "RenderType" = "Transparent"}
		Blend SrcAlpha OneMinusSrcAlpha
		ZWrite Off
		Cull back

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct VertexInput
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Vert2Frag
            {
                float4 vertex : SV_POSITION;
				float2 uv : TEXCOORD0;
				half4 posScreen : TEXCOORD1;
            };

			uniform sampler2D _MainTex;
			uniform float4 _MainTex_ST;
			uniform sampler2D _DepthTex;

			uniform sampler2D _LastCameraDepthTexture;

			Vert2Frag vert (VertexInput vertIn)
            {
				Vert2Frag output;
				output.vertex = UnityObjectToClipPos(vertIn.vertex);
				output.posScreen = ComputeScreenPos(output.vertex);
				output.uv = TRANSFORM_TEX(vertIn.uv, _MainTex);
                return output;
            }

			float4 frag (Vert2Frag fragIn) : SV_Target
            {
				float4 col = tex2D(_MainTex, fragIn.uv);
				float depth = 1 - SAMPLE_DEPTH_TEXTURE_PROJ(_LastCameraDepthTexture, UNITY_PROJ_COORD(fragIn.posScreen));
				float texDepth = 1 - tex2D(_DepthTex, fragIn.uv).z;
				col.a = 1 - saturate((texDepth - depth) * 1000);
				return col;
			}
            ENDCG
        }
    }
}
