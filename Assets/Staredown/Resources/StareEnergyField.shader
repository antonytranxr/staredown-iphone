Shader "Staredown/EnergyField" {
Properties { _Color("Field",Color)=(.18,.85,1,1) }
SubShader {Tags {"Queue"="Transparent" "RenderType"="Transparent"} ZWrite Off Cull Off Blend SrcAlpha One
Pass {CGPROGRAM
#pragma vertex vert
#pragma fragment frag
#pragma multi_compile_instancing
#include "UnityCG.cginc"
fixed4 _Color;
struct appdata{float4 vertex:POSITION;float2 uv:TEXCOORD0;UNITY_VERTEX_INPUT_INSTANCE_ID};
struct v2f{float4 pos:SV_POSITION;float2 uv:TEXCOORD0;UNITY_VERTEX_OUTPUT_STEREO};
v2f vert(appdata v){v2f o;UNITY_SETUP_INSTANCE_ID(v);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);o.pos=UnityObjectToClipPos(v.vertex);o.uv=v.uv;return o;}
fixed4 frag(v2f i):SV_Target {float base=exp(-i.uv.y*32);float top=exp(-(1-i.uv.y)*70)*.35;float grid=pow(saturate(.5+.5*cos(i.uv.x*201.06)),20)*.025;float sweep=exp(-abs(i.uv.y-frac(_Time.y*.13))*35)*.045;fixed4 c=_Color;c.a=.65*base+.22*top+grid+sweep;return c;}
ENDCG } } }
