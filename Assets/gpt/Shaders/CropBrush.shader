Shader "Hidden/CropBrush"
{
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            float2 _BrushCenter;
            float _BrushSize;
            float4 _BrushColor;

            sampler2D _MainTex; // текущая текстура
            float4 _MainTex_TexelSize; // обязательно, если используешь _MainTex

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            v2f vert(appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.texcoord.xy;
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                float dist = distance(i.uv, _BrushCenter);
                float mask = 1 - smoothstep(0, _BrushSize, dist);

                float4 existing = tex2D(_MainTex, i.uv);

                // Простой альфа-бленд (можно адаптировать под add/multiply и т.п.)
                float4 result = lerp(existing, _BrushColor, mask);

                return result;
            }
            ENDCG
        }
    }
}
