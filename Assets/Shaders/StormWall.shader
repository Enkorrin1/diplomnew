// Стена бури: клубящаяся пыль из процедурного шума в пространстве объекта.
// Клубы поднимаются и катятся вперёд, у земли плотнее, края и верх растворяются,
// вспышка молнии подсвечивает толщу изнутри.
Shader "RogueDrive/StormWall"
{
    Properties
    {
        _ColorLow ("Low Color", Color) = (0.16, 0.11, 0.08, 1)
        _ColorHigh ("High Color", Color) = (0.52, 0.43, 0.34, 1)
        _GlowColor ("Lightning Glow", Color) = (0.85, 0.8, 1, 1)
        _Opacity ("Opacity", Range(0, 1)) = 0.92
        _Coverage ("Coverage", Range(0, 1)) = 0.6
        _NoiseScale ("Noise Scale", Float) = 0.011
        _Scroll ("Scroll (units/s)", Vector) = (0, 3.5, 7, 0)
        _Seed ("Seed", Float) = 0
        _Flash ("Flash", Range(0, 1)) = 0
        _HazeDistance ("Haze Distance", Float) = 2400
    }
    SubShader
    {
        Tags { "Queue" = "Transparent+5" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _ColorLow, _ColorHigh, _GlowColor;
            float _Opacity, _Coverage, _NoiseScale, _Seed, _Flash, _HazeDistance;
            float4 _Scroll;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float3 local : TEXCOORD1; float3 world : TEXCOORD2; };

            float hash(float3 p)
            {
                p = frac(p * 0.3183099 + 0.1);
                p *= 17.0;
                return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
            }

            float noise(float3 x)
            {
                float3 i = floor(x);
                float3 f = frac(x);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(lerp(hash(i), hash(i + float3(1, 0, 0)), f.x),
                                 lerp(hash(i + float3(0, 1, 0)), hash(i + float3(1, 1, 0)), f.x), f.y),
                            lerp(lerp(hash(i + float3(0, 0, 1)), hash(i + float3(1, 0, 1)), f.x),
                                 lerp(hash(i + float3(0, 1, 1)), hash(i + float3(1, 1, 1)), f.x), f.y), f.z);
            }

            float fbm(float3 p, int octaves)
            {
                float v = 0, a = 0.5;
                for (int i = 0; i < octaves; i++) { v += a * noise(p); p = p * 2.03 + 17.1; a *= 0.5; }
                return v;
            }

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.local = v.vertex.xyz;
                o.world = mul(unity_ObjectToWorld, v.vertex).xyz;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // Клубы «закреплены» за стеной и прокручиваются во времени: подъём и накат вперёд.
                float3 p = (i.local - _Scroll.xyz * _Time.y) * _NoiseScale + _Seed;
                // Силуэт — 4 октавы; освещение и мелкая деталь — по 3 (стена почти во весь экран, считаем экономно).
                float n = fbm(p, 4);
                float toLight = fbm(p + float3(0, 0.35, 0.15), 3);       // шаг к свету сверху
                float detail = fbm(p * 2.7 + 3.3, 3);
                float v = i.uv.y;

                // Порог растёт с высотой: верх стены рвётся на отдельные кучевые клубы,
                // у земли пыль сплошная.
                float threshold = lerp(1.0 - _Coverage - 0.15, 1.0 - _Coverage + 0.3, smoothstep(0.3, 1.0, v));
                float density = saturate((n + (detail - 0.5) * 0.25 - threshold) * 4.0 + (1.0 - v) * (1.0 - v) * 0.6);
                float sides = smoothstep(0.0, 0.12, i.uv.x) * smoothstep(1.0, 0.88, i.uv.x);
                float alpha = saturate(density * sides * _Opacity);

                float lit = saturate((n - toLight) * 4.0 + 0.55);        // освещённая сторона клуба
                float3 col = lerp(_ColorLow.rgb, _ColorHigh.rgb, saturate(lit * 0.8 + v * 0.35));
                col *= lerp(1.1, 0.7, density);                          // толща темнее краёв
                col += _GlowColor.rgb * _Flash * (0.35 + detail) * 2.2;  // молния внутри толщи

                float d = distance(_WorldSpaceCameraPos, i.world);
                col = lerp(col, unity_FogColor.rgb, saturate(d / _HazeDistance) * 0.4);
                return fixed4(col, alpha);
            }
            ENDCG
        }
    }
}
