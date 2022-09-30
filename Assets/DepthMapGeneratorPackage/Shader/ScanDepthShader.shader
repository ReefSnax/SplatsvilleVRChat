/*
 *	Created by Martin Reintges
 */

Shader "DepthMap/ScanDepthShader"
{
    Properties
    {
		_MainColor("Color", Color) = (1,0,0,0)
		_DepthTex("Depth", 2D) = "white" {}
		_EffectDistance("Effect distance", Range(-2, 4)) = 0.5
		_EffectSize("Effect size", Range(0.001, 20)) = 0.1
    }
    SubShader
    {
		Tags {"Queue" = "Transparent" "IgnoreProjector" = "True" "RenderType" = "Transparent"}
		Blend SrcAlpha One
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

			uniform sampler2D _DepthTex;
			uniform float4 _DepthTex_ST;
			uniform float4 _MainColor;
			uniform float _EffectDistance;
			uniform float _EffectSize;

			uniform sampler2D _LastCameraDepthTexture;

			Vert2Frag vert (VertexInput vertIn)
            {
				Vert2Frag output;
				output.vertex = UnityObjectToClipPos(vertIn.vertex);
				output.posScreen = ComputeScreenPos(output.vertex);
				output.uv = TRANSFORM_TEX(vertIn.uv, _DepthTex);
                return output;
            }

			float4 frag (Vert2Frag fragIn) : SV_Target
            {
				float4 col = _MainColor;
				float depth = 1 - tex2D(_DepthTex, fragIn.uv).z;
				col.a = 1 - abs(_EffectDistance - depth) * _EffectSize;
				return col;
			}
            ENDCG
        }
    }
}
