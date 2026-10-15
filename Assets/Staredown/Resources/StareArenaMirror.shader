Shader "Staredown/Arena Mirror" {
 Properties{_MirrorLeft("Left",2D)="black"{} _MirrorRight("Right",2D)="black"{} _MirrorReady("Ready",Float)=0}
 SubShader{Tags{"RenderType"="Opaque"} Cull Off ZWrite On
 Pass{CGPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #pragma multi_compile_instancing
 #include "UnityCG.cginc"
 sampler2D _MirrorLeft,_MirrorRight;float4x4 _MirrorVPLeft,_MirrorVPRight;float _MirrorReady;
 struct appdata{float4 vertex:POSITION;UNITY_VERTEX_INPUT_INSTANCE_ID};
 struct v2f{float4 pos:SV_POSITION;float4 left:TEXCOORD0;float4 right:TEXCOORD1;UNITY_VERTEX_OUTPUT_STEREO};
 v2f vert(appdata v){v2f o;UNITY_SETUP_INSTANCE_ID(v);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);o.pos=UnityObjectToClipPos(v.vertex);float4 world=mul(unity_ObjectToWorld,v.vertex);o.left=mul(_MirrorVPLeft,world);o.right=mul(_MirrorVPRight,world);return o;}
 fixed4 frag(v2f i):SV_Target{UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);if(_MirrorReady<.5)return fixed4(.025,.05,.07,1);
  float4 p=i.left;
  #if defined(UNITY_SINGLE_PASS_STEREO) || defined(UNITY_STEREO_INSTANCING_ENABLED) || defined(UNITY_STEREO_MULTIVIEW_ENABLED)
   if(unity_StereoEyeIndex==1)p=i.right;
  #endif
  float2 uv=p.xy/p.w*.5+.5;
  #if UNITY_UV_STARTS_AT_TOP
   uv.y=1-uv.y;
  #endif
  #if defined(UNITY_SINGLE_PASS_STEREO) || defined(UNITY_STEREO_INSTANCING_ENABLED) || defined(UNITY_STEREO_MULTIVIEW_ENABLED)
   return unity_StereoEyeIndex==1?tex2D(_MirrorRight,uv):tex2D(_MirrorLeft,uv);
  #else
   return tex2D(_MirrorLeft,uv);
  #endif
 }
 ENDCG}}
}
