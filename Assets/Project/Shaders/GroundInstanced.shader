Shader "WheatFarm/Ground Instanced"
{
    Properties
    {
        _GroundAtlas ("Ground Atlas (2x2, legacy)", 2D) = "white" {}
        _GroundAlbedoArray ("Ground Albedo Array", 2DArray) = "white" {}
        _GroundNormalArray ("Ground Normal Array", 2DArray) = "bump" {}
        _PathTileSize ("Path Tile Size (world units)", Float) = 1.0
        _PathSpecular ("Path Specular", Range(0, 1)) = 0.1
        _PathSmoothness ("Path Smoothness", Range(0, 1)) = 0.5
        _FlipNormalY ("Flip Normal Y (fix inverted relief)", Float) = 0
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
        _TypeBlendWidth ("Path Type Blend Width", Range(0.05, 0.5)) = 0.15
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
            TEXTURE2D_ARRAY(_GroundAlbedoArray);
            SAMPLER(sampler_GroundAlbedoArray);
            TEXTURE2D_ARRAY(_GroundNormalArray);
            SAMPLER(sampler_GroundNormalArray);

            CBUFFER_START(UnityPerMaterial)
                float4 _GroundAtlas_ST;
                float _PathTileSize;
                float _PathSpecular;
                float _PathSmoothness;
                float _FlipNormalY;
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
                float _TypeBlendWidth;
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
                // For path tiles: neighbor GroundState per direction, packed N/E/S/W nibbles
                nointerpolation uint neighborTypes : TEXCOORD9;
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
                uint nTypes = 0;

                #if UNITY_ANY_INSTANCING_ENABLED
                    MeshProperties data = _PerInstanceData[unity_InstanceID];
                    state = data.cropstate.z;
                    startTime = data.cropstate.w;

                    if (state > 0.5)
                    {
                        nFlags = (uint)data.uv.w;
                        nTypes = data.neighborTypes; // packed neighbor states (paths only; 0 otherwise)
                    }
                    else
                    {
                        prox = data.uv.w;
                        // color.xy stores (dx,dy) offset to nearest farmland cell
                        fDir = data.color.xy;
                    }
                #endif

                output.neighborTypes = nTypes;
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

            // Path coverage mask (1 = path, 0 = grass understory) based on which orthogonal
            // neighbors are grass (flag bit clear). Exposed edges soften over _EdgeSoftness;
            // corners where both adjacent edges are exposed are rounded by _CornerRadius (SDF).
            float PathCoverage(float2 uv, uint flags)
            {
                bool eN = ((flags >> 0) & 1u) == 0u; // +Y neighbor is grass
                bool eE = ((flags >> 1) & 1u) == 0u; // +X
                bool eS = ((flags >> 2) & 1u) == 0u; // -Y
                bool eW = ((flags >> 3) & 1u) == 0u; // -X

                // Distance inward from each exposed edge (1.0 = no boundary on that side).
                float dN = eN ? (1.0 - uv.y) : 1.0;
                float dS = eS ? uv.y : 1.0;
                float dE = eE ? (1.0 - uv.x) : 1.0;
                float dW = eW ? uv.x : 1.0;

                float d = min(min(dN, dS), min(dE, dW)); // straight edges (square corners)

                float r = _CornerRadius;
                if (r > 0.001)
                {
                    if (eN && eE) { float2 q = max(float2(r - dE, r - dN), 0.0); d = min(d, r - length(q)); }
                    if (eS && eE) { float2 q = max(float2(r - dE, r - dS), 0.0); d = min(d, r - length(q)); }
                    if (eN && eW) { float2 q = max(float2(r - dW, r - dN), 0.0); d = min(d, r - length(q)); }
                    if (eS && eW) { float2 q = max(float2(r - dW, r - dS), 0.0); d = min(d, r - length(q)); }
                }

                return smoothstep(0.0, _EdgeSoftness, d);
            }

            half3 TintForState(uint s)
            {
                if (s == 5u) return _TintPathWood.rgb;
                if (s == 6u) return _TintPathBrick.rgb;
                return _TintPathStone.rgb; // s == 4 (only path states reach here)
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                int state = (int)input.groundState;

                // Paths (4-6) use a seamless world-space projection so the texture flows across
                // cell boundaries; farmable states (0-3) use the per-tile UV. Slice = GroundState ordinal.
                float2 worldUV = input.positionWS.xz / _PathTileSize;
                float2 uvSel = (state >= 4) ? worldUV : input.tileUV;
                half4 texColor = SAMPLE_TEXTURE2D_ARRAY(_GroundAlbedoArray, sampler_GroundAlbedoArray, uvSel, state);

                // Perturbed normal from the per-state normal slice. The ground is flat, so use a
                // fixed tangent basis T=(1,0,0), B=(0,0,1), N=(0,1,0) → worldN = (n.x, n.z, n.y).
                // _FlipNormalY corrects DirectX/OpenGL handedness if the relief looks inverted.
                float3 nTS = UnpackNormal(SAMPLE_TEXTURE2D_ARRAY(_GroundNormalArray, sampler_GroundNormalArray, uvSel, state));
                nTS.y *= (_FlipNormalY > 0.5) ? -1.0 : 1.0;
                float3 N = normalize(float3(nTS.x, nTS.z, nTS.y));

                // Directional lighting using the perturbed normal
                Light mainLight = GetMainLight();
                half NdotL = saturate(dot(N, mainLight.direction));

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
                            // Soil preview = Tilled slice (1), sampled with the same per-tile UV.
                            half3 soilTex = SAMPLE_TEXTURE2D_ARRAY(_GroundAlbedoArray, sampler_GroundAlbedoArray, input.tileUV, 1).rgb;
                            half3 soilBase = soilTex * _TintTilled.rgb;
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

                // Soft blend across a seam with a DIFFERENT path type: in a narrow band near the
                // shared edge, mix in the neighbor type's slice. Each side mixes 50% at the seam,
                // so both sides match there (continuous). Dominant neighbor = largest band weight.
                if (state >= 4)
                {
                    uint nt = input.neighborTypes;
                    uint nN = (nt >> 0) & 0xFu;
                    uint nE = (nt >> 4) & 0xFu;
                    uint nS = (nt >> 8) & 0xFu;
                    uint nW = (nt >> 12) & 0xFu;
                    uint cur = (uint)state;
                    float bw = _TypeBlendWidth;

                    float wN = (nN >= 4u && nN != cur) ? smoothstep(1.0 - bw, 1.0, input.tileUV.y) : 0.0;
                    float wS = (nS >= 4u && nS != cur) ? smoothstep(1.0 - bw, 1.0, 1.0 - input.tileUV.y) : 0.0;
                    float wE = (nE >= 4u && nE != cur) ? smoothstep(1.0 - bw, 1.0, input.tileUV.x) : 0.0;
                    float wW = (nW >= 4u && nW != cur) ? smoothstep(1.0 - bw, 1.0, 1.0 - input.tileUV.x) : 0.0;

                    float wMax = max(max(wN, wS), max(wE, wW));
                    if (wMax > 0.001)
                    {
                        uint nbr = nN; float best = wN;
                        if (wE > best) { best = wE; nbr = nE; }
                        if (wS > best) { best = wS; nbr = nS; }
                        if (wW > best) { best = wW; nbr = nW; }

                        // Neighbor type sampled with the same world-UV (paths are world-projected).
                        half3 nbrTex = SAMPLE_TEXTURE2D_ARRAY(_GroundAlbedoArray, sampler_GroundAlbedoArray, uvSel, nbr).rgb;
                        stateColor = lerp(stateColor, nbrTex * TintForState(nbr), wMax * 0.5);
                    }
                }

                half3 stateLit = stateColor * NdotL * mainLight.color + stateColor * 0.4;

                // Paths get a soft Blinn-Phong highlight; bare soil/grass stay matte.
                if (state >= 4)
                {
                    float3 V = GetWorldSpaceNormalizeViewDir(input.positionWS);
                    float3 H = normalize(mainLight.direction + V);
                    float specPower = exp2(_PathSmoothness * 8.0) + 1.0;
                    // Weight by micro-facet tilt: the flat base orientation (N ~ up) gets ~no
                    // specular, so an orthographic camera no longer washes the whole path at once —
                    // only the tilted facets of stones/planks glint. nTS.xy length = sin(facet angle).
                    float facet = saturate(length(nTS.xy) * 1.5);
                    float spec = pow(saturate(dot(N, H)), specPower) * _PathSpecular * facet;
                    // Gate by NdotL so the highlight fades where the path faces away from the sun.
                    stateLit += spec * NdotL * mainLight.color;

                    // Round/soften exposed path edges by revealing a grass understory in the cuts,
                    // so an isolated path cell reads as a rounded island and bends round their outer corner.
                    float cov = PathCoverage(input.tileUV, input.neighborFlags);
                    if (cov < 0.999)
                    {
                        half3 grassTex = SAMPLE_TEXTURE2D_ARRAY(_GroundAlbedoArray, sampler_GroundAlbedoArray, input.tileUV, 0).rgb;
                        half3 grassBaseU = grassTex * _TintGrass.rgb;
                        half grassN = saturate(dot(float3(0, 1, 0), mainLight.direction)); // flat understory
                        half3 grassLitU = grassBaseU * grassN * mainLight.color + grassBaseU * 0.4;
                        stateLit = lerp(grassLitU, stateLit, cov);
                    }
                }

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
