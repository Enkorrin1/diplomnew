Shader "Ithappy/Blood Overlay/URP"
{
    Properties
    {
        [MainTexture] _BaseMap ("Base Texture (UV0)", 2D) = "white" {}
        [MainColor] _BaseColor ("Base Color", Color) = (1,1,1,1)

        _BloodMask ("Blood Mask (UV1)", 2D) = "black" {}
        _BloodColor ("Blood Color", Color) = (0.35,0.015,0.02,1)
        _BloodAmount ("Blood Amount", Range(0,1)) = 1
        _BloodThreshold ("Blood Threshold", Range(0,1)) = 0.07
        _BloodSoftness ("Blood Edge Softness", Range(0.0001,0.25)) = 0.005
        [Toggle] _InvertMask ("Invert Mask", Float) = 0

        _Metallic ("Metallic", Range(0,1)) = 0
        _Smoothness ("Smoothness", Range(0,1)) = 0.35
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 200

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0

        sampler2D _BaseMap;
        sampler2D _BloodMask;

        struct Input
        {
            float2 uv_BaseMap;
            float2 uv2_BloodMask;
        };

        half4 _BaseColor;
        half4 _BloodColor;
        half _BloodAmount;
        half _BloodThreshold;
        half _BloodSoftness;
        half _InvertMask;
        half _Metallic;
        half _Smoothness;

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            fixed4 c = tex2D(_BaseMap, IN.uv_BaseMap) * _BaseColor;
            fixed maskValue = tex2D(_BloodMask, IN.uv2_BloodMask).r;
            maskValue = lerp(maskValue, 1.0 - maskValue, saturate(_InvertMask));

            half softness = max(_BloodSoftness, 0.0001);
            half bloodMask = smoothstep(_BloodThreshold - softness, _BloodThreshold + softness, maskValue);
            bloodMask = saturate(bloodMask * _BloodAmount);

            o.Albedo = lerp(c.rgb, _BloodColor.rgb, bloodMask);
            o.Metallic = _Metallic;
            o.Smoothness = _Smoothness;
            o.Alpha = c.a;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
