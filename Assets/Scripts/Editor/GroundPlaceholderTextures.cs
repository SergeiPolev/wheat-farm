using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using WheatFarm.Farming;

namespace WheatFarm.Editor
{
    /// <summary>
    /// Editor utility: generates 512×512 *tileable* placeholder albedo+normal textures for every
    /// <see cref="GroundState"/> and wires them into the project's <see cref="GroundTextureSet"/>.
    ///
    /// Path states get distinctive procedural patterns (stone = toroidal Voronoi + cracks,
    /// wood = vertical planks + fibre, brick = offset masonry + mortar). The four ground states
    /// (Grass/Tilled/Watered/Fertilized) get near-neutral subtle noise — the shader tints them
    /// via _Tint* so they stay mostly grayscale here. Normals are derived from a height field by
    /// central differences (Sobel-style) with wraparound so they stay seamless.
    ///
    /// Everything is deterministic (fixed seeds) and tileable so the road world-UV projection has
    /// no visible seams between cells. Replace with authored art without touching shader/code.
    ///
    /// Usage: WheatFarm > Build Ground Placeholder Textures
    /// </summary>
    public static class GroundPlaceholderTextures
    {
        private const int Size = 512;
        private const string OutDir = "Assets/Project/Textures/Ground/Generated";

        [MenuItem("WheatFarm/Build Ground Placeholder Textures")]
        public static void Build()
        {
            Directory.CreateDirectory(OutDir);

            var states = new[]
            {
                GroundState.Grass, GroundState.Tilled, GroundState.Watered, GroundState.Fertilized,
                GroundState.PathStone, GroundState.PathWood, GroundState.PathBrick
            };

            var entries = new List<GroundTextureSet.Entry>(states.Length);

            foreach (var state in states)
            {
                Generate(state, out var albedo, out var height);

                var albedoTex = SavePng($"Ground_{state}_A", albedo, isNormal: false);
                var normalPixels = HeightToNormal(height, strength: NormalStrength(state));
                var normalTex = SavePng($"Ground_{state}_N", normalPixels, isNormal: true);

                entries.Add(new GroundTextureSet.Entry { State = state, Albedo = albedoTex, Normal = normalTex });
            }

            AssetDatabase.SaveAssets();
            AssignToSet(entries);
            Debug.Log($"[GroundPlaceholder] Built {states.Length} albedo+normal pairs in {OutDir}.");
        }

        // --- per-state dispatch ---------------------------------------------------------------

        private static float NormalStrength(GroundState s) => s switch
        {
            GroundState.PathStone => 3.0f,
            GroundState.PathBrick => 3.5f,
            GroundState.PathWood => 1.6f,
            _ => 0.4f // ground states are nearly flat
        };

        private static void Generate(GroundState state, out Color32[] albedo, out float[] height)
        {
            switch (state)
            {
                case GroundState.PathStone: Stone(out albedo, out height); break;
                case GroundState.PathWood: Wood(out albedo, out height); break;
                case GroundState.PathBrick: Brick(out albedo, out height); break;
                default: FlatGround(state, out albedo, out height); break;
            }
        }

        // --- generators -----------------------------------------------------------------------

        /// <summary>Cobblestone: toroidal Voronoi cells, darkened mortar lines along cell borders.</summary>
        private static void Stone(out Color32[] albedo, out float[] height)
        {
            albedo = new Color32[Size * Size];
            height = new float[Size * Size];
            var feats = VoronoiFeatures(cellCount: 9, seed: 1117);

            for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                float u = (x + 0.5f) / Size, v = (y + 0.5f) / Size;
                VoronoiSample(feats, u, v, out float f1, out float f2, out int cellSeed);

                float crack = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((f2 - f1) / 0.03f)); // 0 at borders
                float stoneVal = 0.55f + Hash01(cellSeed, 7) * 0.25f;                      // per-stone tone
                stoneVal += (PeriodicFbm(u, v, freq: 24, seed: 51, octaves: 3) - 0.5f) * 0.12f; // grain
                float lit = stoneVal * Mathf.Lerp(0.35f, 1f, crack);

