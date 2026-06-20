Shader "WheatFarm/BrushRing"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,0.9)
        _Thickness ("Thickness (0-0.5 of radius)", Range(0.01,0.5)) = 0.06
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent+6" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Name "Ring"
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _Thickness;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings  { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float r = length(IN.uv - 0.5) * 2.0;          // 0 center, 1 at quad edge
                float ring = smoothstep(_Thickness, _Thickness * 0.5, abs(r - (1.0 - _Thickness)));
                return half4(_Color.rgb, _Color.a * ring);
            }
            ENDHLSL
        }
    }
}
