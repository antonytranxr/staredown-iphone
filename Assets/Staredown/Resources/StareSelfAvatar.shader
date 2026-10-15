Shader "Staredown/Self Avatar" {
 Properties { _Color("Color",Color)=(1,1,1,1) _MainTex("Albedo",2D)="white"{} _BumpMap("Normal",2D)="bump"{} _Metallic("Metallic",Range(0,1))=0 _Glossiness("Smoothness",Range(0,1))=.5 _EmissionColor("Emission",Color)=(0,0,0,0) _LocalHead("Head",Vector)=(0,0,0,0) _LocalHeadRadius("Head clip",Float)=0 _HeadPart("Rigid head detail",Float)=0 }
 SubShader {Tags{"RenderType"="Opaque"} LOD 200
 CGPROGRAM
 #pragma surface surf Standard fullforwardshadows vertex:vert
 #pragma target 3.0
 #pragma multi_compile_instancing
 sampler2D _MainTex,_BumpMap;fixed4 _Color,_EmissionColor;half _Metallic,_Glossiness;float3 _LocalHead;float _LocalHeadRadius,_StareExternalView,_HeadPart;
 struct Input {float2 uv_MainTex;float2 uv_BumpMap;float3 worldPos;float headMask;};
 void vert(inout appdata_full v,out Input o){UNITY_INITIALIZE_OUTPUT(Input,o);o.headMask=v.texcoord3.x;}
 void surf(Input i,inout SurfaceOutputStandard o){
  if(_StareExternalView<.5&&_LocalHeadRadius>0&&distance(_WorldSpaceCameraPos,_LocalHead)<.4){clip(.35-max(i.headMask,_HeadPart));clip(distance(i.worldPos,_LocalHead)-_LocalHeadRadius);}
  fixed4 c=tex2D(_MainTex,i.uv_MainTex)*_Color;o.Albedo=c.rgb;o.Alpha=c.a;o.Normal=UnpackNormal(tex2D(_BumpMap,i.uv_BumpMap));o.Metallic=_Metallic;o.Smoothness=_Glossiness;o.Emission=_EmissionColor.rgb;
 }
 ENDCG
 }
 Fallback "Diffuse"
}
