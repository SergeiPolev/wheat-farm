Shader "Custom/CropShader"
{
    Properties
    {
        _BaseColor ("Color", Color) = (1,1,1,1)
    }

    HLSLINCLUDE
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

    StructuredBuffer<float4> _InstanceData; // x = posX, y = growth, z = posZ, w = unused
    float _CellSize;

    struct Attributes
    {
        float3 positionOS : POSITION;
    };

    struct Varyings
    {
        float4 positionCS : SV_POSITION;
    };

    Varyings vert (Attributes input, uint instanceID : SV_InstanceID)
    {
        Varyings output;

        float4 data = _InstanceData[instanceID];
        float3 pos = float3(data.x * _CellSize, 0, data.z * _CellSize);
        float growth = data.y;

        float3 worldPos = input.positionOS * growth + pos;
        output.positionCS = TransformObjectToHClip(worldPos);

        return output;
    }

    half4 frag (Varyings i) : SV_Target
    {
        return half4(0, 1, 0, 1); // зелёный
    }
    ENDHLSL

    SubShader
    {
        Tags { "RenderType" = "Opaque" }
        Pass
        {
            Name "CropPass"
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            ENDHLSL
        }
    }
}
