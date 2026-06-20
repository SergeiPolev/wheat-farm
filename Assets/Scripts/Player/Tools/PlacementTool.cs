using System.Collections.Generic;
using UnityEngine;
using WheatFarm.Buildings;
using WheatFarm.Core.Data;
using WheatFarm.Farming;
using WheatFarm.Inventory;
using WheatFarm.Player.Preview;


namespace WheatFarm.Player.Tools
{
    /// <summary>
    /// Unified placement tool: handles PlantData (crops/bushes/trees) and PlaceableData (buildings/decor/paths).
    /// Replaces PlanterTool + BuildTool.
    /// </summary>
    public class PlacementTool : ITool, IBrushAction, IBrushPreviewSource
    {
        private readonly IPlantSystem _plantSystem;
        private readonly ITreePlacementService _treePlacement;
        private readonly IBrushService _brush;
        private readonly IPlacementService _placementService;
        private readonly IChunkSystem _chunkSystem;
        private readonly IInventoryService _inventory;


        private readonly IPlacementGhostService _ghost;
        private readonly IBrushPreviewService _brushPreview;
        private readonly FarmRenderConfig _config;

        private PlantData _selectedPlant;
        private PlaceableData _selectedPlaceable;
        private float _pendingRotation;
        private int _pendingRotationSteps;
        private readonly List<(Vector3 worldPos, bool ok)> _previewCells = new(64);

        public ToolId Id => ToolId.Placement;
        public bool RequiresResource => true;

        public PlantData SelectedPlant => _selectedPlant;
        public PlaceableData SelectedPlaceable => _selectedPlaceable;
        public float PendingRotation => _pendingRotation;

        public PlacementTool(
            IPlantSystem plantSystem,
            ITreePlacementService treePlacement,
            IBrushService brush,
            IPlacementService placementService,
            IChunkSystem chunkSystem,
            IInventoryService inventory,
            IPlacementGhostService ghost,
            IBrushPreviewService brushPreview,
            FarmRenderConfig config)
        {
            _plantSystem = plantSystem;
            _treePlacement = treePlacement;
            _brush = brush;
            _placementService = placementService;
            _chunkSystem = chunkSystem;
            _inventory = inventory;
            _ghost = ghost;
            _brushPreview = brushPreview;
            _config = config;

        }

        public void SelectPlant(PlantData plant)
        {
            _selectedPlaceable = null;
            _selectedPlant = plant;
            _pendingRotation = 0f;
            _pendingRotationSteps = 0;
            _ghost.Hide();
        }

        public void SelectPlaceable(PlaceableData placeable)
        {
            _selectedPlant = null;
            _selectedPlaceable = placeable;
            _pendingRotation = 0f;
            _pendingRotationSteps = 0;
            _ghost.Hide();
            if (placeable != null && placeable.Category != PlaceableCategory.Path && placeable.Prefab != null)
                _ghost.Show(placeable.Prefab);
            if (placeable != null && placeable.Category == PlaceableCategory.Path)
                _pathPreviewColor = ResolvePathPreviewColor(placeable);
        }

        public void ClearSelection()
        {
            _selectedPlant = null;
            _selectedPlaceable = null;
            _pendingRotation = 0f;
            _pendingRotationSteps = 0;
            _ghost.Hide();
        }

        public void OnEquip() { }

        public void OnUnequip()
        {
            _ghost.Hide();
        }

        public void UseAtPosition(Vector3 worldPos)
        {
            if (_selectedPlant != null)
            {
                UsePlantAtPosition(worldPos);
            }
            else if (_selectedPlaceable != null)
            {
                UsePlaceableAtPosition(worldPos);
            }
        }

        /// <summary>
        /// Called each frame by FarmInteractionController to update ghost preview.
        /// </summary>
        public void UpdatePreview(Vector3 cursorWorldPos)
        {
            if (_selectedPlaceable == null || _selectedPlaceable.Category == PlaceableCategory.Path)
                return;

            // For Free5 the footprint is a conservative rasterization — highlighted cells may
            // slightly exceed the visual ghost bounds at non-axis angles. This is intentional.
            var eval = _placementService.EvaluateFootprint(
                _selectedPlaceable, cursorWorldPos, _pendingRotationSteps, _pendingRotation, _previewCells);

            // Ghost sits on the occupied-cells bounding box center (not raw cursor cell)
            _ghost.UpdatePose(eval.BBoxCenter, _pendingRotation);
            _ghost.SetValid(eval.AllOk);
            _brushPreview.RenderFootprintCells(_previewCells);
        }

        /// <summary>
        /// Adjust rotation based on scroll delta. Called by FarmInteractionController.
        /// </summary>
        public void AdjustRotation(float scrollDelta)
        {
            if (_selectedPlaceable == null) return;

            int dir = scrollDelta > 0 ? 1 : (scrollDelta < 0 ? -1 : 0);
            if (dir == 0) return;

            switch (_selectedPlaceable.Rotation)
            {
                case RotationMode.Step90:
                    // Wrap steps 0..3; keep _pendingRotation in sync for ghost visual
                    _pendingRotationSteps = (_pendingRotationSteps + 4 + dir) % 4;
                    _pendingRotation = _pendingRotationSteps * 90f;
                    break;
                case RotationMode.Free5:
                    // Free5 uses float angle; rotationSteps stays 0 (RasterizeRotated path)
                    _pendingRotation += dir * 5f;
                    _pendingRotation = (_pendingRotation % 360f + 360f) % 360f;
                    break;
                // RotationMode.Fixed: no-op
            }
        }

