Shader "RogueDrive/InteractableOutline"
{
    Properties
    {
        _MainTex ("Source", 2D) = "white" {}
        _OutlineMaskTex ("Silhouette Mask", 2D) = "black" {}
        _OutlineColor ("Outline Color", Color) = (0.24, 0.90, 1.0, 1.0)
        _OutlineWidth ("Outline Width (Pixels)", Range(1.0, 8.0)) = 2.2
    }

    SubShader
    {
        // ---------------------------------------------------------------------
        // Pass 0: Silhouette Mask
        // Рисует меши цели сплошным белым в R8-маску. Cull Off — чтобы односторонние
        // лоу-поли модели (лезвия, листы) давали цельный силуэт.
        // ---------------------------------------------------------------------
        Pass
        {
            Name "SilhouetteMask"
            ZWrite On
            ZTest LEqual
            Cull Off
            ColorMask R

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float4 screenPos : TEXCOORD0;
                float eyeDepth : TEXCOORD1;
            };

            UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);
            float _UseSceneDepth;

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.screenPos = ComputeScreenPos(o.pos);
                o.eyeDepth = -UnityObjectToViewPos(v.vertex).z;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // Части предмета, спрятанные за другой геометрией (полка, стол), не попадают в силуэт —
                // контур обводит только видимую часть.
                if (_UseSceneDepth > 0.5)
                {
                    float rawDepth = SAMPLE_DEPTH_TEXTURE_PROJ(_CameraDepthTexture, UNITY_PROJ_COORD(i.screenPos));
                    float sceneDepth = LinearEyeDepth(rawDepth);
                    if (i.eyeDepth > sceneDepth + 0.03 + sceneDepth * 0.01)
                    {
                        discard;
                    }
                }
                return fixed4(1, 1, 1, 1);
            }
            ENDCG
        }

        // ---------------------------------------------------------------------
        // Pass 1: Composite
        // Полноэкранный проход OnRenderImage: копирует кадр из _MainTex и
        // поверх него рисует контур по границе маски _OutlineMaskTex.
        // ---------------------------------------------------------------------
        Pass
        {
            Name "CompositeOutline"
            ZTest Always
            ZWrite Off
            Cull Off
            Blend Off

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            sampler2D _MainTex;
            sampler2D _OutlineMaskTex;
            float4 _OutlineMaskTex_TexelSize;
            fixed4 _OutlineColor;
            float _OutlineWidth;

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 scene = tex2D(_MainTex, i.uv);
                float center = tex2D(_OutlineMaskTex, i.uv).r;
                if (center > 0.5)
                {
                    // Внутри объекта: оставляем кадр без изменений
                    return scene;
                }

                float2 o1 = _OutlineMaskTex_TexelSize.xy * (_OutlineWidth * 0.55);
                float2 o2 = _OutlineMaskTex_TexelSize.xy * _OutlineWidth;

                // Ближнее кольцо
                float n_up   = tex2D(_OutlineMaskTex, i.uv + float2(0, o1.y)).r;
                float n_down = tex2D(_OutlineMaskTex, i.uv - float2(0, o1.y)).r;
                float n_left = tex2D(_OutlineMaskTex, i.uv - float2(o1.x, 0)).r;
                float n_rgt  = tex2D(_OutlineMaskTex, i.uv + float2(o1.x, 0)).r;

                // Дальнее кольцо (диагонали + оси)
                float n_ur = tex2D(_OutlineMaskTex, i.uv + float2( o2.x * 0.7071,  o2.y * 0.7071)).r;
                float n_ul = tex2D(_OutlineMaskTex, i.uv + float2(-o2.x * 0.7071,  o2.y * 0.7071)).r;
                float n_dr = tex2D(_OutlineMaskTex, i.uv + float2( o2.x * 0.7071, -o2.y * 0.7071)).r;
                float n_dl = tex2D(_OutlineMaskTex, i.uv + float2(-o2.x * 0.7071, -o2.y * 0.7071)).r;
                float n_u2 = tex2D(_OutlineMaskTex, i.uv + float2(0, o2.y)).r;
                float n_d2 = tex2D(_OutlineMaskTex, i.uv - float2(0, o2.y)).r;
                float n_l2 = tex2D(_OutlineMaskTex, i.uv - float2(o2.x, 0)).r;
                float n_r2 = tex2D(_OutlineMaskTex, i.uv + float2(o2.x, 0)).r;

                float maxInner = max(max(n_up, n_down), max(n_left, n_rgt));
                float maxOuter = max(max(max(n_ur, n_ul), max(n_dr, n_dl)), max(max(n_u2, n_d2), max(n_l2, n_r2)));

                float edge = max(maxInner, maxOuter * 0.75);
                if (edge < 0.05)
                {
                    return scene;
                }

                float alpha = saturate(edge) * _OutlineColor.a;
                return fixed4(lerp(scene.rgb, _OutlineColor.rgb, alpha), scene.a);
            }
            ENDCG
        }
    }
    FallBack Off
}
