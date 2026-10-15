Shader "Staredown/Spark" {
SubShader { Tags {"Queue"="Transparent" "RenderType"="Transparent"} ZWrite Off Cull Off Blend SrcAlpha One
Pass { CGPROGRAM
#pragma vertex vert
#pragma fragment frag
#pragma multi_compile_instancing
#include "UnityCG.cginc"
struct appdata {float4 vertex:POSITION;float2 uv:TEXCOORD0;fixed4 color:COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID};
struct v2f {float4 pos:SV_POSITION;float2 uv:TEXCOORD0;fixed4 color:COLOR; UNITY_VERTEX_OUTPUT_STEREO};
v2f vert(appdata v){v2f o;UNITY_SETUP_INSTANCE_ID(v);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);o.pos=UnityObjectToClipPos(v.vertex);o.color=v.color;o.uv=v.uv;return o;}
fixed4 frag(v2f i):SV_Target {float2 p=abs(i.uv*2-1);float shape=saturate(1-p.x-p.y);fixed4 c=i.color;c.rgb+=shape*shape*.3;c.a*=shape;return c;}
ENDCG } } }
