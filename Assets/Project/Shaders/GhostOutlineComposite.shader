Shader "Hidden/WheatFarm/GhostOutlineComposite"
{
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        ZWrite Off ZTest Always Cull Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Name "GhostOutlineComposite"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float4 _PreviewHighlightColor;
            float _OutlineThickness;   // px
            float _DashDensity;        // px per dash period
            float _DashSpeed;
            float _FillStrength;

            float SampleMask(float2 uv) { return SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_PointClamp, uv).r; }

            float4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.texcoord;
                float2 texel = _BlitTexture_TexelSize.xy * _OutlineThickness;

                float m = SampleMask(uv);
                float dil = m;
                dil = max(dil, SampleMask(uv + float2( texel.x, 0)));
                dil = max(dil, SampleMask(uv + float2(-texel.x, 0)));
                dil = max(dil, SampleMask(uv + float2(0,  texel.y)));
                dil = max(dil, SampleMask(uv + float2(0, -texel.y)));
                dil = max(dil, SampleMask(uv + texel * 0.707));
                dil = max(dil, SampleMask(uv - texel * 0.707));
                dil = max(dil, SampleMask(uv + float2(texel.x, -texel.y) * 0.707));
                dil = max(dil, SampleMask(uv + float2(-texel.x, texel.y) * 0.707));

                // Outline ribbon just OUTSIDE the silhouette
                float edge = saturate(dil - m);

                // Marching-ants: animated diagonal stripes in screen space
                float2 px = uv * _ScreenParams.xy;
                float dash = step(0.5, frac((px.x + px.y) / max(_DashDensity, 1.0) + _Time.y * _DashSpeed));

                float alpha = edge * dash + m * _FillStrength;
                return float4(_PreviewHighlightColor.rgb, alpha * _PreviewHighlightColor.a);
            }
            ENDHLSL
        }
    }
}
