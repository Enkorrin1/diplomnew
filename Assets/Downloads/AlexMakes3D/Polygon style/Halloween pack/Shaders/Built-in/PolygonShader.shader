// Made with Amplify Shader Editor
// Available at the Unity Asset Store - http://u3d.as/y3X 
Shader "PolygonShader"
{
	Properties
	{
		_Color01("Color 01", Color) = (0,0,0,0)
		_Color02("Color 02", Color) = (0,0,0,0)
		_Color03("Color 03", Color) = (0,0,0,0)
		_Color04("Color 04", Color) = (0,0,0,0)
		_Color05("Color 05", Color) = (0,0,0,0)
		_Color06("Color 06", Color) = (0,0,0,0)
		_Color07("Color 07", Color) = (0,0,0,0)
		_Color08("Color 08", Color) = (0,0,0,0)
		_Color09("Color 09", Color) = (0,0,0,0)
		_Color10("Color 10", Color) = (0,0,0,0)
		_Color11("Color 11", Color) = (0,0,0,0)
		_Color12("Color 12", Color) = (0,0,0,0)
		_Smoothness("Smoothness", Range( 0 , 1)) = 0
		[Toggle]_Emission("Emission", Float) = 1
		[NoScaleOffset]_EmissionMap("Emission Map", 2D) = "white" {}
		[HDR]_EmissionColor("Emission Color", Color) = (0,0,0,0)
		[Toggle]_Blood("Blood", Float) = 1
		[NoScaleOffset]_BloodMap("Blood Map", 2D) = "white" {}
		_BloodColor("Blood Color", Color) = (0,0,0,0)
		_BloodIntensity("Blood Intensity", Range( 0 , 1)) = 0
		[NoScaleOffset]_Mask_1("Mask_1", 2D) = "white" {}
		[NoScaleOffset]_Mask_2("Mask_2", 2D) = "white" {}
		[NoScaleOffset]_Mask_3("Mask_3", 2D) = "white" {}
		[NoScaleOffset]_Mask_4("Mask_4", 2D) = "white" {}
		[HideInInspector] _texcoord2( "", 2D ) = "white" {}
		[HideInInspector] _texcoord( "", 2D ) = "white" {}
		[HideInInspector] __dirty( "", Int ) = 1
		[Header(Forward Rendering Options)]
		[ToggleOff] _SpecularHighlights("Specular Highlights", Float) = 1.0
		[ToggleOff] _GlossyReflections("Reflections", Float) = 1.0
	}

	SubShader
	{
		Tags{ "RenderType" = "Opaque"  "Queue" = "Geometry+0" "IsEmissive" = "true"  }
		Cull Off
		CGPROGRAM
		#pragma target 3.0
		#pragma shader_feature _SPECULARHIGHLIGHTS_OFF
		#pragma shader_feature _GLOSSYREFLECTIONS_OFF
		#pragma surface surf Standard keepalpha addshadow fullforwardshadows 
		struct Input
		{
			float2 uv_texcoord;
			float2 uv2_texcoord2;
		};

		uniform float _Blood;
		uniform sampler2D _Mask_1;
		uniform float4 _Color01;
		uniform float4 _Color02;
		uniform float4 _Color03;
		uniform sampler2D _Mask_2;
		uniform float4 _Color04;
		uniform float4 _Color05;
		uniform float4 _Color06;
		uniform sampler2D _Mask_3;
		uniform float4 _Color07;
		uniform float4 _Color08;
		uniform float4 _Color09;
		uniform sampler2D _Mask_4;
		uniform float4 _Color10;
		uniform float4 _Color11;
		uniform float4 _Color12;
		uniform float4 _BloodColor;
		uniform sampler2D _BloodMap;
		uniform float _BloodIntensity;
		uniform float _Emission;
		uniform float4 _EmissionColor;
		uniform sampler2D _EmissionMap;
		uniform float _Smoothness;

		void surf( Input i , inout SurfaceOutputStandard o )
		{
			float2 uv_Mask_11 = i.uv_texcoord;
			float4 tex2DNode1 = tex2D( _Mask_1, uv_Mask_11 );
			float2 uv_Mask_215 = i.uv_texcoord;
			float4 tex2DNode15 = tex2D( _Mask_2, uv_Mask_215 );
			float2 uv_Mask_322 = i.uv_texcoord;
			float4 tex2DNode22 = tex2D( _Mask_3, uv_Mask_322 );
			float2 uv_Mask_427 = i.uv_texcoord;
			float4 tex2DNode27 = tex2D( _Mask_4, uv_Mask_427 );
			float4 temp_output_36_0 = ( ( ( tex2DNode1.r * _Color01 ) + ( tex2DNode1.g * _Color02 ) + ( tex2DNode1.b * _Color03 ) + ( tex2DNode15.r * _Color04 ) + ( tex2DNode15.g * _Color05 ) + ( tex2DNode15.b * _Color06 ) ) + ( ( tex2DNode22.r * _Color07 ) + ( tex2DNode22.g * _Color08 ) + ( tex2DNode22.b * _Color09 ) + ( tex2DNode27.r * _Color10 ) + ( tex2DNode27.g * _Color11 ) + ( tex2DNode27.b * _Color12 ) ) );
			float4 lerpResult46 = lerp( temp_output_36_0 , _BloodColor , ( tex2D( _BloodMap, i.uv2_texcoord2 ) * _BloodIntensity ));
			o.Albedo = (( _Blood )?( lerpResult46 ):( temp_output_36_0 )).rgb;
			float2 uv_EmissionMap41 = i.uv_texcoord;
			o.Emission = (( _Emission )?( ( _EmissionColor * tex2D( _EmissionMap, uv_EmissionMap41 ) ) ):( float4( 0,0,0,0 ) )).rgb;
			o.Smoothness = _Smoothness;
			o.Alpha = 1;
		}

		ENDCG
	}
	Fallback "Diffuse"
	CustomEditor "ASEMaterialInspector"
}
/*ASEBEGIN
Version=18935
0;0;1536;803;-78.41235;-675.2248;1;True;True
Node;AmplifyShaderEditor.CommentaryNode;16;-990.3101,-175.4097;Inherit;False;737.6502;865.2629;;7;5;7;6;1;2;4;3;Mask_1;1,1,1,1;0;0
Node;AmplifyShaderEditor.CommentaryNode;17;-963.2468,795.7934;Inherit;False;645.9869;842.2725;;7;11;12;13;14;9;10;15;Mask_2;1,1,1,1;0;0
Node;AmplifyShaderEditor.CommentaryNode;18;-897.1801,1755.349;Inherit;False;645.9869;842.2725;;7;19;20;21;22;23;24;25;Mask_3;1,1,1,1;0;0
Node;AmplifyShaderEditor.CommentaryNode;26;-888,2718.624;Inherit;False;645.9869;842.2725;;7;33;32;31;30;29;28;27;Mask_4;1,1,1,1;0;0
Node;AmplifyShaderEditor.SamplerNode;1;-940.3101,-125.4097;Inherit;True;Property;_Mask_1;Mask_1;20;1;[NoScaleOffset];Create;True;0;0;0;False;0;False;-1;None;3c5705d8e48b07f47aa4786121fd605c;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SamplerNode;15;-913.2468,845.7934;Inherit;True;Property;_Mask_2;Mask_2;21;1;[NoScaleOffset];Create;True;0;0;0;False;0;False;-1;None;None;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.ColorNode;19;-831.6954,2222.795;Inherit;False;Property;_Color08;Color 08;7;0;Create;True;0;0;0;False;0;False;0,0,0,0;0,0,0,0;True;0;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.ColorNode;20;-829.2112,2408.241;Inherit;False;Property;_Color09;Color 09;8;0;Create;True;0;0;0;False;0;False;0,0,0,0;0,0,0,0;True;0;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SamplerNode;22;-840.5917,1824.969;Inherit;True;Property;_Mask_3;Mask_3;22;1;[NoScaleOffset];Create;True;0;0;0;False;0;False;-1;None;None;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.ColorNode;28;-825.2225,3000.478;Inherit;False;Property;_Color10;Color 10;9;0;Create;True;0;0;0;False;0;False;0,0,0,0;0,0,0,0;True;0;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.CommentaryNode;51;122.3594,666.7227;Inherit;False;1452.375;754.9315;;8;49;44;46;50;43;47;48;36;Color + Blood;1,1,1,1;0;0
Node;AmplifyShaderEditor.ColorNode;29;-823.8121,3186.07;Inherit;False;Property;_Color11;Color 11;10;0;Create;True;0;0;0;False;0;False;0,0,0,0;0,0,0,0;True;0;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.ColorNode;14;-907.6386,1056.73;Inherit;False;Property;_Color04;Color 04;3;0;Create;True;0;0;0;False;0;False;0,0,0,0;0,0,0,0;True;0;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.ColorNode;30;-820.0311,3371.516;Inherit;False;Property;_Color12;Color 12;11;0;Create;True;0;0;0;False;0;False;0,0,0,0;0,0,0,0;True;0;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.ColorNode;4;-908.2914,465.6013;Inherit;False;Property;_Color03;Color 03;2;0;Create;True;0;0;0;False;0;False;0,0,0,0;0,0,0,0;True;0;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.ColorNode;10;-901.8663,1429.065;Inherit;False;Property;_Color06;Color 06;5;0;Create;True;0;0;0;False;0;False;0,0,0,0;0,0,0,0;True;0;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.ColorNode;2;-912.1861,93.26614;Inherit;False;Property;_Color01;Color 01;0;0;Create;True;0;0;0;False;0;False;0,0,0,0;0.5754717,0.5754717,0.5754717,0;True;0;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SamplerNode;27;-831.4116,2788.244;Inherit;True;Property;_Mask_4;Mask_4;23;1;[NoScaleOffset];Create;True;0;0;0;False;0;False;-1;None;None;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.ColorNode;3;-911.7757,280.1552;Inherit;False;Property;_Color02;Color 02;1;0;Create;True;0;0;0;False;0;False;0,0,0,0;0.1792453,0.1792453,0.1792453,0;True;0;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.ColorNode;24;-833.1058,2034.508;Inherit;False;Property;_Color07;Color 07;6;0;Create;True;0;0;0;False;0;False;0,0,0,0;0,0,0,0;True;0;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.ColorNode;9;-905.3506,1243.619;Inherit;False;Property;_Color05;Color 05;4;0;Create;True;0;0;0;False;0;False;0,0,0,0;0,0,0,0;True;0;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;5;-486.0848,8.06489;Inherit;False;2;2;0;FLOAT;0;False;1;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;32;-419.381,3093.202;Inherit;False;2;2;0;FLOAT;0;False;1;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;23;-424.1302,1950.705;Inherit;False;2;2;0;FLOAT;0;False;1;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;25;-428.5609,2129.927;Inherit;False;2;2;0;FLOAT;0;False;1;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;11;-479.6597,971.5287;Inherit;False;2;2;0;FLOAT;0;False;1;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;13;-506.1091,1185.003;Inherit;False;2;2;0;FLOAT;0;False;1;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;31;-414.9503,2913.979;Inherit;False;2;2;0;FLOAT;0;False;1;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;6;-512.5342,221.5397;Inherit;False;2;2;0;FLOAT;0;False;1;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.TextureCoordinatesNode;43;305.2979,939.3526;Inherit;False;1;-1;2;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT2;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;21;-427.517,2289.769;Inherit;False;2;2;0;FLOAT;0;False;1;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;33;-418.3371,3253.044;Inherit;False;2;2;0;FLOAT;0;False;1;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;7;-499.2577,437.6508;Inherit;False;2;2;0;FLOAT;0;False;1;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;12;-492.8326,1401.115;Inherit;False;2;2;0;FLOAT;0;False;1;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.SimpleAddOpNode;34;-105.2571,989.6934;Inherit;False;6;6;0;COLOR;0,0,0,0;False;1;COLOR;0,0,0,0;False;2;COLOR;0,0,0,0;False;3;COLOR;0,0,0,0;False;4;COLOR;0,0,0,0;False;5;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.CommentaryNode;42;288.3225,1600.456;Inherit;False;684.517;471.9269;;4;38;39;40;41;Emission;1,1,1,1;0;0
Node;AmplifyShaderEditor.RangedFloatNode;49;758.8619,1305.654;Inherit;False;Property;_BloodIntensity;Blood Intensity;19;0;Create;True;0;0;0;False;0;False;0;1;0;1;0;1;FLOAT;0
Node;AmplifyShaderEditor.SamplerNode;44;528.0107,1126.38;Inherit;True;Property;_BloodMap;Blood Map;17;1;[NoScaleOffset];Create;True;0;0;0;False;0;False;-1;None;6307b9b10976b41449f187a67cbe9678;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SimpleAddOpNode;35;158.328,2482.853;Inherit;False;6;6;0;COLOR;0,0,0,0;False;1;COLOR;0,0,0,0;False;2;COLOR;0,0,0,0;False;3;COLOR;0,0,0,0;False;4;COLOR;0,0,0,0;False;5;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.SamplerNode;41;338.3226,1842.383;Inherit;True;Property;_EmissionMap;Emission Map;14;1;[NoScaleOffset];Create;True;0;0;0;False;0;False;-1;None;None;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;50;1053.889,1072.073;Inherit;False;2;2;0;COLOR;0,0,0,0;False;1;FLOAT;0;False;1;COLOR;0
Node;AmplifyShaderEditor.ColorNode;38;512.8456,1683.203;Inherit;False;Property;_EmissionColor;Emission Color;15;1;[HDR];Create;True;0;0;0;False;0;False;0,0,0,0;0,0,0,0;True;0;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.ColorNode;47;852.1246,949.7262;Inherit;False;Property;_BloodColor;Blood Color;18;0;Create;True;0;0;0;False;0;False;0,0,0,0;0.4627451,0,0,0;True;0;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SimpleAddOpNode;36;172.3594,1151.086;Inherit;True;2;2;0;COLOR;0,0,0,0;False;1;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.LerpOp;46;1204.93,716.7227;Inherit;False;3;0;COLOR;0,0,0,0;False;1;COLOR;0,0,0,0;False;2;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;40;763.1282,1799.347;Inherit;False;2;2;0;COLOR;0,0,0,0;False;1;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.ToggleSwitchNode;48;1350.735,1174.926;Inherit;False;Property;_Blood;Blood;16;0;Create;True;0;0;0;False;0;False;1;True;2;0;COLOR;0,0,0,0;False;1;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.ToggleSwitchNode;39;783.2338,1650.456;Inherit;False;Property;_Emission;Emission;13;0;Create;True;0;0;0;False;0;False;1;True;2;0;COLOR;0,0,0,0;False;1;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.RangedFloatNode;37;1171.926,1784.479;Inherit;False;Property;_Smoothness;Smoothness;12;0;Create;True;0;0;0;False;0;False;0;0;0;1;0;1;FLOAT;0
Node;AmplifyShaderEditor.StandardSurfaceOutputNode;0;1983.385,1075.999;Float;False;True;-1;2;ASEMaterialInspector;0;0;Standard;PolygonShader;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;True;False;Off;0;False;-1;0;False;-1;False;0;False;-1;0;False;-1;False;0;Opaque;0.5;True;True;0;False;Opaque;;Geometry;All;18;all;True;True;True;True;0;False;-1;False;0;False;-1;255;False;-1;255;False;-1;0;False;-1;0;False;-1;0;False;-1;0;False;-1;0;False;-1;0;False;-1;0;False;-1;0;False;-1;False;2;15;10;25;False;0.5;True;0;0;False;-1;0;False;-1;0;0;False;-1;0;False;-1;0;False;-1;0;False;-1;0;False;0;0,0,0,0;VertexOffset;True;False;Cylindrical;False;True;Relative;0;;-1;-1;-1;-1;0;False;0;0;False;-1;-1;0;False;-1;0;0;0;False;0.1;False;-1;0;False;-1;False;16;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;2;FLOAT3;0,0,0;False;3;FLOAT;0;False;4;FLOAT;0;False;5;FLOAT;0;False;6;FLOAT3;0,0,0;False;7;FLOAT3;0,0,0;False;8;FLOAT;0;False;9;FLOAT;0;False;10;FLOAT;0;False;13;FLOAT3;0,0,0;False;11;FLOAT3;0,0,0;False;12;FLOAT3;0,0,0;False;14;FLOAT4;0,0,0,0;False;15;FLOAT3;0,0,0;False;0
WireConnection;5;0;1;1
WireConnection;5;1;2;0
WireConnection;32;0;27;2
WireConnection;32;1;29;0
WireConnection;23;0;22;1
WireConnection;23;1;24;0
WireConnection;25;0;22;2
WireConnection;25;1;19;0
WireConnection;11;0;15;1
WireConnection;11;1;14;0
WireConnection;13;0;15;2
WireConnection;13;1;9;0
WireConnection;31;0;27;1
WireConnection;31;1;28;0
WireConnection;6;0;1;2
WireConnection;6;1;3;0
WireConnection;21;0;22;3
WireConnection;21;1;20;0
WireConnection;33;0;27;3
WireConnection;33;1;30;0
WireConnection;7;0;1;3
WireConnection;7;1;4;0
WireConnection;12;0;15;3
WireConnection;12;1;10;0
WireConnection;34;0;5;0
WireConnection;34;1;6;0
WireConnection;34;2;7;0
WireConnection;34;3;11;0
WireConnection;34;4;13;0
WireConnection;34;5;12;0
WireConnection;44;1;43;0
WireConnection;35;0;23;0
WireConnection;35;1;25;0
WireConnection;35;2;21;0
WireConnection;35;3;31;0
WireConnection;35;4;32;0
WireConnection;35;5;33;0
WireConnection;50;0;44;0
WireConnection;50;1;49;0
WireConnection;36;0;34;0
WireConnection;36;1;35;0
WireConnection;46;0;36;0
WireConnection;46;1;47;0
WireConnection;46;2;50;0
WireConnection;40;0;38;0
WireConnection;40;1;41;0
WireConnection;48;0;36;0
WireConnection;48;1;46;0
WireConnection;39;1;40;0
WireConnection;0;0;48;0
WireConnection;0;2;39;0
WireConnection;0;4;37;0
ASEEND*/
//CHKSM=424CBA306358DA75CCBE22BDCA6BB37FC93DEAE9