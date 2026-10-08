Shader "PRGame/Room PBR"
{
    Properties
    {
        _Color ("Tint", Color) = (1,1,1,1)
        _MainTex ("Albedo", 2D) = "white" {}
        [Normal] _BumpMap ("Normal", 2D) = "bump" {}
        _RoughnessMap ("Roughness", 2D) = "white" {}
        _OcclusionMap ("Occlusion", 2D) = "white" {}
        _BumpScale ("Normal strength", Range(0,2)) = 0.35
        _SmoothnessScale ("Smoothness scale", Range(0,2)) = 1
        _Metallic ("Metallic", Range(0,1)) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 250
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0
        #include "UnityStandardUtils.cginc"
        sampler2D _MainTex, _BumpMap, _RoughnessMap, _OcclusionMap;
        fixed4 _Color;
        half _BumpScale, _SmoothnessScale, _Metallic;
        struct Input { float2 uv_MainTex; };
        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            o.Albedo = tex2D(_MainTex, IN.uv_MainTex).rgb * _Color.rgb;
            o.Normal = UnpackScaleNormal(tex2D(_BumpMap, IN.uv_MainTex), _BumpScale);
            o.Smoothness = saturate((1-tex2D(_RoughnessMap,IN.uv_MainTex).r)*_SmoothnessScale);
            o.Occlusion = lerp(1,tex2D(_OcclusionMap,IN.uv_MainTex).r,0.6);
            o.Metallic = _Metallic;
            o.Alpha = 1;
        }
        ENDCG
    }
    FallBack "Standard"
}
