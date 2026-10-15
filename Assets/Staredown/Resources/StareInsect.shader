Shader "Staredown/CompetitionInsect"
{
    SubShader
    {
        Tags { "Queue"="Geometry" "RenderType"="Opaque" }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"
            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                fixed4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct v2f
            {
                float4 position : SV_POSITION;
                float3 normal : TEXCOORD0;
                float3 view : TEXCOORD1;
                fixed4 color : COLOR;
                UNITY_VERTEX_OUTPUT_STEREO
            };
            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.position = UnityObjectToClipPos(v.vertex);
                o.normal = UnityObjectToWorldNormal(v.normal);
                o.view = UnityWorldSpaceViewDir(mul(unity_ObjectToWorld, v.vertex).xyz);
                o.color = v.color;
                return o;
            }
            fixed4 frag(v2f i) : SV_Target
            {
                float3 normal = normalize(i.normal);
                float light = .72 + .28 * saturate(dot(normal, normalize(float3(-.35, .8, -.4))));
                float rim = pow(1 - saturate(dot(normal, normalize(i.view))), 3) * .12;
                return fixed4(i.color.rgb * light + rim * i.color.rgb, 1);
            }
            ENDCG
        }
    }
}
