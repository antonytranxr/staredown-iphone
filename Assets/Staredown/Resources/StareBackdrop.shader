Shader "Staredown/AtmosphericBackdrop"
{
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"
            struct appdata {float4 vertex:POSITION;float3 normal:NORMAL;fixed4 color:COLOR;UNITY_VERTEX_INPUT_INSTANCE_ID};
            struct v2f {float4 pos:SV_POSITION;fixed4 color:COLOR;float3 normal:TEXCOORD1;float3 world:TEXCOORD2;UNITY_FOG_COORDS(0) UNITY_VERTEX_OUTPUT_STEREO};
            v2f vert(appdata v)
            {v2f o;UNITY_SETUP_INSTANCE_ID(v);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);o.pos=UnityObjectToClipPos(v.vertex);o.normal=UnityObjectToWorldNormal(v.normal);o.world=mul(unity_ObjectToWorld,v.vertex).xyz;o.color=v.color;UNITY_TRANSFER_FOG(o,o.pos);return o;}
            fixed4 frag(v2f i):SV_Target
            {UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);fixed4 color=i.color;float face=.60+.40*saturate(dot(normalize(i.normal),normalize(float3(-.5,.8,.3))));float strata=.96+.04*sin(i.world.y*.32+i.world.x*.015);color.rgb*=face*strata;UNITY_APPLY_FOG(i.fogCoord,color);return color;}
            ENDCG
        }
    }
}
