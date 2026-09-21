Shader "Hidden/RogueDrive/PostProcessScreenShader"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _VignetteColor ("Vignette Color", Color) = (0, 0, 0, 1)
        _VignetteIntensity ("Vignette Intensity", Range(0, 2)) = 0.35
        _VignetteSmoothness ("Vignette Smoothness", Range(0.01, 1)) = 0.5
        _VignetteRoundness ("Vignette Roundness", Range(0.1, 2)) = 1.0
        _ChromaticAberration ("Chromatic Aberration", Range(0, 0.03)) = 0.0
        _RadialBlurStrength ("Radial Blur Strength", Range(0, 0.05)) = 0.0
        _GlitchIntensity ("Glitch Intensity", Range(0, 0.08)) = 0.0
        _DamageFlash ("Damage Flash", Range(0, 1)) = 0.0
        _DamageColor ("Damage Color", Color) = (0.85, 0.05, 0.05, 1)
    }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;

            fixed4 _VignetteColor;
            float _VignetteIntensity;
            float _VignetteSmoothness;
            float _VignetteRoundness;

            float _ChromaticAberration;
            float _RadialBlurStrength;
            float _GlitchIntensity;

            float _DamageFlash;
            fixed4 _DamageColor;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float2 uv = i.uv;

                // 1. EMP / Hazard Glitch (горизонтальное смещение полос развертки)
                if (_GlitchIntensity > 0.001)
                {
                    float slice = sin(uv.y * 120.0 + _Time.y * 35.0);
                    if (slice > 0.85)
                    {
                        uv.x += (_GlitchIntensity * 0.75 * sin(_Time.y * 70.0));
                    }
                }

                // 2. Хроматическая аберрация (RGB расщепление при нитро и высокой скорости)
                float2 toCenter = uv - 0.5;
                float distFromCenter = length(toCenter);
                float2 caOffset = toCenter * (_ChromaticAberration * distFromCenter);

                float r = tex2D(_MainTex, uv + caOffset).r;
                float g = tex2D(_MainTex, uv).g;
                float b = tex2D(_MainTex, uv - caOffset).b;
                float a = tex2D(_MainTex, uv).a;
                fixed4 col = fixed4(r, g, b, a);

                // 3. Радиальное искажение скорости (Radial Speed Blur)
                if (_RadialBlurStrength > 0.001)
                {
                    fixed4 blurCol = col;
                    float2 blurDir = toCenter * _RadialBlurStrength;
                    for (int s = 1; s <= 4; s++)
                    {
                        blurCol += tex2D(_MainTex, uv - blurDir * (float)s * 0.25);
                    }
                    col = blurCol * 0.2;
                }

                // 4. Кинематографическая виньетка (Vignette)
                float2 d = abs(toCenter) * 2.0;
                d.x = pow(d.x, _VignetteRoundness);
                d.y = pow(d.y, _VignetteRoundness);
                float vFactor = length(d);
                float vignette = smoothstep(_VignetteIntensity, _VignetteIntensity - _VignetteSmoothness, vFactor);
                col.rgb = lerp(_VignetteColor.rgb, col.rgb, saturate(vignette));

                // 5. Вспышка урона (Damage Flash)
                if (_DamageFlash > 0.01)
                {
                    col.rgb = lerp(col.rgb, _DamageColor.rgb, _DamageFlash * 0.45);
                }

                return col;
            }
            ENDCG
        }
    }
    FallBack Off
}
