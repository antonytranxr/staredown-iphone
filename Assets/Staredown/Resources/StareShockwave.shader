Shader "Staredown/CinematicShockwave" {
Properties { _Color("Energy",Color)=(1,.22,.06,1) _Age("Age",Range(0,1))=0 }
SubShader { Tags {"Queue"="Transparent" "RenderType"="Transparent"} Blend SrcAlpha One ZWrite Off Cull Off
Pass { CGPROGRAM
#pragma vertex vert
#pragma fragment frag
#pragma multi_compile_instancing
#include "UnityCG.cginc"
fixed4 _Color;float _Age;
struct appdata {float4 vertex:POSITION;float2 uv:TEXCOORD0;UNITY_VERTEX_INPUT_INSTANCE_ID};
struct v2f {float4 pos:SV_POSITION;float2 uv:TEXCOORD0;UNITY_VERTEX_OUTPUT_STEREO};
v2f vert(appdata v){v2f o;UNITY_SETUP_INSTANCE_ID(v);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);o.pos=UnityObjectToClipPos(v.vertex);o.uv=v.uv;return o;}
fixed4 frag(v2f i):SV_Target {
float2 p=i.uv*2-1;float radius=length(p);float ring=exp(-abs(radius-.78)*55);float glow=exp(-abs(radius-.78)*15)*.28;
float inner=exp(-abs(radius-.66)*90)*.35;float rays=pow(saturate(.5+.5*sin(atan2(p.x,p.y)*32)),14)*exp(-abs(radius-.81)*24)*.25;
fixed4 c=_Color;c.rgb+=ring*.65;c.a*=saturate(ring+glow+inner+rays);return c;
}
ENDCG } } }
