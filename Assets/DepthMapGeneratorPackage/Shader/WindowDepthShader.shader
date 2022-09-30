/*
 *	Created by Martin Reintges
 */

Shader "DepthMap/WindowDepthShader"
{
    Properties
    {
		_EffectDistance("Effect distance", Range(-1, 1)) = 0.5
    }
    SubShader
    {
		Tags {"Queue" = "Transparent" "IgnoreProjector" = "True" "RenderType" = "Opaque"}
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
            };

            struct Vert2Frag
            {
                float4 vertex : SV_POSITION;
				half4 posScreen : TEXCOORD0;
            };

			uniform float _EffectDistance;
			uniform sampler2D _LastCameraDepthTexture;

			Vert2Frag vert (VertexInput vertIn)
            {
				Vert2Frag output;
				output.vertex = UnityObjectToClipPos(vertIn.vertex);
				output.posScreen = ComputeScreenPos(output.vertex);
                return output;
            }

			float4 frag (Vert2Frag fragIn) : SV_Target
            {
				float4 col = float4(1, 1, 1, 1);
				float depth = SAMPLE_DEPTH_TEXTURE_PROJ(_LastCameraDepthTexture, UNITY_PROJ_COORD(fragIn.posScreen));
				col *= (depth - _EffectDistance);
				col.a = 1;
				return col;
			}
            ENDCG
        }
    }
}
