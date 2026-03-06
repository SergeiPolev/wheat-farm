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
        public GameObject Instance;
    }

    public interface ITreePlacementService
    {
        IReadOnlyList<PlacedTree> PlacedTrees { get; }
        bool CanPlace(PlantData treeData, Vector3 worldPos);
        PlacedTree Place(PlantData treeData, Vector3 worldPos);
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

            var tree = new PlacedTree
            {
                Data = treeData,
                WorldPosition = worldPos,
                CenterChunk = centerChunk,
                CenterCellX = centerX,
                CenterCellY = centerY
            };

            // Instantiate prefab if available
            if (treeData.Mesh != null)
            {
                // Trees use prefab instantiation, not GPU instancing
                // TODO: create tree prefab from mesh + material
            }

            _trees.Add(tree);
            return tree;
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
