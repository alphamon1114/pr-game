Shader "PRGame/Keyboard RGB Wave"
{
    Properties
    {
        _Intensity ("LED brightness", Range(0,4)) = 1.6
        _Speed ("Wave cycles per second", Range(0,1)) = 0.12
        _Frequency ("Cycles per world unit", Float) = 1.35
        _Saturation ("Saturation", Range(0,1)) = 0.9
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata { float4 vertex : POSITION; };
            struct v2f { float4 position : SV_POSITION; float worldX : TEXCOORD0; };
            float _Intensity, _Speed, _Frequency, _Saturation;
            v2f vert(appdata v)
            {
                v2f o;
                o.position = UnityObjectToClipPos(v.vertex);
                o.worldX = mul(unity_ObjectToWorld, v.vertex).x;
                return o;
            }
            float4 frag(v2f i) : SV_Target
            {
                float phase = frac(i.worldX * _Frequency - _Time.y * _Speed);
                float3 hue = saturate(abs(frac(phase + float3(0, 2.0/3.0, 1.0/3.0)) * 6 - 3) - 1);
                return float4(lerp(float3(1,1,1), hue, _Saturation) * _Intensity, 1);
            }
            ENDCG
        }
    }
    Fallback "Unlit/Color"
}
