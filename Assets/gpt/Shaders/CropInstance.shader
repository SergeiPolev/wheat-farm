Shader "Instanced/CropRender"
{
    Properties
    {
        _FieldMap ("Field Map", 2D) = "white" {}
        _CropID ("Crop ID", Float) = 1
        _BaseHeight ("Height", Float) = 1
        _BaseScale ("Base Scale", Float) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 5.0
            #pragma require structuredBuffer
            #pragma multi_compile_instancing

            #include "UnityCG.cginc"

            StructuredBuffer<float3> _PlantBuffer;

            float _CropID;
            float _BaseHeight;
            float _BaseScale;
            sampler2D _FieldMap;

            UNITY_INSTANCING_BUFFER_START(Props)
            UNITY_INSTANCING_BUFFER_END(Props)

            struct appdata
            {
                float3 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
                uint id : SV_InstanceID;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            float4 GetSample(float2 uv)
            {
                return tex2Dlod(_FieldMap, float4(uv, 0, 0));
            }

            v2f vert(appdata v)
            {
                v2f o;

                float3 worldOffset = _PlantBuffer[v.id];
                float2 uv = frac(worldOffset.xz);

                float4 data = GetSample(uv);
                float cropType = floor(data.r * 255 + 0.5);
                float growth = data.g;

                // Если ID не совпадает — отбрасываем меш
                if (abs(cropType - _CropID) > 0.1)
                {
                    // Сдвигаем далеко вниз, чтобы не отображать
                    o.pos = float4(0, -1, 0, 0);
                    o.uv = v.uv;
                    return o;
                }

                // Масштаб по росту
                float3 scaledVertex = v.vertex;
                scaledVertex.y = _BaseHeight * growth * _BaseScale;

                float3 finalPos = scaledVertex;
                o.pos = UnityObjectToClipPos(finalPos);
                o.uv = v.uv;
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                return float4(0.4 + i.uv.y * 0.6, 1, 0.4, 1); // простая раскраска
            }
            ENDHLSL
        }
    }
}
