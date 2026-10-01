Shader "RogueDrive/Journey Sky"
{
    Properties
    {
        _Horizon ("Horizon", Color) = (.64,.68,.67,1)
        _Storm ("Storm approach", Range(0,1)) = 0
        _SunDirection ("Sun direction", Vector) = (.65,.4,-.65,0)
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off ZWrite Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata { float4 vertex:POSITION; };
            struct v2f { float4 vertex:SV_POSITION; float3 direction:TEXCOORD0; };
            float4 _Horizon, _SunDirection;
            float _Storm;
            v2f vert(appdata v) { v2f o; o.vertex=UnityObjectToClipPos(v.vertex); o.direction=v.vertex.xyz; return o; }
            float hash(float2 p) { return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453); }
            float noise(float2 p)
            {
                float2 a=floor(p), f=frac(p); f=f*f*(3-2*f);
                return lerp(lerp(hash(a),hash(a+float2(1,0)),f.x),lerp(hash(a+float2(0,1)),hash(a+1),f.x),f.y);
            }
            fixed4 frag(v2f i):SV_Target
            {
                float3 d=normalize(i.direction);
                float height=saturate(d.y);
                float3 top=lerp(float3(.30,.43,.55),float3(.22,.29,.37),_Storm);
                float3 sky=lerp(_Horizon.rgb,top,pow(height,.55));
                float sun=saturate(dot(d,normalize(_SunDirection.xyz)));
                sky+=float3(.25,.13,.035)*pow(sun,12)*(1-_Storm*.8);
                float2 p=d.xz/max(.14,d.y+.12)*2.5+float2(_Time.y*.0009,0);
                float n=noise(p)*.58+noise(p*2.1+8)*.28+noise(p*4.3+19)*.14;
                float clouds=smoothstep(lerp(.48,.30,_Storm),.76,n)*smoothstep(.015,.18,height);
                float3 cloud=lerp(float3(.78,.77,.71),float3(.36,.42,.46),_Storm);
                cloud*=lerp(.78,1.07,n);
                sky=lerp(sky,cloud,clouds*.92);
                sky+=float3(1,.79,.46)*smoothstep(.99965,.9999,sun)*(1-clouds)*(1-_Storm*.8);
                return float4(sky,1);
            }
            ENDCG
        }
    }
    Fallback Off
}
