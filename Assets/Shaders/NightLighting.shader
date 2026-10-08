Shader "Hidden/PRGame/NightLighting"
{
    Properties { _MainTex("Source",2D)="white"{} }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        CGINCLUDE
        #include "UnityCG.cginc"
        sampler2D _MainTex, _CameraDepthNormalsTexture, _OcclusionTex;
        UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);
        float4 _MainTex_TexelSize, _OcclusionTex_TexelSize;
        float4 _ProjectionScale;
        float _Radius, _Strength, _Exposure;
        float _HasMonitor;
        float4x4 _ViewToWorld, _WorldToMonitor;
        float2 depthUV(float2 uv)
        {
            #if UNITY_UV_STARTS_AT_TOP
            if(_MainTex_TexelSize.y<0)uv.y=1-uv.y;
            #endif
            return uv;
        }
        float depthAt(float2 uv) { return LinearEyeDepth(SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture,depthUV(uv))); }
        float3 positionAt(float2 uv,float depth) { return float3((uv*2-1)*_ProjectionScale.xy*depth,-depth); }
        float4 occlusion(v2f_img i):SV_Target
        {
            float d=depthAt(i.uv);if(d>_ProjectionScale.z*.98)return 1;
            float unused;float3 n;DecodeDepthNormal(tex2D(_CameraDepthNormalsTexture,depthUV(i.uv)),unused,n);
            float3 p=positionAt(i.uv,d);float total=0;
            float2 scale=_Radius/(max(d,.1)*_ProjectionScale.xy)*.5;
            float rotation=frac(sin(dot(floor(i.uv/_MainTex_TexelSize.xy),float2(12.9898,78.233)))*43758.5453)*6.283;
            [unroll]for(int k=0;k<16;k++)
            {
                float angle=k*2.39996+rotation;
                float2 uv=i.uv+float2(cos(angle),sin(angle))*scale*sqrt((k+.5)/16.0);
                if(any(uv<0)||any(uv>1))continue;
                float3 diff=positionAt(uv,depthAt(uv))-p;float distance=length(diff);
                total+=max(0,dot(n,diff/max(distance,.0001))-.10)*(1-smoothstep(_Radius*.15,_Radius,distance));
            }
            return saturate(1-total/16.0*_Strength*3.5);
        }
        float3 aces(float3 x) { return saturate((x*(2.51*x+.03))/(x*(2.43*x+.59)+.14)); }
        float4 composite(v2f_img i):SV_Target
        {
            float d=depthAt(i.uv);float ao=0;float weight=0;
            // The unlit display is drawn after the deferred depth buffer. Intersect
            // its plane explicitly and reject any nearer foreground geometry.
            float3 origin=mul(_WorldToMonitor,mul(_ViewToWorld,float4(0,0,0,1))).xyz;
            float3 ray=mul(_WorldToMonitor,mul(_ViewToWorld,float4(positionAt(i.uv,1),0))).xyz;
            float t=-origin.z/(abs(ray.z)>.00001 ? ray.z : .00001);
            float3 screen=origin+ray*t;
            if(_HasMonitor>.5 && t>0 && t<d+.01 && abs(screen.x)<.5 && abs(screen.y)<.5)
                return float4(tex2D(_MainTex,i.uv).rgb,1);
            [unroll]for(int y=-1;y<=1;y++)[unroll]for(int x=-1;x<=1;x++)
            {
                float2 uv=i.uv+float2(x,y)*_OcclusionTex_TexelSize.xy;
                float w=exp(-abs(depthAt(uv)-d)*35)*((x==0&&y==0)?2:1);
                ao+=tex2D(_OcclusionTex,uv).r*w;weight+=w;
            }
            float3 c=tex2D(_MainTex,i.uv).rgb*(ao/max(weight,.001));
            c=aces(c*_Exposure);
            float2 q=i.uv*2-1;c*=1-.10*pow(saturate(dot(q,q)*.5),1.4);
            return float4(c,1);
        }
        ENDCG
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment occlusion
            #pragma target 3.0
            ENDCG
        }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment composite
            #pragma target 3.0
            ENDCG
        }
    }
    Fallback Off
}
