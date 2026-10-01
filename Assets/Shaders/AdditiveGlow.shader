// Яркое аддитивное свечение (разряды молний): HDR-цвет попадает в bloom.
Shader "RogueDrive/AdditiveGlow"
{
    Properties
    {
        [HDR] _Color ("Color", Color) = (3, 3, 4, 1)
    }
    SubShader
    {
        Tags { "Queue" = "Transparent+20" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Blend One One
        ZWrite Off
        Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            float4 _Color;
            struct appdata { float4 vertex : POSITION; float4 color : COLOR; };
            struct v2f { float4 pos : SV_POSITION; float4 color : COLOR; };

            v2f vert(appdata v) { v2f o; o.pos = UnityObjectToClipPos(v.vertex); o.color = v.color * _Color; return o; }
            float4 frag(v2f i) : SV_Target { return float4(i.color.rgb * i.color.a, 1); }
            ENDCG
        }
    }
}
