using UnityEngine;
using WheatFarm.Buildings;
using WheatFarm.Core.Data;
using WheatFarm.Farming;

namespace WheatFarm.Player.Tools
{
    /// <summary>
    /// Removes placed objects (buildings, decor), clears paths, and uproots crops.
    /// Click on a building/decor prefab → PlacementService.Remove() with partial refund.
    /// Click on ground → clear path (reset GroundState) or uproot crop.
    /// </summary>
    public class BulldozeTool : ITool, IBrushAction
    {
        private const float BuildingProximityThreshold = 3f;
        private const float DecorProximityThreshold = 0.5f;

        private readonly IPlacementService _placementService;
        private readonly IPlantSystem _plantSystem;
        private readonly IChunkSystem _chunkSystem;
        private readonly IBrushService _brush;

        public ToolId Id => ToolId.Bulldoze;
        public bool RequiresResource => false;

        public BulldozeTool(
            IPlacementService placementService,
            IPlantSystem plantSystem,
            IChunkSystem chunkSystem,
            IBrushService brush)
        {
            _placementService = placementService;
            _plantSystem = plantSystem;
            _chunkSystem = chunkSystem;
            _brush = brush;
        }

        public void OnEquip() { }
        public void OnUnequip() { }

        public void UseAtPosition(Vector3 worldPos)
        {
            // First: try to find a placed object (building/decor) via raycast
            if (TryRemovePlacedObject(worldPos))
                return;

            // Otherwise: brush-based removal of paths/crops on ground
            _brush.ApplyAtWorldPos(worldPos, this);
        }

        /// <summary>IBrushAction — clear path or uproot crop at each cell.</summary>
        public void Apply(ChunkData chunk, int cellX, int cellY)
        {
            int idx = chunk.CellIndex(cellX, cellY);
            ref var cell = ref chunk.Cells[idx];

            // Clear path
            if (cell.GroundState >= GroundState.PathStone)
            {
                cell.GroundState = GroundState.Grass;
                cell.Occupied = false;

                ref var props = ref chunk.MeshProps[idx];
                props.cropState.z = (float)GroundState.Grass;
                props.cropState.w = Time.time;

                chunk.Dirty = true;
                _chunkSystem.UpdateGroundNeighborFlags(chunk.ChunkCoord, cellX, cellY);
                return;
            }

            // Uproot crop
            if (cell.HasPlant)
            {
                _plantSystem.Uproot(chunk.ChunkCoord, cellX, cellY);
            }
        }

        private bool TryRemovePlacedObject(Vector3 worldPos)
        {
            // Check PlacementService's placed objects for one near the click position
            foreach (var obj in _placementService.PlacedObjects)
            {
                if (obj.Instance == null) continue;

                float dist = Vector3.Distance(obj.Instance.transform.position, worldPos);
                // Use chunk size as proximity threshold for buildings, cell size for decor
                float threshold = obj.Data.Level == PlacementLevel.Chunk ? BuildingProximityThreshold : DecorProximityThreshold;

                if (dist <= threshold)
                {
                    _placementService.Remove(obj);
                    Debug.Log($"[Bulldoze] Removed {obj.Data.DisplayName}");
                    return true;
                }
            }
            return false;
        }
    }
}
