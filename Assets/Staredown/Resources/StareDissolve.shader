Shader "Staredown/ContestantDissolve" {
Properties { _MainTex("Albedo",2D)="white"{} _Color("Tint",Color)=(1,1,1,1) _EdgeColor("Edge",Color)=(1,.22,.06,1) _Progress("Dissolve",Range(0,1))=0 _FloorY("Floor",Float)=0 }
SubShader { Tags {"Queue"="AlphaTest" "RenderType"="TransparentCutout"} Cull Back ZWrite On
Pass { CGPROGRAM
#pragma vertex vert
#pragma fragment frag
#pragma multi_compile_instancing
#include "UnityCG.cginc"
sampler2D _MainTex;float4 _MainTex_ST;fixed4 _Color,_EdgeColor;float _Progress,_FloorY;
struct appdata {float4 vertex:POSITION;float3 normal:NORMAL;float2 uv:TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID};
struct v2f {float4 pos:SV_POSITION;float2 uv:TEXCOORD0;float3 world:TEXCOORD1;float3 normal:TEXCOORD2; UNITY_VERTEX_OUTPUT_STEREO};
v2f vert(appdata v){v2f o;UNITY_SETUP_INSTANCE_ID(v);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);o.pos=UnityObjectToClipPos(v.vertex);o.uv=TRANSFORM_TEX(v.uv,_MainTex);o.world=mul(unity_ObjectToWorld,v.vertex).xyz;o.normal=UnityObjectToWorldNormal(v.normal);return o;}
fixed4 frag(v2f i):SV_Target {
float3 cell=floor(i.world*24);float noise=frac(sin(dot(cell,float3(12.9898,78.233,37.719)))*43758.5453);
float height=saturate((i.world.y-_FloorY)/2.15);float boundary=height*.82+noise*.18;
float remaining=boundary-_Progress*1.12;clip(remaining+.015);
float edge=1-smoothstep(0,.065,remaining);float scan=pow(saturate(.5+.5*sin(i.world.y*140)),8);
fixed3 base=tex2D(_MainTex,i.uv).rgb*_Color.rgb;
float light=.42+.58*saturate(dot(normalize(i.normal),normalize(float3(.3,.8,-.5))));
return fixed4(base*light+_EdgeColor.rgb*(edge*2.5+scan*.12*_Progress),1);
}
ENDCG } } }
