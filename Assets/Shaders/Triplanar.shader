// Standard-освещение с трипланарной текстурой в мировых координатах: профлист, бетон и краска
// ложатся с одинаковым шагом на стены любого размера без развёртки. Грязь — второй слой крупнее.
Shader "RogueDrive/Triplanar"
{
    Properties
    {
        _Color ("Color", Color) = (1, 1, 1, 1)
        _MainTex ("Albedo (A = smoothness mask)", 2D) = "white" {}
        _Scale ("Tiles per Meter", Float) = 0.25
        _Glossiness ("Smoothness", Range(0, 1)) = 0.15
        _Metallic ("Metallic", Range(0, 1)) = 0
        _Dirt ("Dirt", 2D) = "white" {}
        _DirtScale ("Dirt Tiles per Meter", Float) = 0.04
        _DirtStrength ("Dirt Strength", Range(0, 1)) = 0.5
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" }
        LOD 200
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows vertex:vert
        #pragma target 3.0

        sampler2D _MainTex, _Dirt;
        fixed4 _Color;
        half _Scale, _Glossiness, _Metallic, _DirtScale, _DirtStrength;

        struct Input { float3 worldPos; float3 worldNormalRaw; };

        void vert(inout appdata_full v, out Input o)
        {
            UNITY_INITIALIZE_OUTPUT(Input, o);
            o.worldNormalRaw = UnityObjectToWorldNormal(v.normal);
        }

        fixed4 Tri(sampler2D tex, float3 p, float3 w)
        {
            return tex2D(tex, p.zy) * w.x + tex2D(tex, p.xz) * w.y + tex2D(tex, p.xy) * w.z;
        }

        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            float3 w = pow(abs(normalize(IN.worldNormalRaw)), 4);
            w /= (w.x + w.y + w.z);
            fixed4 c = Tri(_MainTex, IN.worldPos * _Scale, w);
            fixed dirt = Tri(_Dirt, IN.worldPos * _DirtScale, w).r;
            o.Albedo = c.rgb * _Color.rgb * lerp(1, dirt, _DirtStrength);
            o.Metallic = _Metallic;
            o.Smoothness = _Glossiness * c.a;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
