Shader "Staredown/Panel"
{
 Properties { _Color("Surface",Color)=(.035,.048,.075,1) _Accent("Accent",Color)=(.35,.85,.88,1) _Size("Size / radius / border",Vector)=(1,1,.08,.006) _Hover("Hover",Float)=0 _Selected("Selected",Float)=0 _Primary("Primary",Float)=0 _Disabled("Disabled",Float)=0 }
 SubShader
 {
  Tags { "Queue"="AlphaTest" "RenderType"="TransparentCutout" }
  Cull Off ZWrite On
  Pass
  {
   CGPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #pragma multi_compile_instancing
   #include "UnityCG.cginc"
   fixed4 _Color,_Accent; float4 _Size; float _Hover,_Selected,_Primary,_Disabled;
   struct appdata {float4 vertex:POSITION;float2 uv:TEXCOORD0;UNITY_VERTEX_INPUT_INSTANCE_ID};
   struct v2f {float4 vertex:SV_POSITION;float2 uv:TEXCOORD0;UNITY_VERTEX_OUTPUT_STEREO};
   v2f vert(appdata v){v2f o;UNITY_SETUP_INSTANCE_ID(v);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);o.vertex=UnityObjectToClipPos(v.vertex);o.uv=v.uv;return o;}
   fixed4 frag(v2f i):SV_Target
   {
    float2 p=(i.uv-.5)*_Size.xy, q=abs(p)-_Size.xy*.5+_Size.z;
    float d=length(max(q,0))+min(max(q.x,q.y),0)-_Size.z;
    clip(-d);
    float rim=1-smoothstep(-_Size.w*1.8,-_Size.w,d);
    float edge=1-rim;
    float glow=exp(-length((i.uv-float2(.22,.95))*float2(1.1,.7))*3.8);
    fixed3 baseColor=_Color.rgb*(.86+.22*i.uv.y)+_Accent.rgb*glow*(.04+_Selected*.09);
    baseColor=lerp(baseColor,_Accent.rgb*(.79+.16*i.uv.y),_Primary*(1-_Disabled));
    baseColor+=_Accent.rgb*_Hover*.075;
    fixed3 border=lerp(_Color.rgb*1.9,_Accent.rgb,saturate(_Selected*.85+_Hover*.55+_Primary*.2)*(1-_Disabled));
    return fixed4(lerp(baseColor,border,edge),1);
   }
   ENDCG
  }
 }
}
