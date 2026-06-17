using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using WheatFarm.Core.Data;
using WheatFarm.Farming;

namespace WheatFarm.Editor
{
    /// <summary>
    /// Editor utility: (re)generates per-cell footprint masks for all non-Path PlaceableData
    /// assets by projecting each prefab's mesh geometry top-down onto the farm cell grid.
    ///
    /// Cell world size is read from the project's FarmRenderConfig (ChunkWorldSize / SubCellResolution).
    /// Each cell is marked occupied ('X') when at least <see cref="CoverageThreshold"/> of its area
    /// is covered by the prefab geometry (3×3 sample points), otherwise free ('.').
    ///
    /// Category=Path assets are skipped (they use brush-based per-cell placement).
    ///
    /// Usage: WheatFarm > Generate Placeable Footprints
    ///    OR: WheatFarm.Editor.PlaceableFootprintMigration.Generate() from execute_code.
    /// </summary>
    public static class PlaceableFootprintMigration
    {
        /// <summary>Fraction of a cell that must be covered by geometry to count as occupied.</summary>
        private const float CoverageThreshold = 0.30f;

        /// <summary>Safety cap so a mis-scaled prefab can't produce an enormous mask.</summary>
        private const int MaxGridSide = 32;

        [MenuItem("WheatFarm/Generate Placeable Footprints")]
        public static void Generate()
        {
            float cell = ResolveCellWorldSize();

            string[] guids = AssetDatabase.FindAssets("t:PlaceableData");
            int updated = 0, skipped = 0;

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var data = AssetDatabase.LoadAssetAtPath<PlaceableData>(path);
                if (data == null) continue;

                if (data.Category == PlaceableCategory.Path)
                {
                    skipped++;
                    continue;
                }

                if (data.Prefab == null)
                {
                    Debug.LogWarning($"[FootprintGen] {data.name}: no prefab — left unchanged.");
                    skipped++;
                    continue;
                }

                if (TryBuildFootprint(data, cell, out var gridSize, out var rows))
                {
                    data.GridSize = gridSize;
                    data.FootprintRows = rows;
                    EditorUtility.SetDirty(data);
                    updated++;
                    Debug.Log($"[FootprintGen] {data.name} -> {gridSize.x}x{gridSize.y}: {string.Join("/", rows)}");
                }
                else
                {
                    Debug.LogWarning($"[FootprintGen] {data.name}: no mesh geometry — left unchanged.");
                    skipped++;
                }
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[FootprintGen] Done. Updated={updated}, Skipped={skipped}, cell={cell}m.");
        }

        private static float ResolveCellWorldSize()
        {
            string[] cfgGuids = AssetDatabase.FindAssets("t:FarmRenderConfig");
            foreach (string g in cfgGuids)
            {
                var cfg = AssetDatabase.LoadAssetAtPath<FarmRenderConfig>(AssetDatabase.GUIDToAssetPath(g));
                if (cfg != null && cfg.SubCellResolution > 0)
                    return cfg.ChunkWorldSize / cfg.SubCellResolution;
            }
            Debug.LogWarning("[FootprintGen] No FarmRenderConfig found — defaulting cell size to 0.25m.");
            return 0.25f;
        }

        /// <summary>
        /// Projects the prefab's mesh triangles onto the XZ plane and rasterizes them into a cell mask.
        /// Returns false if the prefab has no readable mesh geometry.
        /// </summary>
        private static bool TryBuildFootprint(PlaceableData data, float cell, out Vector2Int gridSize, out string[] rows)
        {
            gridSize = Vector2Int.one;
            rows = null;

            GameObject temp = (GameObject)PrefabUtility.InstantiatePrefab(data.Prefab);
            if (temp == null) return false;
            temp.hideFlags = HideFlags.HideAndDontSave;
            temp.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

            try
            {
                // Gather all triangles in XZ world space (instance sits at origin, identity rotation).
                var tris = new List<Vector2>();
                float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;

                foreach (var mf in temp.GetComponentsInChildren<MeshFilter>())
                {
                    var mesh = mf.sharedMesh;
                    if (mesh == null) continue;
                    var verts = mesh.vertices;
                    var tr = mf.transform;
                    var xz = new Vector2[verts.Length];
                    for (int i = 0; i < verts.Length; i++)
                    {
                        var w = tr.TransformPoint(verts[i]);
                        xz[i] = new Vector2(w.x, w.z);
                        if (w.x < minX) minX = w.x;
                        if (w.x > maxX) maxX = w.x;
                        if (w.z < minZ) minZ = w.z;
                        if (w.z > maxZ) maxZ = w.z;
                    }
                    var idx = mesh.triangles;
                    for (int i = 0; i + 2 < idx.Length; i += 3)
                    {
                        tris.Add(xz[idx[i]]);
                        tris.Add(xz[idx[i + 1]]);
                        tris.Add(xz[idx[i + 2]]);
                    }
                }

                if (tris.Count == 0) return false;

                int gridW = Mathf.Clamp(Mathf.CeilToInt((maxX - minX) / cell), 1, MaxGridSide);
                int gridH = Mathf.Clamp(Mathf.CeilToInt((maxZ - minZ) / cell), 1, MaxGridSide);

                rows = new string[gridH];
                int xCount = 0;
                for (int z = 0; z < gridH; z++)
                {
                    var chars = new char[gridW];
                    for (int x = 0; x < gridW; x++)
                    {
                        float cx0 = minX + x * cell;
                        float cz0 = minZ + z * cell;
                        int inside = 0;
                        for (int sx = 0; sx < 3; sx++)
                        for (int sz = 0; sz < 3; sz++)
                        {
                            float px = cx0 + (sx + 1) / 4f * cell;
                            float pz = cz0 + (sz + 1) / 4f * cell;
                            if (CoveredByAnyTriangle(px, pz, tris)) inside++;
                        }
                        bool occ = inside / 9f >= CoverageThreshold;
                        chars[x] = occ ? 'X' : '.';
                        if (occ) xCount++;
                    }
                    rows[z] = new string(chars);
                }

                // Degenerate (all sub-threshold): fall back to a solid rectangle so the placeable still has a footprint.
                if (xCount == 0)
                    for (int z = 0; z < gridH; z++)
                        rows[z] = new string('X', gridW);

                gridSize = new Vector2Int(gridW, gridH);
                return true;
            }
            finally
            {
                Object.DestroyImmediate(temp);
            }
        }

        private static bool CoveredByAnyTriangle(float px, float pz, List<Vector2> tris)
        {
            for (int i = 0; i + 2 < tris.Count; i += 3)
                if (PointInTriangle(px, pz, tris[i], tris[i + 1], tris[i + 2]))
                    return true;
            return false;
        }

        private static bool PointInTriangle(float px, float py, Vector2 a, Vector2 b, Vector2 c)
        {
            float d1 = Sign(a, b), d2 = Sign(b, c), d3 = Sign(c, a);
            bool hasNeg = d1 < 0 || d2 < 0 || d3 < 0;
            bool hasPos = d1 > 0 || d2 > 0 || d3 > 0;
            return !(hasNeg && hasPos);

            float Sign(Vector2 p2, Vector2 p3) => (px - p3.x) * (p2.y - p3.y) - (p2.x - p3.x) * (py - p3.y);
        }
    }
}