        // --- Plant placement ---

        private void UsePlantAtPosition(Vector3 worldPos)
        {
            if (_selectedPlant.Category == PlantCategory.Tree)
            {
                _treePlacement.Place(_selectedPlant, worldPos);
                return;
            }

            // Crops and bushes require a purchased seed
            string seedId = "seed_" + _selectedPlant.PlantId;
            if (!_inventory.HasItem(seedId, 1))
            {
                Debug.Log($"[Plant] No {_selectedPlant.DisplayName} seeds — buy some at the shop.");
                return;
            }

            // Brush-based; each planted cell consumes one seed (see Apply)
            _brush.ApplyAtWorldPos(worldPos, this);
        }

        private GroundState SelectedPathState => _selectedPlaceable.PathSubtype switch
        {
            1 => GroundState.PathWood,
            2 => GroundState.PathBrick,
            _ => GroundState.PathStone
        };

        public bool CanApply(ChunkData chunk, int cellX, int cellY)
        {
            ref readonly var cell = ref chunk.Cells[chunk.CellIndex(cellX, cellY)];
            if (_selectedPlaceable != null && _selectedPlaceable.Category == PlaceableCategory.Path)
                return BrushPredicates.PathPaintable(cell, SelectedPathState);
            if (_selectedPlant != null)
                return BrushPredicates.Plantable(cell);
            // Non-path placeables (buildings) aren't brush-applied
            return false;
        }

        /// <summary>IBrushAction implementation — called for each cell in brush radius.</summary>
        public void Apply(ChunkData chunk, int cellX, int cellY)
        {
            // Path painting mode
            if (_selectedPlaceable != null && _selectedPlaceable.Category == PlaceableCategory.Path)
            {
                ApplyPath(chunk, cellX, cellY);
                return;
            }

            // Plant mode
            if (_selectedPlant == null) return;

            // One seed per planted cell
            string seedId = "seed_" + _selectedPlant.PlantId;
            if (!_inventory.HasItem(seedId, 1)) return;

            if (_plantSystem.Plant(chunk.ChunkCoord, cellX, cellY, _selectedPlant))
                _inventory.TryConsume(seedId, 1);
        }

        private void ApplyPath(ChunkData chunk, int cellX, int cellY)
        {
            int idx = chunk.CellIndex(cellX, cellY);
            ref var cell = ref chunk.Cells[idx];

            var pathState = SelectedPathState;

            cell.GroundState = pathState;
            cell.Occupied = true;

            // Sync to GPU
            ref var props = ref chunk.MeshProps[idx];
            props.cropState.z = (float)pathState;
            props.cropState.w = UnityEngine.Time.time;

            _chunkSystem.UpdateGroundNeighborFlags(chunk.ChunkCoord, cellX, cellY);
        }

        // --- Placeable placement ---

        private void UsePlaceableAtPosition(Vector3 worldPos)
        {
            if (_selectedPlaceable.Category == PlaceableCategory.Path)
            {
                // Path painting: brush-based ground state change
                _brush.ApplyAtWorldPos(worldPos, this);
                return;
            }

            var result = _placementService.Place(_selectedPlaceable, worldPos, _pendingRotationSteps, _pendingRotation);
            if (result != null)
                Debug.Log($"[Placement] Placed {_selectedPlaceable.DisplayName}");
            else
                Debug.Log($"[Placement] Cannot place {_selectedPlaceable.DisplayName}");
        }

        // Trees are placed singly via TreePlacementService, not the brush — no cell preview for them
        public bool PreviewActive =>
            (_selectedPlant != null && _selectedPlant.Category != PlantCategory.Tree) ||
            (_selectedPlaceable != null && _selectedPlaceable.Category == PlaceableCategory.Path);

        public Color PreviewCellColor =>
            _selectedPlaceable != null && _selectedPlaceable.Category == PlaceableCategory.Path
                ? _pathPreviewColor
                : new Color(0.2f, 0.9f, 0.2f, 0.4f); // plant mode

        // Resolved once on selection: Material.GetColor is a native call, no need per frame
        private Color _pathPreviewColor = Color.gray;

        private Color ResolvePathPreviewColor(PlaceableData placeable)
        {
            var prop = placeable.PathSubtype switch
            {
                1 => "_TintPathWood",
                2 => "_TintPathBrick",
                _ => "_TintPathStone"
            };
            var mat = _config != null ? _config.GroundMaterial : null;
            var c = (mat != null && mat.HasProperty(prop)) ? mat.GetColor(prop) : Color.gray;
            c.a = 0.55f;
            return c;
        }

    }
}
