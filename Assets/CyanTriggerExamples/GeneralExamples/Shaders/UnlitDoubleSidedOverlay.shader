Shader "Unlit/UnlitDoubleSidedOverlay"
{
	Properties
	{
		_Color ("Color", Color) = (0, 0, 0, 1.0) 
		_Alpha ("Alpha", float) = 1.0
	}
	SubShader
	{
		Tags { "Queue"="Overlay-1" "ForceNoShadowCasting"="True" "IgnoreProjector"="True"}
		LOD 100
		Cull Off
		Blend SrcAlpha OneMinusSrcAlpha 
		ZWrite Off ZTest Off
		Lighting Off
		SeparateSpecular Off

		Pass
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			
			#include "UnityCG.cginc"
			
			float4 _Color;
			float _Alpha;

			struct appdata
			{
				float4 vertex : POSITION;
			};

			struct v2f
			{
				float4 vertex : SV_POSITION;
			};
			
			v2f vert (appdata v)
			{
				v2f o;
				o.vertex = UnityObjectToClipPos(v.vertex);
				return o;
			}
			
			fixed4 frag (v2f i) : SV_Target
			{
				return fixed4(_Color.rgb, _Alpha);
			}
			ENDCG
		}
	}
}
