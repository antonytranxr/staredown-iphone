Shader "Staredown/EyeWall" {
Properties{_MainTex("Eye mural",2D)="white"{} _ClosedTex("Closed eyelids",2D)="white"{} _Closures("Lids",Vector)=(0,0,0,0) _Species("Species",Vector)=(0,1,0,0) _LiveLeftTex("Left finalist",2D)="black"{} _LiveRightTex("Right finalist",2D)="black"{} _LiveFeedReady("Live eyes ready",Float)=0 _ArenaTex("Arena live",2D)="black"{} _ArenaMode("Arena mode",Float)=0 _ArenaReady("Arena ready",Float)=0}
SubShader {Tags{"RenderType"="Opaque"} Cull Off
Pass{CGPROGRAM
#pragma vertex vert
#pragma fragment frag
#pragma multi_compile_instancing
#include "UnityCG.cginc"
sampler2D _MainTex,_ClosedTex;float4 _Closures,_Species;
sampler2D _LiveLeftTex,_LiveRightTex,_ArenaTex;float _LiveFeedReady,_ArenaMode,_ArenaReady;
struct appdata{float4 vertex:POSITION;float2 uv:TEXCOORD0;UNITY_VERTEX_INPUT_INSTANCE_ID};
struct v2f{float4 pos:SV_POSITION;float2 uv:TEXCOORD0;UNITY_VERTEX_OUTPUT_STEREO};
v2f vert(appdata v){v2f o;UNITY_SETUP_INSTANCE_ID(v);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);o.pos=UnityObjectToClipPos(v.vertex);o.uv=v.uv;return o;}
fixed3 lid(float2 uv,float2 center,float close,fixed3 color){float2 d=(uv-center)/float2(.082,.19);float region=1-smoothstep(.8,1.1,length(d));float closing=smoothstep(.12,.87,close);fixed3 skin=tex2D(_ClosedTex,uv).rgb;return lerp(color,skin,region*closing);}

fixed4 frag(v2f i):SV_Target{
 if(_ArenaMode>1.5){
  float2 panel=float2(i.uv.x,saturate((i.uv.y-.39)/.49));
  float glow=pow(saturate(1-length((panel-float2(.5,.8))*float2(1.4,1))),3);
  float2 grid=abs(frac(panel*float2(64,18))-.5);
  float lines=step(.482,max(grid.x,grid.y));
  fixed3 base=lerp(fixed3(.006,.015,.03),fixed3(.014,.065,.09),glow);
  base+=fixed3(.015,.06,.075)*lines*.24;
  float rim=1-smoothstep(.002,.009,min(panel.y,1-panel.y));
  return fixed4(base+rim*fixed3(.08,.55,.62),1);
 }
 if(_ArenaMode>.5&&_ArenaReady>.5)return tex2D(_ArenaTex,float2(i.uv.x,saturate((i.uv.y-.39)/.49)));
 if(_LiveFeedReady>.5){float2 liveUV=float2(frac(i.uv.x*2),saturate((i.uv.y-.39)/.49));return i.uv.x<.5?tex2D(_LiveLeftTex,liveUV):tex2D(_LiveRightTex,liveUV);}
 float side=step(.5,i.uv.x);float species=side<.5?_Species.x:_Species.y;float2 uv=float2(frac(i.uv.x*2)*.5+species*.5,i.uv.y);float2 lids=side<.5?_Closures.xy:_Closures.zw;
 fixed3 col=tex2D(_MainTex,uv).rgb;col=lid(uv,species<.5?float2(.120,.626):float2(.613,.622),lids.x,col);col=lid(uv,species<.5?float2(.380,.626):float2(.887,.622),lids.y,col);
 float edge=smoothstep(0,.015,i.uv.x)*smoothstep(0,.015,1-i.uv.x);return fixed4(col*edge*.92,1);
}
ENDCG}}
}
