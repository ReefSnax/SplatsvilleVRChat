/*
 *	Created by Martin Reintges
 */

Shader "DepthMap/PortalDepthShader"
{
    Properties
    {
		_MainTex("Main", 2D) = "white" {}
		_SecondaryTex("Secondary", 2D) = "white" {}
		_EffectDistance("Effect distance", Range(-2, 4)) = 0.5
		_EffectSize("Effect size", Range(0.001, 100)) = 0.1
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
			uniform sampler2D _SecondaryTex;
			uniform float _EffectDistance;
			uniform float _EffectSize;
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
				float4 world1 = tex2D(_MainTex, fragIn.uv);
				float4 world2 = tex2D(_SecondaryTex, fragIn.uv);
				float depth = 1-Linear01Depth(SAMPLE_DEPTH_TEXTURE_PROJ(_LastCameraDepthTexture, UNITY_PROJ_COORD(fragIn.posScreen)));
				depth = saturate((_EffectDistance - depth) * _EffectSize);
				float4 col = world1 * depth + world2 * (1 - depth);
				return col;
			}
            ENDCG
        }
    }
}
