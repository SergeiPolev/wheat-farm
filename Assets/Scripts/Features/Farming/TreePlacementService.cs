using System.Collections.Generic;
using UnityEngine;
using WheatFarm.Core.Data;

namespace WheatFarm.Farming
{
    public class PlacedTree
    {
        public PlantData Data;
        public Vector3 WorldPosition;
        public Vector2Int CenterChunk;
        public int CenterCellX;
        public int CenterCellY;
        public GameObject Instance;        public float Scale;
        public float RotationY;

    }

    public interface ITreePlacementService
    {
        IReadOnlyList<PlacedTree> PlacedTrees { get; }
        bool CanPlace(PlantData treeData, Vector3 worldPos);
        PlacedTree Place(PlantData treeData, Vector3 worldPos);        PlacedTree RestoreTree(PlantData treeData, Vector3 worldPos, float scale, float rotationY);

        void Remove(PlacedTree tree);
    }

    public class TreePlacementService : ITreePlacementService
    {
        private readonly IChunkSystem _chunkSystem;
        private readonly IPlantSystem _plantSystem;
        private readonly List<PlacedTree> _trees = new();

        public IReadOnlyList<PlacedTree> PlacedTrees => _trees;

        public TreePlacementService(IChunkSystem chunkSystem, IPlantSystem plantSystem)
        {
            _chunkSystem = chunkSystem;
            _plantSystem = plantSystem;
        }

        public bool CanPlace(PlantData treeData, Vector3 worldPos)
        {
            if (treeData == null || treeData.Category != PlantCategory.Tree) return false;

            var trunkCells = GetTrunkCells(treeData, worldPos);
            foreach (var (chunkCoord, cx, cy) in trunkCells)
            {
                var chunk = _chunkSystem.GetChunk(chunkCoord);
                if (chunk == null || !chunk.Unlocked) return false;

                int idx = chunk.CellIndex(cx, cy);
                if (chunk.Cells[idx].Occupied || chunk.Cells[idx].HasPlant) return false;
            }
            return true;
        }

        public PlacedTree Place(PlantData treeData, Vector3 worldPos)
        {
            if (!CanPlace(treeData, worldPos)) return null;

            var (centerChunk, centerX, centerY) = _chunkSystem.WorldToCell(worldPos);

            // Mark trunk cells as occupied
            var trunkCells = GetTrunkCells(treeData, worldPos);
            foreach (var (chunkCoord, cx, cy) in trunkCells)
            {
                var chunk = _chunkSystem.GetChunk(chunkCoord);
                if (chunk == null) continue;

                int idx = chunk.CellIndex(cx, cy);
                chunk.Cells[idx].Occupied = true;
                // Clear any crop rendering for trunk cells
                chunk.MeshProps[idx].cropState = Vector4.zero;
                chunk.Dirty = true;
            }

            // Plant the tree in the center cell for growth tracking
            _plantSystem.Plant(centerChunk, centerX, centerY, treeData);
            // Trees grow without the watering can — water the growth-tracking cell on placement.
            _plantSystem.Water(centerChunk, centerX, centerY);


            var tree = new PlacedTree
            {
                Data = treeData,
                WorldPosition = worldPos,
                CenterChunk = centerChunk,
                CenterCellX = centerX,
                CenterCellY = centerY
            };

            // Create visual GameObject from mesh + material
            if (treeData.Mesh != null)
            {
                float scale = Random.Range(treeData.ScaleRange.x, treeData.ScaleRange.y);
                float rotationY = CropRotation.BaseAngle + Random.Range(-CropRotation.Variance, CropRotation.Variance);
                tree.Scale = scale;
                tree.RotationY = rotationY;
                tree.Instance = BuildTreeVisual(treeData, worldPos, scale, rotationY);
            }

            _trees.Add(tree);
            return tree;
        }

