Shader "WheatFarm/Ground Instanced"
{
    Properties
    {
        _GroundAtlas ("Ground Atlas (2x2)", 2D) = "white" {}
        [HDR] _TintGrass ("Grass Tint", Color) = (0.45, 0.65, 0.25, 1)
        [HDR] _TintTilled ("Tilled Tint", Color) = (0.35, 0.22, 0.1, 1)
        [HDR] _TintWatered ("Watered Tint", Color) = (0.2, 0.14, 0.08, 1)
        [HDR] _TintFertilized ("Fertilized Tint", Color) = (0.45, 0.35, 0.15, 1)
        [HDR] _TintPathStone ("Path Stone Tint", Color) = (0.55, 0.55, 0.5, 1)
        [HDR] _TintPathWood ("Path Wood Tint", Color) = (0.5, 0.35, 0.2, 1)
        [HDR] _TintPathBrick ("Path Brick Tint", Color) = (0.6, 0.3, 0.25, 1)
        _TransitionDuration ("Transition Duration (s)", Float) = 0.6
        _EdgeSoftness ("Edge Softness", Range(0.01, 0.5)) = 0.15
        _CornerRadius ("Corner Radius", Range(0.0, 0.5)) = 0.25
        _ProximityStrength ("Proximity Blend Strength", Range(0.0, 1.0)) = 0.35
    }
    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry+1"
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma instancing_options procedural:vertInstancingGroundSetup

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Assets/Project/Shaders/GetStructedBuffer.hlsl"

            TEXTURE2D(_GroundAtlas);
            SAMPLER(sampler_GroundAtlas);

            CBUFFER_START(UnityPerMaterial)
                float4 _GroundAtlas_ST;
                half4 _TintGrass;
                half4 _TintTilled;
                half4 _TintWatered;
                half4 _TintFertilized;
                half4 _TintPathStone;
                half4 _TintPathWood;
                half4 _TintPathBrick;
                float _TransitionDuration;
                float _EdgeSoftness;
                float _CornerRadius;
                float _ProximityStrength;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 atlasUV : TEXCOORD0;
                float2 tileUV : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                float3 positionWS : TEXCOORD3;
                nointerpolation float groundState : TEXCOORD4;
                nointerpolation float transitionStart : TEXCOORD5;
                // For farmed tiles: neighbor flags packed as bits (N E S W NE SE SW NW)
                nointerpolation uint neighborFlags : TEXCOORD6;
                // For grass tiles: proximity (0..1) and offset (dx,dy) to nearest farmland
                nointerpolation float proximity : TEXCOORD7;
                nointerpolation float2 farmDir : TEXCOORD8;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS);

                output.positionCS = vertexInput.positionCS;
                output.positionWS = vertexInput.positionWS;
                output.normalWS = normalInput.normalWS;

                // Raw tile UV for edge masking (0-1 within tile)
                output.tileUV = input.uv;

                // Read per-instance ground state, transition time, and pre-computed data
                float state = 0;
                float startTime = 0;
                uint nFlags = 0xFF;
                float prox = 0;
                float2 fDir = float2(0, 0);

                #if UNITY_ANY_INSTANCING_ENABLED
                    MeshProperties data = _PerInstanceData[unity_InstanceID];
                    state = data.cropstate.z;
                    startTime = data.cropstate.w;

                    if (state > 0.5)
                    {
                        nFlags = (uint)data.uv.w;
                    }
                    else
                    {
                        prox = data.uv.w;
                        // color.xy stores (dx,dy) offset to nearest farmland cell
                        fDir = data.color.xy;
                    }
                #endif

                output.groundState = state;
                output.transitionStart = startTime;
                output.neighborFlags = nFlags;
                output.proximity = prox;
                output.farmDir = fDir;

                // Compute atlas UV: 2x2 grid (states 0-3 map to atlas tiles, paths 4-6 reuse Tilled tile)
                float atlasState = (state > 3.5) ? 1.0 : state; // paths reuse Tilled atlas tile
                float col = fmod(atlasState, 2.0);
                float row = floor(atlasState / 2.0);
                float2 atlasOffset = float2(col * 0.5, (1.0 - row) * 0.5);
                output.atlasUV = input.uv * 0.5 + atlasOffset;

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                int state = (int)input.groundState;

                // Sample atlas texture
                half4 texColor = SAMPLE_TEXTURE2D(_GroundAtlas, sampler_GroundAtlas, input.atlasUV);

                // Simple directional lighting
                Light mainLight = GetMainLight();
                half NdotL = saturate(dot(input.normalWS, mainLight.direction));

                // Grass base color
                half3 grassBase = texColor.rgb * _TintGrass.rgb;
                half3 grassLit = grassBase * NdotL * mainLight.color + grassBase * 0.4;

                // Grass state (0) — per-pixel proximity blend toward soil
                if (state == 0)
                {
                    float baseProd = input.proximity;
                    if (baseProd > 0.001)
                    {
                        // farmDir = (dx, dy) offset in cell units to nearest farmland
                        // UV (0.5, 0.5) = cell center. Pixel offset within cell = uv - 0.5
                        // Distance from this pixel to nearest farmland cell center (in cell units):
                        float2 pixelOffset = input.tileUV - 0.5;
                        float2 toFarm = input.farmDir - pixelOffset;
                        float pixelDist = length(toFarm);

                        // Smooth falloff: strongest at farmland boundary (dist ~0.5), fading out
                        float maxDist = 2.5; // ProximityRadius + 0.5
                        float prox = saturate(1.0 - (pixelDist - 0.5) / maxDist) * _ProximityStrength;

                        if (prox > 0.001)
                        {
                            half3 soilBase = texColor.rgb * _TintTilled.rgb;
                            half3 soilLit = soilBase * NdotL * mainLight.color + soilBase * 0.4;
                            return half4(lerp(grassLit, soilLit, prox), 1.0);
                        }
                    }
                    return half4(grassLit, 1.0);
                }

                // Farmed/path states — full solid fill, no edge softening
                // (proximity fade on neighboring grass tiles handles the visual transition)
                half4 stateTint = _TintTilled;
                if (state == 2) stateTint = _TintWatered;
                else if (state == 3) stateTint = _TintFertilized;
                else if (state == 4) stateTint = _TintPathStone;
                else if (state == 5) stateTint = _TintPathWood;
                else if (state == 6) stateTint = _TintPathBrick;

                half3 stateColor = texColor.rgb * stateTint.rgb;
                half3 stateLit = stateColor * NdotL * mainLight.color + stateColor * 0.4;

                return half4(stateLit, 1.0);
            }
            ENDHLSL
        }

        // No ShadowCaster pass — flat ground tiles at Y=0.01 don't cast shadows

        // DepthOnly pass
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma vertex DepthVert
            #pragma fragment DepthFrag
            #pragma multi_compile_instancing
            #pragma instancing_options procedural:vertInstancingGroundSetup

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Assets/Project/Shaders/GetStructedBuffer.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings DepthVert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half4 DepthFrag(Varyings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