                var c = Tint(lit, warm: 0.04f);
                int i = y * Size + x;
                albedo[i] = c;
                height[i] = lit; // mortar (low crack) sits lower than stone faces
            }
        }

        /// <summary>Wooden boards: vertical planks with per-plank tone, fibre noise, dark grooves.</summary>
        private static void Wood(out Color32[] albedo, out float[] height)
        {
            albedo = new Color32[Size * Size];
            height = new float[Size * Size];
            const int planks = 6;                 // 512 / 6 isn't integer, so work in normalized space
            const float plankW = 1f / planks;

            for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                float u = (x + 0.5f) / Size, v = (y + 0.5f) / Size;
                int plank = Mathf.FloorToInt(u / plankW);
                float inPlank = (u - plank * plankW) / plankW;       // 0..1 across plank

                float groove = Mathf.SmoothStep(0f, 1f, Mathf.Min(inPlank, 1f - inPlank) / 0.06f); // dark edges
                float tone = 0.45f + Hash01(plank, 23) * 0.22f;       // each plank slightly different
                // Fibre runs along the plank (v direction): stretch noise vertically.
                float fibre = PeriodicFbm(u * 3f, v * 0.35f, freq: 32, seed: 90 + plank, octaves: 3);
                float lit = (tone + (fibre - 0.5f) * 0.18f) * Mathf.Lerp(0.4f, 1f, groove);

                int i = y * Size + x;
                albedo[i] = Tint(lit, warm: 0.12f);
                height[i] = Mathf.Lerp(0.3f, 1f, groove); // grooves between planks are lower
            }
        }

        /// <summary>Brick masonry: running-bond rows (half-offset), mortar gaps between bricks.</summary>
        private static void Brick(out Color32[] albedo, out float[] height)
        {
            albedo = new Color32[Size * Size];
            height = new float[Size * Size];
            const int rows = 8;                   // even so the half-offset pattern tiles vertically
            const float rowH = 1f / rows;
            const float brickW = 1f / 4f;         // 4 bricks per row width
            const float mortar = 0.05f;           // mortar thickness (normalized)

            for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                float u = (x + 0.5f) / Size, v = (y + 0.5f) / Size;
                int row = Mathf.FloorToInt(v / rowH);
                float inRow = (v - row * rowH) / rowH;
                float offset = (row & 1) == 0 ? 0f : 0.5f * brickW; // alternate rows shift half a brick
                float ub = Frac(u + offset);
                int brick = Mathf.FloorToInt(ub / brickW);
                float inBrick = (ub - brick * brickW) / brickW;

                // Mortar mask: 1 = brick face, 0 = mortar gap.
                float mh = Mathf.SmoothStep(0f, 1f, Mathf.Min(inRow, 1f - inRow) / (mortar / rowH));
                float mv = Mathf.SmoothStep(0f, 1f, Mathf.Min(inBrick, 1f - inBrick) / (mortar / brickW));
                float brickMask = Mathf.Min(mh, mv);

                int brickSeed = brick * 31 + row * 131;
                float tone = 0.5f + Hash01(brickSeed, 5) * 0.22f;
                tone += (PeriodicFbm(u, v, freq: 40, seed: 71, octaves: 2) - 0.5f) * 0.1f;
                float lit = Mathf.Lerp(0.32f, tone, brickMask);

                int i = y * Size + x;
                // Mortar is gray; brick faces are warm red.
                var brickCol = Tint(lit, warm: 0.30f);
                var mortarCol = Tint(0.4f, warm: 0.0f);
                albedo[i] = Color32.Lerp(mortarCol, brickCol, brickMask);
                height[i] = Mathf.Lerp(0.25f, 1f, brickMask);
            }
        }

        /// <summary>Near-neutral subtle noise for the four farmable ground states (shader tints them).</summary>
        private static void FlatGround(GroundState state, out Color32[] albedo, out float[] height)
        {
            albedo = new Color32[Size * Size];
            height = new float[Size * Size];
            int seed = (int)state * 1000 + 17;
            float baseVal = state == GroundState.Tilled || state == GroundState.Watered ? 0.62f : 0.78f;

            for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                float u = (x + 0.5f) / Size, v = (y + 0.5f) / Size;
                float n = PeriodicFbm(u, v, freq: 16, seed: seed, octaves: 4);
                // Tilled soil gets faint horizontal furrows.
                if (state == GroundState.Tilled)
                    n = Mathf.Lerp(n, 0.5f + 0.5f * Mathf.Sin(v * Mathf.PI * 2f * 12f), 0.25f);
                float lit = Mathf.Clamp01(baseVal + (n - 0.5f) * 0.18f);

                int i = y * Size + x;
                albedo[i] = Tint(lit, warm: 0.02f);
                height[i] = lit;
            }
        }

        // --- height -> normal (tileable central differences) ----------------------------------

        private static Color32[] HeightToNormal(float[] h, float strength)
        {
            var px = new Color32[Size * Size];
            for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                float hl = h[Idx(x - 1, y)], hr = h[Idx(x + 1, y)];
                float hd = h[Idx(x, y - 1)], hu = h[Idx(x, y + 1)];
                var n = new Vector3((hl - hr) * strength, (hd - hu) * strength, 1f).normalized;
                px[y * Size + x] = new Color32(
                    (byte)Mathf.RoundToInt((n.x * 0.5f + 0.5f) * 255f),
                    (byte)Mathf.RoundToInt((n.y * 0.5f + 0.5f) * 255f),
                    (byte)Mathf.RoundToInt((n.z * 0.5f + 0.5f) * 255f),
                    255);
            }
            return px;
        }

        // --- tileable noise helpers -----------------------------------------------------------

        private struct Feature { public float U, V; public int Seed; }

        private static Feature[] VoronoiFeatures(int cellCount, int seed)
        {
            var feats = new Feature[cellCount * cellCount];
            for (int cy = 0; cy < cellCount; cy++)
            for (int cx = 0; cx < cellCount; cx++)
            {
                int s = (cx * 374761393) ^ (cy * 668265263) ^ seed;
                feats[cy * cellCount + cx] = new Feature
                {
                    U = (cx + Hash01(s, 1)) / cellCount,
                    V = (cy + Hash01(s, 2)) / cellCount,
                    Seed = s
                };
            }
            return feats;
        }

        private static void VoronoiSample(Feature[] feats, float u, float v, out float f1, out float f2, out int cellSeed)
        {
            f1 = 9f; f2 = 9f; cellSeed = 0;
            foreach (var f in feats)
            {
                float du = Mathf.Abs(u - f.U); du = Mathf.Min(du, 1f - du); // toroidal wrap
                float dv = Mathf.Abs(v - f.V); dv = Mathf.Min(dv, 1f - dv);
                float d = Mathf.Sqrt(du * du + dv * dv);
                if (d < f1) { f2 = f1; f1 = d; cellSeed = f.Seed; }
                else if (d < f2) { f2 = d; }
            }
        }

        /// <summary>Periodic value noise: lattice hashed modulo <paramref name="freq"/> so it tiles.</summary>
        private static float PeriodicValue(float u, float v, int freq, int seed)
        {
            float x = u * freq, y = v * freq;
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
            float fx = x - x0, fy = y - y0;
            float v00 = LatticeHash(x0, y0, freq, seed);
            float v10 = LatticeHash(x0 + 1, y0, freq, seed);
            float v01 = LatticeHash(x0, y0 + 1, freq, seed);
            float v11 = LatticeHash(x0 + 1, y0 + 1, freq, seed);
            float ux = Smooth(fx), uy = Smooth(fy);
            return Mathf.Lerp(Mathf.Lerp(v00, v10, ux), Mathf.Lerp(v01, v11, ux), uy);
        }

        private static float PeriodicFbm(float u, float v, int freq, int seed, int octaves)
        {
            float sum = 0f, amp = 0.5f, norm = 0f;
            for (int o = 0; o < octaves; o++)
            {
                sum += PeriodicValue(u, v, freq << o, seed + o * 101) * amp;
                norm += amp;
                amp *= 0.5f;
            }
            return sum / norm;
        }

        // --- small math + color ---------------------------------------------------------------

        private static int Idx(int x, int y) => ((y + Size) % Size) * Size + ((x + Size) % Size);
        private static float Frac(float v) => v - Mathf.Floor(v);
        private static float Smooth(float t) => t * t * (3f - 2f * t);
        private static int Mod(int a, int m) => ((a % m) + m) % m;

        private static float LatticeHash(int x, int y, int period, int seed)
        {
            return Hash01((Mod(x, period) * 73856093) ^ (Mod(y, period) * 19349663), seed);
        }

        private static float Hash01(int a, int b)
        {
            unchecked
            {
                int h = a * 374761393 + b * 668265263 + unchecked((int)0x9E3779B1);
                h = (h ^ (h >> 13)) * 1274126177;
                h ^= h >> 16;
                return (h & 0x7fffffff) / (float)int.MaxValue;
            }
        }

        /// <summary>Grayscale value with an optional warm (reddish) bias; clamped to bytes.</summary>
        private static Color32 Tint(float lit, float warm)
        {
            lit = Mathf.Clamp01(lit);
            float r = Mathf.Clamp01(lit + warm);
            float g = Mathf.Clamp01(lit + warm * 0.35f);
            float b = Mathf.Clamp01(lit - warm * 0.4f);
            return new Color32(
                (byte)Mathf.RoundToInt(r * 255f),
                (byte)Mathf.RoundToInt(g * 255f),
                (byte)Mathf.RoundToInt(b * 255f),
                255);
        }

        // --- IO + asset wiring ----------------------------------------------------------------

        private static Texture2D SavePng(string name, Color32[] pixels, bool isNormal)
        {
            string path = $"{OutDir}/{name}.png";
            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            tex.SetPixels32(pixels);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = isNormal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.sRGBTexture = !isNormal;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.SaveAndReimport();

            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static void AssignToSet(List<GroundTextureSet.Entry> entries)
        {
            string[] guids = AssetDatabase.FindAssets("t:GroundTextureSet");
            if (guids.Length == 0)
            {
                Debug.LogWarning("[GroundPlaceholder] No GroundTextureSet asset found — textures generated " +
                                 "but not assigned. Create one (WheatFarm/GroundTextureSet) and re-run.");
                return;
            }

            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            var set = AssetDatabase.LoadAssetAtPath<GroundTextureSet>(path);
            set.Entries = entries.ToArray();
            EditorUtility.SetDirty(set);
            AssetDatabase.SaveAssets();
            Debug.Log($"[GroundPlaceholder] Assigned {entries.Count} entries to {set.name}.");
        }
    }
}
