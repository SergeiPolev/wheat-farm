using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using WheatFarm.Farming;

namespace WheatFarm.Editor
{
    /// <summary>
    /// Packs per-state ground textures into Texture2DArrays for the GroundInstanced shader.
    /// <see cref="BuildArray"/> is pure (no AssetDatabase) so it can be unit-tested; the menu
    /// wrapper handles asset IO and material wiring.
    /// </summary>
    public static class GroundTextureArrayBuilder
    {
        private const string AlbedoArrayPath = "Assets/Settings/GroundAlbedoArray.asset";
        private const string NormalArrayPath = "Assets/Settings/GroundNormalArray.asset";
        private const string AlbedoProperty = "_GroundAlbedoArray";
        private const string NormalProperty = "_GroundNormalArray";

        /// <summary>
        /// Assemble equally-sized slices into a Texture2DArray (slice i = element i).
        /// Returns null and sets <paramref name="error"/> on any validation failure.
        /// </summary>
        public static Texture2DArray BuildArray(IReadOnlyList<Texture2D> slices, out string error)
        {
            error = null;

            if (slices == null || slices.Count == 0)
            {
                error = "No slices provided.";
                return null;
            }

            var first = slices[0];
            if (first == null)
            {
                error = "Slice 0 is null.";
                return null;
            }

            int w = first.width, h = first.height;
            TextureFormat fmt = first.format;
            int mips = first.mipmapCount;

            for (int i = 0; i < slices.Count; i++)
            {
                var s = slices[i];
                if (s == null) { error = $"Slice {i} is null."; return null; }
                if (s.width != w || s.height != h)
                {
                    error = $"Slice {i} size {s.width}x{s.height} != {w}x{h}.";
                    return null;
                }
                if (s.format != fmt) { error = $"Slice {i} format {s.format} != {fmt}."; return null; }
                if (s.mipmapCount != mips) { error = $"Slice {i} mip count {s.mipmapCount} != {mips}."; return null; }
            }

            var arr = new Texture2DArray(w, h, slices.Count, fmt, mips > 1)
            {
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear,
            };
            for (int i = 0; i < slices.Count; i++)
                Graphics.CopyTexture(slices[i], 0, arr, i); // copies all mips of element i

            arr.Apply(updateMipmaps: false, makeNoLongerReadable: false);
            return arr;
        }

        [MenuItem("WheatFarm/Build Ground Texture Arrays")]
        public static void BuildAndAssign()
        {
            var set = LoadFirst<GroundTextureSet>("t:GroundTextureSet");
            if (set == null)
            {
                Debug.LogError("[GroundArray] No GroundTextureSet asset found. Run " +
                               "'WheatFarm/Build Ground Placeholder Textures' first.");
                return;
            }
            if (set.Entries == null || set.Entries.Length == 0)
            {
                Debug.LogError("[GroundArray] GroundTextureSet has no entries.");
                return;
            }

            int n = set.Entries.Length;
            var albedo = new Texture2D[n];
            var normal = new Texture2D[n];
            for (int i = 0; i < n; i++)
            {
                albedo[i] = set.Entries[i].Albedo;
                normal[i] = set.Entries[i].Normal;
            }

            var albedoArray = BuildArray(albedo, out var aErr);
            if (albedoArray == null) { Debug.LogError($"[GroundArray] Albedo: {aErr}"); return; }

            var normalArray = BuildArray(normal, out var nErr);
            if (normalArray == null) { Debug.LogError($"[GroundArray] Normal: {nErr}"); return; }

            SaveArray(albedoArray, AlbedoArrayPath);
            SaveArray(normalArray, NormalArrayPath);

            set.AlbedoArray = AssetDatabase.LoadAssetAtPath<Texture2DArray>(AlbedoArrayPath);
            set.NormalArray = AssetDatabase.LoadAssetAtPath<Texture2DArray>(NormalArrayPath);
            EditorUtility.SetDirty(set);

            AssignToGroundMaterial(set);
            AssetDatabase.SaveAssets();
            Debug.Log($"[GroundArray] Built {n}-slice albedo+normal arrays and assigned to {set.name}.");
        }

        private static void SaveArray(Texture2DArray array, string path)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Texture2DArray>(path);
            if (existing != null)
            {
                // Reuse the asset so references to it survive (copy pixels into the existing object).
                Graphics.CopyTexture(array, existing);
                EditorUtility.SetDirty(existing);
            }
            else
            {
                AssetDatabase.CreateAsset(array, path);
            }
        }

        private static void AssignToGroundMaterial(GroundTextureSet set)
        {
            var cfg = LoadFirst<FarmRenderConfig>("t:FarmRenderConfig");
            var mat = cfg != null ? cfg.GroundMaterial : null;
            if (mat == null)
            {
                Debug.LogWarning("[GroundArray] No GroundMaterial on FarmRenderConfig — arrays built " +
                                 "but not assigned to a material (shader properties land in Task 6).");
                return;
            }

            bool assigned = false;
            if (mat.HasProperty(AlbedoProperty)) { mat.SetTexture(AlbedoProperty, set.AlbedoArray); assigned = true; }
            if (mat.HasProperty(NormalProperty)) { mat.SetTexture(NormalProperty, set.NormalArray); assigned = true; }
            if (assigned) EditorUtility.SetDirty(mat);
            else Debug.Log($"[GroundArray] {mat.name} has no array properties yet (added in Task 6) — skipped material assign.");
        }

        private static T LoadFirst<T>(string filter) where T : Object
        {
            string[] guids = AssetDatabase.FindAssets(filter);
            if (guids.Length == 0) return null;
            return AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guids[0]));
        }
    }
}