        /// <summary>
        /// Recreate a tree from save data. Unlike Place(), this skips the CanPlace guard
        /// (the trunk cells are already restored as occupied by the chunk loader) and does
        /// not re-plant — growth already lives in the restored center cell.
        /// </summary>
        public PlacedTree RestoreTree(PlantData treeData, Vector3 worldPos, float scale, float rotationY)
        {
            if (treeData == null || treeData.Category != PlantCategory.Tree) return null;

            var (centerChunk, centerX, centerY) = _chunkSystem.WorldToCell(worldPos);

            // Backward-compat: older saves had no scale/rotation — generate them.
            if (scale <= 0f)
            {
                scale = Random.Range(treeData.ScaleRange.x, treeData.ScaleRange.y);
                rotationY = CropRotation.BaseAngle + Random.Range(-CropRotation.Variance, CropRotation.Variance);
            }

            // Re-mark trunk cells occupied and hide their crop rendering (tree shows its own mesh).
            var trunkCells = GetTrunkCells(treeData, worldPos);
            foreach (var (chunkCoord, cx, cy) in trunkCells)
            {
                var chunk = _chunkSystem.GetChunk(chunkCoord);
                if (chunk == null) continue;

                int idx = chunk.CellIndex(cx, cy);
                chunk.Cells[idx].Occupied = true;
                chunk.MeshProps[idx].cropState = Vector4.zero;
                chunk.Dirty = true;
            }

            var tree = new PlacedTree
            {
                Data = treeData,
                WorldPosition = worldPos,
                CenterChunk = centerChunk,
                CenterCellX = centerX,
                CenterCellY = centerY,
                Scale = scale,
                RotationY = rotationY
            };

            if (treeData.Mesh != null)
                tree.Instance = BuildTreeVisual(treeData, worldPos, scale, rotationY);

            _trees.Add(tree);
            return tree;
        }

        private static GameObject BuildTreeVisual(PlantData treeData, Vector3 worldPos, float scale, float rotationY)
        {
            var go = new GameObject($"Tree_{treeData.PlantId}");
            go.transform.position = worldPos + Vector3.up * 0.01f;

            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = treeData.Mesh;

            var mr = go.AddComponent<MeshRenderer>();
            if (treeData.Material != null)
                mr.sharedMaterial = treeData.Material;

            go.transform.localScale = Vector3.one * scale;
            go.transform.rotation = Quaternion.Euler(0, rotationY, 0);
            return go;
        }


        public void Remove(PlacedTree tree)
        {
            // Free trunk cells
            var trunkCells = GetTrunkCells(tree.Data, tree.WorldPosition);
            foreach (var (chunkCoord, cx, cy) in trunkCells)
            {
                var chunk = _chunkSystem.GetChunk(chunkCoord);
                if (chunk == null) continue;

                int idx = chunk.CellIndex(cx, cy);
                chunk.Cells[idx].Occupied = false;
                chunk.Dirty = true;
            }

            // Remove the plant
            _plantSystem.Uproot(tree.CenterChunk, tree.CenterCellX, tree.CenterCellY);

            if (tree.Instance != null)
                Object.Destroy(tree.Instance);

            _trees.Remove(tree);
        }

        private List<(Vector2Int chunkCoord, int cx, int cy)> GetTrunkCells(PlantData data, Vector3 worldPos)
        {
            var result = new List<(Vector2Int, int, int)>();
            int halfX = data.TrunkSize.x / 2;
            int halfY = data.TrunkSize.y / 2;
            float cellSize = _chunkSystem.CellWorldSize;

            for (int dx = -halfX; dx <= halfX; dx++)
            {
                for (int dy = -halfY; dy <= halfY; dy++)
                {
                    var offset = new Vector3(dx * cellSize, 0, dy * cellSize);
                    var (chunkCoord, cx, cy) = _chunkSystem.WorldToCell(worldPos + offset);
                    result.Add((chunkCoord, cx, cy));
                }
            }
            return result;
        }
    }
}
