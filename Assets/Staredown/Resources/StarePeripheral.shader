Shader "Staredown/Peripheral" {
Properties { _Color("Color",Color)=(1,.25,.04,.2) }
SubShader { Tags {"Queue"="Overlay" "RenderType"="Transparent"} ZTest Always ZWrite Off Cull Off Blend SrcAlpha OneMinusSrcAlpha
Pass { CGPROGRAM
#pragma vertex vert
#pragma fragment frag
#pragma multi_compile_instancing
#include "UnityCG.cginc"
fixed4 _Color;
struct appdata { float4 vertex:POSITION; float2 uv:TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
struct v2f { float4 pos:SV_POSITION;float2 uv:TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };
v2f vert(appdata v){v2f o;UNITY_SETUP_INSTANCE_ID(v);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);o.pos=UnityObjectToClipPos(v.vertex);o.uv=v.uv;return o;}
fixed4 frag(v2f i):SV_Target { if(distance(_WorldSpaceCameraPos,unity_ObjectToWorld._m03_m13_m23)>1.05)discard;float2 p=(i.uv-.5)*2;float r=length(p*float2(1,1.12));float edge=smoothstep(.48,.93,r);return fixed4(_Color.rgb,_Color.a*edge); }
ENDCG } } }
