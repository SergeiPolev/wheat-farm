using UnityEngine;
using WheatFarm.Buildings;
using WheatFarm.Core.Data;
using WheatFarm.Farming;using WheatFarm.Inventory;
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
        private readonly IChunkSystem _chunkSystem;        private readonly IInventoryService _inventory;


        private readonly IPlacementGhostService _ghost;
        private readonly IBrushPreviewService _brushPreview;
        private readonly FarmRenderConfig _config;

        private PlantData _selectedPlant;
        private PlaceableData _selectedPlaceable;
        private float _pendingRotation;

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
            _chunkSystem = chunkSystem;            _inventory = inventory;
            _ghost = ghost;
            _brushPreview = brushPreview;
            _config = config;

        }

        public void SelectPlant(PlantData plant)
        {
            _selectedPlaceable = null;
            _selectedPlant = plant;
            _pendingRotation = 0f;
            _ghost.Hide();
        }

        public void SelectPlaceable(PlaceableData placeable)
        {
            _selectedPlant = null;
            _selectedPlaceable = placeable;
            _pendingRotation = 0f;
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
            Vector3 snappedPos = SnapPosition(cursorWorldPos);
            _ghost.UpdatePose(snappedPos, _pendingRotation);

            bool canPlace = _placementService.CanPlace(_selectedPlaceable, cursorWorldPos);
            _ghost.SetValid(canPlace);
            _brushPreview.RenderFootprint(snappedPos, FootprintWorldSize(), canPlace);
        }

        private Vector2 FootprintWorldSize()
        {
            if (_selectedPlaceable.Level == PlacementLevel.Chunk)
                return new Vector2(
                    _selectedPlaceable.GridSize.x * _chunkSystem.ChunkWorldSize,
                    _selectedPlaceable.GridSize.y * _chunkSystem.ChunkWorldSize);
            return Vector2.one * _chunkSystem.CellWorldSize;
        }

        /// <summary>
        /// Adjust rotation based on scroll delta. Called by FarmInteractionController.
        /// </summary>
        public void AdjustRotation(float scrollDelta)
        {
            if (_selectedPlaceable == null) return;

            float step = _selectedPlaceable.Rotation switch
            {
                RotationMode.Step90 => 90f,
                RotationMode.Free5 => 5f,
                _ => 0f
            };

            if (step <= 0f) return;

            if (scrollDelta > 0) _pendingRotation += step;
            else if (scrollDelta < 0) _pendingRotation -= step;

            _pendingRotation = (_pendingRotation % 360f + 360f) % 360f;
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

            var result = _placementService.Place(_selectedPlaceable, worldPos, _pendingRotation);
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

        private Vector3 SnapPosition(Vector3 worldPos)
        {
            if (_selectedPlaceable != null && _selectedPlaceable.Level == PlacementLevel.Chunk)
            {
                var chunkCoord = _chunkSystem.WorldToChunkCoord(worldPos);
                float cw = _chunkSystem.ChunkWorldSize;
                // Center of the GridSize-chunk footprint (matches PlacementService spawn)
                return new Vector3(
                    (chunkCoord.x + _selectedPlaceable.GridSize.x * 0.5f) * cw,
                    0f,
                    (chunkCoord.y + _selectedPlaceable.GridSize.y * 0.5f) * cw);
            }
            else
            {
                var (chunkCoord, cellX, cellY) = _chunkSystem.WorldToCell(worldPos);
                return _chunkSystem.CellToWorld(chunkCoord, cellX, cellY);
            }
        }
    }
}
