// Мягкий аддитивный световой конус: края силуэта и участок у камеры растворяются.
Shader "RogueDrive/LightShaft"
{
    Properties
    {
        _Color ("Color", Color) = (1, 0.85, 0.6, 1)
        _Intensity ("Intensity", Range(0, 2)) = 0.25
        _EdgePower ("Edge Softness", Range(0.5, 6)) = 2
        _NearFade ("Near Fade Distance", Float) = 1.5
    }
    SubShader
    {
        Tags { "Queue" = "Transparent+10" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Blend One One
        ZWrite Off
        Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "UnityCG.cginc"

            fixed4 _Color;
            half _Intensity, _EdgePower, _NearFade;

            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; fixed4 color : COLOR; };
            struct v2f
            {
                float4 pos : SV_POSITION;
                fixed4 color : COLOR;
                float3 worldNormal : TEXCOORD0;
                float3 viewDir : TEXCOORD1;
                float eyeDepth : TEXCOORD2;
                UNITY_FOG_COORDS(3)
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                float3 world = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                o.viewDir = _WorldSpaceCameraPos - world;
                o.eyeDepth = -UnityObjectToViewPos(v.vertex).z;
                o.color = v.color;
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                half facing = abs(dot(normalize(i.worldNormal), normalize(i.viewDir)));
                half edge = pow(facing, _EdgePower);
                half near = saturate((i.eyeDepth - 0.3) / _NearFade);
                fixed3 c = _Color.rgb * (_Intensity * edge * near * i.color.a);
                UNITY_APPLY_FOG_COLOR(i.fogCoord, c, fixed4(0, 0, 0, 0));
                return fixed4(c, 1);
            }
            ENDCG
        }
    }
}
