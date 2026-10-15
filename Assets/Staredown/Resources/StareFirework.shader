Shader "Staredown/Firework" {
SubShader { Tags {"Queue"="Transparent" "RenderType"="Transparent"} ZWrite Off Cull Off Blend SrcAlpha One
Pass { CGPROGRAM
#pragma vertex vert
#pragma fragment frag
#pragma multi_compile_instancing
#include "UnityCG.cginc"
struct appdata {float4 vertex:POSITION;float2 uv:TEXCOORD0;fixed4 color:COLOR;UNITY_VERTEX_INPUT_INSTANCE_ID};
struct v2f {float4 pos:SV_POSITION;float2 uv:TEXCOORD0;fixed4 color:COLOR;UNITY_VERTEX_OUTPUT_STEREO};
v2f vert(appdata v){v2f o;UNITY_SETUP_INSTANCE_ID(v);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);o.pos=UnityObjectToClipPos(v.vertex);o.color=v.color;o.uv=v.uv;return o;}
fixed4 frag(v2f i):SV_Target {float2 p=i.uv*2-1;float d=length(p);float halo=exp(-dot(p,p)*3.5);float core=pow(saturate(1-d),5);fixed4 c=i.color;c.rgb*=.85+core*1.35;c.a*=halo*saturate((1-d)*5);return c;}
ENDCG } } }
