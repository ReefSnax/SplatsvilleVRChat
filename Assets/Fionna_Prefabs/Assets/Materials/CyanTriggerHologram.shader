// Made with Amplify Shader Editor by orels1, used with permission
// Available at the Unity Asset Store - http://u3d.as/y3X 
Shader "CyanTrigger/CyanHologram"
{
	Properties
	{
		_MainTex("MainTex", 2D) = "white" {}
		_Masks("Masks", 2D) = "white" {}
		_InternalFrame("Internal Frame", 2D) = "white" {}
		_HexaFrame("Hexa Frame", 2D) = "white" {}
		[HideInInspector] _texcoord( "", 2D ) = "white" {}
		[HideInInspector] __dirty( "", Int ) = 1
	}

	SubShader
	{
		Tags{ "RenderType" = "Transparent"  "Queue" = "Transparent+0" "IgnoreProjector" = "True" "IsEmissive" = "true"  }
		Cull Off
		CGINCLUDE
		#include "UnityCG.cginc"
		#include "UnityShaderVariables.cginc"
		#include "UnityPBSLighting.cginc"
		#include "Lighting.cginc"
		#pragma target 5.0
		#ifdef UNITY_PASS_SHADOWCASTER
			#undef INTERNAL_DATA
			#undef WorldReflectionVector
			#undef WorldNormalVector
			#define INTERNAL_DATA half3 internalSurfaceTtoW0; half3 internalSurfaceTtoW1; half3 internalSurfaceTtoW2;
			#define WorldReflectionVector(data,normal) reflect (data.worldRefl, half3(dot(data.internalSurfaceTtoW0,normal), dot(data.internalSurfaceTtoW1,normal), dot(data.internalSurfaceTtoW2,normal)))
			#define WorldNormalVector(data,normal) half3(dot(data.internalSurfaceTtoW0,normal), dot(data.internalSurfaceTtoW1,normal), dot(data.internalSurfaceTtoW2,normal))
		#endif
		struct Input
		{
			float2 uv_texcoord;
			float3 viewDir;
			INTERNAL_DATA
			half ASEVFace : VFACE;
		};

		uniform sampler2D _MainTex;
		uniform float4 _MainTex_ST;
		uniform sampler2D _InternalFrame;
		uniform float4 _InternalFrame_ST;
		uniform sampler2D _HexaFrame;
		uniform float4 _HexaFrame_ST;
		uniform sampler2D _Masks;
		uniform float4 _Masks_ST;

		void surf( Input i , inout SurfaceOutputStandard o )
		{
			o.Normal = float3(0,0,1);
			float2 uv_MainTex = i.uv_texcoord * _MainTex_ST.xy + _MainTex_ST.zw;
			float4 tex2DNode1 = tex2D( _MainTex, uv_MainTex );
			float2 uv_InternalFrame = i.uv_texcoord * _InternalFrame_ST.xy + _InternalFrame_ST.zw;
			float4 tex2DNode39 = tex2D( _InternalFrame, uv_InternalFrame );
			float2 paralaxOffset66 = ParallaxOffset( 0 , 0.05 , i.viewDir );
			float2 paralaxOffset68 = ParallaxOffset( 0 , 0.1 , i.viewDir );
			float2 paralaxOffset71 = ParallaxOffset( 0 , 0.15 , i.viewDir );
			float temp_output_76_0 = saturate( ( ( tex2DNode39.a * 0.6 ) + ( tex2D( _InternalFrame, ( uv_InternalFrame + paralaxOffset66 ) ).a * 0.4 ) + ( tex2D( _InternalFrame, ( uv_InternalFrame + paralaxOffset68 ) ).a * 0.2 ) + ( tex2D( _InternalFrame, ( uv_InternalFrame + paralaxOffset71 ) ).a * 0.1 ) ) );
			float4 lerpResult40 = lerp( tex2DNode1 , tex2DNode39 , temp_output_76_0);
			o.Albedo = lerpResult40.rgb;
			float4 color3 = IsGammaSpace() ? float4(0.3726415,0.919261,1,0) : float4(0.1144948,0.8260663,1,0);
			float2 uv_HexaFrame = i.uv_texcoord * _HexaFrame_ST.xy + _HexaFrame_ST.zw;
			float2 paralaxOffset51 = ParallaxOffset( 0 , 0.4 , i.viewDir );
			float temp_output_54_0 = ( tex2D( _HexaFrame, ( uv_HexaFrame + paralaxOffset51 ) ).a * 0.14 );
			float mulTime18 = _Time.y * 0.2;
			float temp_output_23_0 = ( ( cos( ( ( mulTime18 * UNITY_PI ) % UNITY_PI ) ) + 1.0 ) / 2.0 );
			float clampResult26 = clamp( temp_output_23_0 , 0.001 , 0.999 );
			float2 uv_Masks = i.uv_texcoord * _Masks_ST.xy + _Masks_ST.zw;
			float2 paralaxOffset44 = ParallaxOffset( 0 , 0.43 , i.viewDir );
			float4 tex2DNode4 = tex2D( _Masks, ( uv_Masks + paralaxOffset44 ) );
			float temp_output_3_0_g5 = ( clampResult26 - tex2DNode4.r );
			float temp_output_3_0_g4 = ( saturate( ( clampResult26 - -0.07 ) ) - tex2DNode4.r );
			float temp_output_15_0 = ( ( saturate( ( i.ASEVFace < 0.0 ? 0.0 : temp_output_54_0 ) ) + tex2DNode1.a ) + ( i.ASEVFace < 0.0 ? 0.0 : ( ( saturate( ( ( 1.0 - saturate( ( temp_output_3_0_g5 / fwidth( temp_output_3_0_g5 ) ) ) ) - ( 1.0 - saturate( ( temp_output_3_0_g4 / fwidth( temp_output_3_0_g4 ) ) ) ) ) ) * 0.25 ) * saturate( (0.0 + (temp_output_23_0 - 0.0) * (1.0 - 0.0) / (0.1 - 0.0)) ) ) ) );
			float4 color82 = IsGammaSpace() ? float4(1,0.3725491,0.5549218,0) : float4(1,0.1144354,0.268443,0);
			o.Emission = ( ( color3 * temp_output_15_0 ) + ( temp_output_76_0 * color82 ) ).rgb;
			o.Metallic = 0.6718225;
			o.Smoothness = 0.6560131;
			o.Alpha = saturate( ( temp_output_15_0 + temp_output_76_0 ) );
		}

		ENDCG
		CGPROGRAM
		#pragma surface surf Standard alpha:fade keepalpha fullforwardshadows 

		ENDCG
		Pass
		{
			Name "ShadowCaster"
			Tags{ "LightMode" = "ShadowCaster" }
			ZWrite On
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma target 5.0
			#pragma multi_compile_shadowcaster
			#pragma multi_compile UNITY_PASS_SHADOWCASTER
			#pragma skip_variants FOG_LINEAR FOG_EXP FOG_EXP2
			#include "HLSLSupport.cginc"
			#if ( SHADER_API_D3D11 || SHADER_API_GLCORE || SHADER_API_GLES || SHADER_API_GLES3 || SHADER_API_METAL || SHADER_API_VULKAN )
				#define CAN_SKIP_VPOS
			#endif
			#include "UnityCG.cginc"
			#include "Lighting.cginc"
			#include "UnityPBSLighting.cginc"
			sampler3D _DitherMaskLOD;
			struct v2f
			{
				V2F_SHADOW_CASTER;
				float2 customPack1 : TEXCOORD1;
				float4 tSpace0 : TEXCOORD2;
				float4 tSpace1 : TEXCOORD3;
				float4 tSpace2 : TEXCOORD4;
				UNITY_VERTEX_INPUT_INSTANCE_ID
				UNITY_VERTEX_OUTPUT_STEREO
			};
			v2f vert( appdata_full v )
			{
				v2f o;
				UNITY_SETUP_INSTANCE_ID( v );
				UNITY_INITIALIZE_OUTPUT( v2f, o );
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO( o );
				UNITY_TRANSFER_INSTANCE_ID( v, o );
				Input customInputData;
				float3 worldPos = mul( unity_ObjectToWorld, v.vertex ).xyz;
				half3 worldNormal = UnityObjectToWorldNormal( v.normal );
				half3 worldTangent = UnityObjectToWorldDir( v.tangent.xyz );
				half tangentSign = v.tangent.w * unity_WorldTransformParams.w;
				half3 worldBinormal = cross( worldNormal, worldTangent ) * tangentSign;
				o.tSpace0 = float4( worldTangent.x, worldBinormal.x, worldNormal.x, worldPos.x );
				o.tSpace1 = float4( worldTangent.y, worldBinormal.y, worldNormal.y, worldPos.y );
				o.tSpace2 = float4( worldTangent.z, worldBinormal.z, worldNormal.z, worldPos.z );
				o.customPack1.xy = customInputData.uv_texcoord;
				o.customPack1.xy = v.texcoord;
				TRANSFER_SHADOW_CASTER_NORMALOFFSET( o )
				return o;
			}
			half4 frag( v2f IN
			#if !defined( CAN_SKIP_VPOS )
			, UNITY_VPOS_TYPE vpos : VPOS
			#endif
			) : SV_Target
			{
				UNITY_SETUP_INSTANCE_ID( IN );
				Input surfIN;
				UNITY_INITIALIZE_OUTPUT( Input, surfIN );
				surfIN.uv_texcoord = IN.customPack1.xy;
				float3 worldPos = float3( IN.tSpace0.w, IN.tSpace1.w, IN.tSpace2.w );
				half3 worldViewDir = normalize( UnityWorldSpaceViewDir( worldPos ) );
				surfIN.viewDir = IN.tSpace0.xyz * worldViewDir.x + IN.tSpace1.xyz * worldViewDir.y + IN.tSpace2.xyz * worldViewDir.z;
				surfIN.internalSurfaceTtoW0 = IN.tSpace0.xyz;
				surfIN.internalSurfaceTtoW1 = IN.tSpace1.xyz;
				surfIN.internalSurfaceTtoW2 = IN.tSpace2.xyz;
				SurfaceOutputStandard o;
				UNITY_INITIALIZE_OUTPUT( SurfaceOutputStandard, o )
				surf( surfIN, o );
				#if defined( CAN_SKIP_VPOS )
				float2 vpos = IN.pos;
				#endif
				half alphaRef = tex3D( _DitherMaskLOD, float3( vpos.xy * 0.25, o.Alpha * 0.9375 ) ).a;
				clip( alphaRef - 0.01 );
				SHADOW_CASTER_FRAGMENT( IN )
			}
			ENDCG
		}
	}
	Fallback "Diffuse"
	CustomEditor "ASEMaterialInspector"
}
/*ASEBEGIN
Version=18912
1101;172;1251;1072;1243.596;806.3981;1.6;True;False
Node;AmplifyShaderEditor.SimpleTimeNode;18;-3375.066,922.7434;Inherit;False;1;0;FLOAT;0.2;False;1;FLOAT;0
Node;AmplifyShaderEditor.PiNode;29;-3187.195,871.8904;Inherit;False;1;0;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.PiNode;33;-3266.301,635.9919;Inherit;False;1;0;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleRemainderNode;32;-2956.949,623.2787;Inherit;False;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.CosOpNode;31;-2734.858,624.9536;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleAddOpNode;21;-2578.707,848.5782;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.ViewDirInputsCoordNode;47;-2903.879,313.9295;Inherit;False;Tangent;False;0;4;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3
Node;AmplifyShaderEditor.SimpleDivideOpNode;23;-2402.707,848.5782;Inherit;False;2;0;FLOAT;0;False;1;FLOAT;2;False;1;FLOAT;0
Node;AmplifyShaderEditor.TextureCoordinatesNode;46;-2574.879,125.9295;Inherit;False;0;4;2;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT2;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.ParallaxOffsetHlpNode;44;-2606.879,308.9295;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0.43;False;2;FLOAT3;0,0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.ClampOpNode;26;-2162.706,832.5782;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0.001;False;2;FLOAT;0.999;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleAddOpNode;45;-2254.879,214.9296;Inherit;False;2;2;0;FLOAT2;0,0;False;1;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SimpleSubtractOpNode;9;-1938.707,800.5782;Inherit;False;2;0;FLOAT;0;False;1;FLOAT;-0.07;False;1;FLOAT;0
Node;AmplifyShaderEditor.SaturateNode;11;-1826.707,976.5784;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SamplerNode;4;-2098.958,342.8091;Inherit;True;Property;_Masks;Masks;1;0;Create;True;0;0;0;False;0;False;-1;18a99bb6c273369429a81afb73c0e912;18a99bb6c273369429a81afb73c0e912;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.ViewDirInputsCoordNode;49;-1171.282,-580.6673;Inherit;False;Tangent;False;0;4;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3
Node;AmplifyShaderEditor.ParallaxOffsetHlpNode;51;-874.2825,-585.6673;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0.4;False;2;FLOAT3;0,0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.FunctionNode;7;-1730.707,768.5782;Inherit;True;Step Antialiasing;-1;;4;2a825e80dfb3290468194f83380797bd;0;2;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.TextureCoordinatesNode;50;-884.2858,-745.3322;Inherit;False;0;48;2;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT2;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.FunctionNode;5;-1782.958,458.809;Inherit;True;Step Antialiasing;-1;;5;2a825e80dfb3290468194f83380797bd;0;2;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.ViewDirInputsCoordNode;72;-1909.301,-88.25208;Inherit;False;Tangent;False;0;4;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3
Node;AmplifyShaderEditor.OneMinusNode;6;-1560.958,503.8091;Inherit;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleAddOpNode;52;-620.3679,-660.5574;Inherit;False;2;2;0;FLOAT2;0,0;False;1;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.OneMinusNode;12;-1506.707,832.5782;Inherit;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.ParallaxOffsetHlpNode;66;-1651.047,-53.17474;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0.05;False;2;FLOAT3;0,0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SimpleSubtractOpNode;13;-1330.707,768.5782;Inherit;True;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.ParallaxOffsetHlpNode;68;-1650.392,60.14362;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0.1;False;2;FLOAT3;0,0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.TextureCoordinatesNode;65;-1655.396,-248.7131;Inherit;False;0;39;2;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT2;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.ParallaxOffsetHlpNode;71;-1647.962,188.0405;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0.15;False;2;FLOAT3;0,0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SamplerNode;48;-556.5851,-464.5422;Inherit;True;Property;_HexaFrame;Hexa Frame;3;0;Create;True;0;0;0;False;0;False;-1;None;f5f5602e2e302784f9b3863f6a35b13b;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SimpleAddOpNode;69;-1386.047,28.82526;Inherit;False;2;2;0;FLOAT2;0,0;False;1;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SimpleAddOpNode;67;-1380.047,-93.17474;Inherit;False;2;2;0;FLOAT2;0,0;False;1;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;54;-177.4133,-338.9476;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0.14;False;1;FLOAT;0
Node;AmplifyShaderEditor.TFHCRemapNode;34;-2210.707,1072.578;Inherit;False;5;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0.1;False;3;FLOAT;0;False;4;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.SaturateNode;14;-1106.707,848.5782;Inherit;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.FaceVariableNode;55;-176.6773,-229.5859;Inherit;False;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleAddOpNode;73;-1378.497,168.332;Inherit;False;2;2;0;FLOAT2;0,0;False;1;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.Compare;63;-9.669469,-425.6073;Inherit;False;4;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SaturateNode;35;-2034.707,1088.578;Inherit;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;17;-914.7071,912.5783;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0.25;False;1;FLOAT;0
Node;AmplifyShaderEditor.SamplerNode;70;-1224.227,148.466;Inherit;True;Property;_InternalFrame2;Internal Frame;2;0;Create;True;0;0;0;False;0;False;-1;None;None;True;0;False;white;Auto;False;Instance;39;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SamplerNode;74;-1227.971,365.6352;Inherit;True;Property;_InternalFrame3;Internal Frame;2;0;Create;True;0;0;0;False;0;False;-1;None;None;True;0;False;white;Auto;False;Instance;39;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SamplerNode;64;-1221.939,-75.49341;Inherit;True;Property;_InternalFrame1;Internal Frame;2;0;Create;True;0;0;0;False;0;False;-1;None;None;True;0;False;white;Auto;False;Instance;39;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SamplerNode;39;-1348.98,-303.1609;Inherit;True;Property;_InternalFrame;Internal Frame;2;0;Create;True;0;0;0;False;0;False;-1;7a4625bdcd604654e93c9b62025f8d0a;7a4625bdcd604654e93c9b62025f8d0a;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;80;-886.0042,285.9225;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0.1;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;77;-890.7226,-122.2327;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0.6;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;78;-890.7227,8.707275;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0.4;False;1;FLOAT;0
Node;AmplifyShaderEditor.SamplerNode;1;-655.3192,188.1126;Inherit;True;Property;_MainTex;MainTex;0;0;Create;True;0;0;0;False;0;False;-1;None;d3bd019b03280364ab2f13dc342e7807;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.FaceVariableNode;58;-356.3707,583.7986;Inherit;False;0;1;FLOAT;0
Node;AmplifyShaderEditor.SaturateNode;57;197.0223,-200.8498;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;36;-470.5326,1073.465;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;79;-890.7227,144.3658;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0.2;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleAddOpNode;53;-21.00585,102.4206;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleAddOpNode;75;-706.0909,37.70995;Inherit;False;4;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.Compare;62;-123.4308,628.9421;Inherit;False;4;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.ColorNode;3;113.1482,-621.7997;Inherit;False;Constant;_Color0;Color 0;1;1;[HDR];Create;True;0;0;0;False;0;False;0.3726415,0.919261,1,0;0,0,0,0;True;0;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SimpleAddOpNode;15;232.0526,288.734;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.ColorNode;82;-654.7963,462.4022;Inherit;False;Constant;_Color1;Color 1;4;1;[HDR];Create;True;0;0;0;False;0;False;1,0.3725491,0.5549218,0;0,0,0,0;True;0;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SaturateNode;76;-537.7289,-37.3815;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;2;414.6258,-213.8047;Inherit;False;2;2;0;COLOR;0,0,0,0;False;1;FLOAT;0;False;1;COLOR;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;81;-176.3963,366.4021;Inherit;False;2;2;0;FLOAT;0;False;1;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.SimpleAddOpNode;84;335.6033,406.4021;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;8;-2255.258,605.209;Inherit;False;Constant;_Progr;Progr;2;0;Create;True;0;0;0;False;0;False;1;0;0;1;0;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp;40;-197.4638,-133.247;Inherit;False;3;0;COLOR;0,0,0,0;False;1;COLOR;0,0,0,0;False;2;FLOAT;0;False;1;COLOR;0
Node;AmplifyShaderEditor.RangedFloatNode;37;316.2723,34.94076;Inherit;False;Constant;_Float0;Float 0;2;0;Create;True;0;0;0;False;0;False;0.6718225;0;0;1;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;38;342.9367,154.9301;Inherit;False;Constant;_Float1;Float 1;2;0;Create;True;0;0;0;False;0;False;0.6560131;0;0;1;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleSubtractOpNode;56;11.03168,-251.4104;Inherit;False;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SaturateNode;16;413.2499,283.5762;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleAddOpNode;83;578.8037,-206.3976;Inherit;False;2;2;0;COLOR;0,0,0,0;False;1;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.StandardSurfaceOutputNode;0;690.8423,-25.4;Float;False;True;-1;7;ASEMaterialInspector;0;0;Standard;Cyan/Cyan BDay UI;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;False;False;False;False;False;False;Off;0;False;-1;0;False;-1;False;0;False;-1;0;False;-1;False;0;Transparent;0.5;True;True;0;False;Transparent;;Transparent;All;18;all;True;True;True;True;0;False;-1;False;0;False;-1;255;False;-1;255;False;-1;0;False;-1;0;False;-1;0;False;-1;0;False;-1;0;False;-1;0;False;-1;0;False;-1;0;False;-1;False;2;15;10;25;False;0.5;True;2;5;False;-1;10;False;-1;0;0;False;-1;0;False;-1;0;False;-1;0;False;-1;0;False;0;0,0,0,0;VertexOffset;True;False;Cylindrical;False;Relative;0;;-1;-1;-1;-1;0;False;0;0;False;-1;-1;0;False;-1;0;0;0;False;0.1;False;-1;0;False;-1;False;16;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;2;FLOAT3;0,0,0;False;3;FLOAT;0;False;4;FLOAT;0;False;5;FLOAT;0;False;6;FLOAT3;0,0,0;False;7;FLOAT3;0,0,0;False;8;FLOAT;0;False;9;FLOAT;0;False;10;FLOAT;0;False;13;FLOAT3;0,0,0;False;11;FLOAT3;0,0,0;False;12;FLOAT3;0,0,0;False;14;FLOAT4;0,0,0,0;False;15;FLOAT3;0,0,0;False;0
WireConnection;29;0;18;0
WireConnection;32;0;29;0
WireConnection;32;1;33;0
WireConnection;31;0;32;0
WireConnection;21;0;31;0
WireConnection;23;0;21;0
WireConnection;44;2;47;0
WireConnection;26;0;23;0
WireConnection;45;0;46;0
WireConnection;45;1;44;0
WireConnection;9;0;26;0
WireConnection;11;0;9;0
WireConnection;4;1;45;0
WireConnection;51;2;49;0
WireConnection;7;1;4;1
WireConnection;7;2;11;0
WireConnection;5;1;4;1
WireConnection;5;2;26;0
WireConnection;6;0;5;0
WireConnection;52;0;50;0
WireConnection;52;1;51;0
WireConnection;12;0;7;0
WireConnection;66;2;72;0
WireConnection;13;0;6;0
WireConnection;13;1;12;0
WireConnection;68;2;72;0
WireConnection;71;2;72;0
WireConnection;48;1;52;0
WireConnection;69;0;65;0
WireConnection;69;1;68;0
WireConnection;67;0;65;0
WireConnection;67;1;66;0
WireConnection;54;0;48;4
WireConnection;34;0;23;0
WireConnection;14;0;13;0
WireConnection;73;0;65;0
WireConnection;73;1;71;0
WireConnection;63;0;55;0
WireConnection;63;3;54;0
WireConnection;35;0;34;0
WireConnection;17;0;14;0
WireConnection;70;1;69;0
WireConnection;74;1;73;0
WireConnection;64;1;67;0
WireConnection;80;0;74;4
WireConnection;77;0;39;4
WireConnection;78;0;64;4
WireConnection;57;0;63;0
WireConnection;36;0;17;0
WireConnection;36;1;35;0
WireConnection;79;0;70;4
WireConnection;53;0;57;0
WireConnection;53;1;1;4
WireConnection;75;0;77;0
WireConnection;75;1;78;0
WireConnection;75;2;79;0
WireConnection;75;3;80;0
WireConnection;62;0;58;0
WireConnection;62;3;36;0
WireConnection;15;0;53;0
WireConnection;15;1;62;0
WireConnection;76;0;75;0
WireConnection;2;0;3;0
WireConnection;2;1;15;0
WireConnection;81;0;76;0
WireConnection;81;1;82;0
WireConnection;84;0;15;0
WireConnection;84;1;76;0
WireConnection;40;0;1;0
WireConnection;40;1;39;0
WireConnection;40;2;76;0
WireConnection;56;0;54;0
WireConnection;56;1;55;0
WireConnection;16;0;84;0
WireConnection;83;0;2;0
WireConnection;83;1;81;0
WireConnection;0;0;40;0
WireConnection;0;2;83;0
WireConnection;0;3;37;0
WireConnection;0;4;38;0
WireConnection;0;9;16;0
ASEEND*/
//CHKSM=A21995642E626DF3A5973169E2548A0D150FE5FA