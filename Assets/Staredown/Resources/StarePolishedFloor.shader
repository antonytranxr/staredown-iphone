Shader "Staredown/PolishedFloor" {
Properties { _Color("Graphite",Color)=(.08,.095,.13,1) _Smoothness("Polish",Range(0,1))=.88 _Reflectivity("Reflection strength",Range(0,1))=.3 }
SubShader { Tags{"RenderType"="Opaque"} LOD 250
CGPROGRAM
#pragma surface surf Standard fullforwardshadows
#pragma target 3.0
#include "UnityCG.cginc"
sampler2D _ArenaReflection;float4 _ArenaReflection_TexelSize;float4x4 _ArenaReflectionVP;float _ArenaReflectionReady;
fixed4 _Color;half _Smoothness,_Reflectivity;
struct Input {float3 worldPos;};
void surf(Input IN,inout SurfaceOutputStandard o){
 o.Albedo=_Color.rgb;o.Metallic=.25;o.Smoothness=_Smoothness;
 float4 p=mul(_ArenaReflectionVP,float4(IN.worldPos,1));float safeW=max(.0001,p.w);float2 uv=p.xy/safeW*.5+.5;
 #if UNITY_UV_STARTS_AT_TOP
 uv.y=1-uv.y;
 #endif
 float valid=step(0,uv.x)*step(uv.x,1)*step(0,uv.y)*step(uv.y,1)*step(0,p.w)*_ArenaReflectionReady;
 float2 blur=_ArenaReflection_TexelSize.xy*1.8;
 float3 refl=tex2Dbias(_ArenaReflection,float4(uv,0,1.5)).rgb*.4;
 refl+=tex2Dbias(_ArenaReflection,float4(uv+float2(blur.x,0),0,1.5)).rgb*.15;
 refl+=tex2Dbias(_ArenaReflection,float4(uv-float2(blur.x,0),0,1.5)).rgb*.15;
 refl+=tex2Dbias(_ArenaReflection,float4(uv+float2(0,blur.y),0,1.5)).rgb*.15;
 refl+=tex2Dbias(_ArenaReflection,float4(uv-float2(0,blur.y),0,1.5)).rgb*.15;
 float2 coolDelta=(IN.worldPos.xz-float2(-5,0))/float2(10,6);
 float2 warmDelta=(IN.worldPos.xz-float2(7,2))/float2(7,8);
 float3 softboxes=exp(-dot(coolDelta,coolDelta))*float3(.013,.032,.055)+exp(-dot(warmDelta,warmDelta))*float3(.032,.012,.008);
 o.Emission=refl*_Reflectivity*valid+softboxes+_Color.rgb*.045;o.Alpha=1;
}
ENDCG
} FallBack "Standard" }
