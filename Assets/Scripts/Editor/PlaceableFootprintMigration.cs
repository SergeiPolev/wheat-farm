using System;
using UnityEditor;
using UnityEngine;
using WheatFarm.Core.Data;

namespace WheatFarm.Editor
{
    /// <summary>
    /// One-shot editor utility: migrates all PlaceableData assets from chunk-unit GridSize
    /// to cell-unit GridSize by computing renderer bounds from each asset's prefab.
    ///
    /// Cell world size = 0.5 m (ChunkWorldSize=4 / SubCellResolution=8).
    /// Category=Path assets (PlaceableCategory.Path) are skipped — they use per-cell placement.
    ///
    /// Usage: WheatFarm > Migrate Placeable Footprints
    ///    OR: WheatFarm.Editor.PlaceableFootprintMigration.Migrate() from execute_code.
    /// </summary>
    public static class PlaceableFootprintMigration
    {
        private const float CellWorldSize = 0.5f; // 4m chunk / 8 cells

        [MenuItem("WheatFarm/Migrate Placeable Footprints")]
        public static void Migrate()
        {
            string[] guids = AssetDatabase.FindAssets("t:PlaceableData");
            int migrated = 0;
            int skipped = 0;

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var data = AssetDatabase.LoadAssetAtPath<PlaceableData>(path);
                if (data == null) continue;

                // Paths keep their 1×1 cell GridSize — skip them.
                if (data.Category == PlaceableCategory.Path)
                {
                    Debug.Log($"[FootprintMigration] SKIP (Path): {data.name}  GridSize={data.GridSize}");
                    skipped++;
                    continue;
                }

                Vector2Int newGridSize = ComputeCellGridSize(data);

                // Clear footprint rows — solid rectangle fallback from GridSize is correct after migration.
                data.GridSize = newGridSize;
                data.FootprintRows = null;

                EditorUtility.SetDirty(data);
                Debug.Log($"[FootprintMigration] MIGRATED: {data.name}  -> GridSize={newGridSize}  (Category={data.Category})");
                migrated++;
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[FootprintMigration] Done. Migrated={migrated}, Skipped={skipped}.");
        }

        /// <summary>
        /// Computes the cell-unit GridSize for a PlaceableData asset by examining its prefab's
        /// renderer bounds. Falls back to max(1,1) when no prefab or no renderers are found.
        /// </summary>
        private static Vector2Int ComputeCellGridSize(PlaceableData data)
        {
            if (data.Prefab == null)
            {
                // No prefab: use existing GridSize converted from chunks to cells (×8 cells/chunk).
                // This preserves the old intent for assets without prefabs.
                int cx = Mathf.Max(1, data.GridSize.x * 8);
                int cy = Mathf.Max(1, data.GridSize.y * 8);
                Debug.LogWarning($"[FootprintMigration] {data.name}: no prefab — converting chunk GridSize {data.GridSize} → cell GridSize ({cx},{cy})");
                return new Vector2Int(cx, cy);
            }

            // Instantiate a temporary copy in the editor scene to read combined renderer bounds.
            // We use hideFlags so it never shows in the hierarchy and is never saved.
            GameObject temp = null;
            try
            {
                temp = (GameObject)PrefabUtility.InstantiatePrefab(data.Prefab);
                if (temp == null)
                {
                    Debug.LogWarning($"[FootprintMigration] {data.name}: could not instantiate prefab, falling back.");
                    return FallbackCellGridSize(data);
                }

                temp.hideFlags = HideFlags.HideAndDontSave;

                Renderer[] renderers = temp.GetComponentsInChildren<Renderer>(includeInactive: true);
                if (renderers.Length == 0)
                {
                    Debug.LogWarning($"[FootprintMigration] {data.name}: prefab has no Renderers, falling back.");
                    return FallbackCellGridSize(data);
                }

                // Combine all renderer bounds into one.
                Bounds combined = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++)
                    combined.Encapsulate(renderers[i].bounds);

                // XZ footprint (Y is height, irrelevant for grid).
                float sizeX = combined.size.x;
                float sizeZ = combined.size.z;

                int cellsX = Mathf.Max(1, Mathf.CeilToInt(sizeX / CellWorldSize));
                int cellsZ = Mathf.Max(1, Mathf.CeilToInt(sizeZ / CellWorldSize));

                return new Vector2Int(cellsX, cellsZ);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[FootprintMigration] {data.name}: exception during bounds computation: {ex.Message}. Falling back.");
                return FallbackCellGridSize(data);
            }
            finally
            {
                if (temp != null)
                    UnityEngine.Object.DestroyImmediate(temp);
            }
        }

        /// <summary>
        /// Fallback: treat old GridSize as being in chunk units and scale ×8 to get cell units.
        /// Clamps to at least 1×1.
        /// </summary>
        private static Vector2Int FallbackCellGridSize(PlaceableData data)
        {
            return new Vector2Int(
                Mathf.Max(1, data.GridSize.x * 8),
                Mathf.Max(1, data.GridSize.y * 8));
        }
    }
}
