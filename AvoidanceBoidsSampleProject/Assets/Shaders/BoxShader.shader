Shader "Hidden/BoxShader"
{
    Properties
    {
        _Color("Color", Color) = (1, 1, 1, 1)
        _MainTex("Texture", 2D) = "white"
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue" = "Transparent" }
        LOD 100
    //    Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            // make fog work
            #pragma multi_compile_fog

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD;
                UNITY_FOG_COORDS(1)
                float4 vertex : SV_POSITION;
                float3 normal : TEXCOORD1;
            };

            struct BoxData {
                float3 position;
                float3 normal;
            };

            StructuredBuffer<BoxData> _BoxDataBuffer;
            float4 _Color;
            sampler2D _MainTex;
            half _Scale;

            v2f vert (appdata v, uint instancedId : SV_INSTANCEID)
            {
                v2f o;

               float3 worldPos = _BoxDataBuffer[instancedId].position;
                
                float4x4 scale = float4x4(
                    float4(_Scale, 0, 0, 0),
                    float4(0, _Scale, 0, 0),
                    float4(0, 0, _Scale, 0),
                    float4(0, 0, 0, 1)
                );

                v.vertex = mul(v.vertex, scale);
                o.vertex = UnityObjectToClipPos(v.vertex + worldPos);
                o.uv = v.uv;
                o.normal = _BoxDataBuffer[instancedId].normal;
                UNITY_TRANSFER_FOG(o,o.vertex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float4 col = tex2D(_MainTex, i.uv) * float4(i.normal, 1);
                if(length(i.normal))
                    col.a = 1;
                else {
                    discard;
                    // 全Cubeを出力する場合は以下の処理をする
                    // col = tex2D(_MainTex, i.uv) * float4(1, 1, 1, 1); 
                }
                return col;
            }
            ENDCG
        }
    }
}
