using UnityEngine;
using WheatFarm.Buildings;
using WheatFarm.Core.Data;
using WheatFarm.Economy;
using WheatFarm.Farming;

namespace WheatFarm.Player.Tools
{
    /// <summary>
    /// Removes placed objects (buildings, decor), clears paths, and uproots crops.
    /// Click on a building/decor prefab → PlacementService.Remove() with partial refund.
    /// Click on a tree → TreePlacementService.Remove() with SeedCost/2 refund.
    /// Click on ground → clear path (reset GroundState) or uproot crop.
    /// Hover highlights the building/tree under the cursor with a red tint.
    /// </summary>
    public class BulldozeTool : ITool, IBrushAction, IBrushPreviewSource
    {
        private readonly IPlacementService _placementService;
        private readonly IPlantSystem _plantSystem;
        private readonly IChunkSystem _chunkSystem;
        private readonly IBrushService _brush;
        private readonly IWalletService _wallet;
        private readonly ITreePlacementService _treePlacement;

        // Hover highlight state
        private GameObject _hoveredInstance;
        private Renderer[] _hoveredRenderers;
        private static readonly MaterialPropertyBlock _mpb = new();
        private static readonly Color HoverTint = new(1f, 0.5f, 0.5f, 1f);

        public ToolId Id => ToolId.Bulldoze;
        public bool RequiresResource => false;

        public BulldozeTool(
            IPlacementService placementService,
            IPlantSystem plantSystem,
            IChunkSystem chunkSystem,
            IBrushService brush,
            IWalletService wallet,
            ITreePlacementService treePlacement)
        {
            _placementService = placementService;
            _plantSystem = plantSystem;
            _chunkSystem = chunkSystem;
            _brush = brush;
            _wallet = wallet;
            _treePlacement = treePlacement;
        }

        public void OnEquip() { }

        public void OnUnequip()
        {
            ClearHover();
        }

        public void UseAtPosition(Vector3 worldPos)
        {
            // First: try to find a placed object (building/decor) via raycast
            if (TryRemovePlacedObject(worldPos))
                return;

            // Otherwise: brush-based removal of paths/crops on ground
            _brush.ApplyAtWorldPos(worldPos, this);
        }

        public bool CanApply(ChunkData chunk, int cellX, int cellY) =>
            BrushPredicates.Bulldozable(chunk.Cells[chunk.CellIndex(cellX, cellY)]);

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

        public bool PreviewActive => true;
        public Color PreviewCellColor => new(0.9f, 0.25f, 0.2f, 0.45f);

        /// <summary>
        /// Call each frame when the bulldoze tool is active and the cursor is over the ground.
        /// Applies a red tint to the building or tree under the cursor.
        /// </summary>
        public void UpdateHover(Vector3 cursorWorldPos)
        {
            // Resolve which instance is under cursor
            GameObject newInstance = null;
            if (_placementService.TryGetAt(cursorWorldPos, out var placedObj) && placedObj.Instance != null)
                newInstance = placedObj.Instance;
            else if (_treePlacement.TryGetTreeAt(cursorWorldPos, out var tree) && tree.Instance != null)
                newInstance = tree.Instance;

            // Nothing changed — nothing to do
            if (newInstance == _hoveredInstance)
                return;

            // Clear previous tint
            ClearHoveredRenderers();

            _hoveredInstance = newInstance;
            if (_hoveredInstance == null)
            {
                _hoveredRenderers = null;
                return;
            }

            // Apply red tint to all renderers on the new instance
            _hoveredRenderers = _hoveredInstance.GetComponentsInChildren<Renderer>(true);
            _mpb.SetColor("_BaseColor", HoverTint);
            foreach (var r in _hoveredRenderers)
                r.SetPropertyBlock(_mpb);
        }

        /// <summary>
        /// Clears the red tint from the currently hovered instance.
        /// Call when unequipping or when cursor is over UI / misses the ground.
        /// </summary>
        public void ClearHover()
        {
            ClearHoveredRenderers();
            _hoveredInstance = null;
            _hoveredRenderers = null;
        }

        private void ClearHoveredRenderers()
        {
            if (_hoveredRenderers == null) return;
            foreach (var r in _hoveredRenderers)
            {
                // Guard against destroyed instances (Unity null check)
                if (r != null)
                    r.SetPropertyBlock(null);
            }
        }

        private bool TryRemovePlacedObject(Vector3 worldPos)
        {
            // Buildings/decor: cell-accurate footprint hit.
            if (_placementService.TryGetAt(worldPos, out var obj))
            {
                _placementService.Remove(obj);
                Debug.Log($"[Bulldoze] Removed {obj.Data.DisplayName}");
                return true;
            }

            if (_treePlacement.TryGetTreeAt(worldPos, out var tree))
            {
                _treePlacement.Remove(tree);
                int refund = tree.Data.SeedCost / 2;
                if (refund > 0) _wallet.Add(refund);
                Debug.Log($"[Bulldoze] Removed tree {tree.Data.DisplayName} (+{refund})");
                return true;
            }

            return false;
        }
    }
}
