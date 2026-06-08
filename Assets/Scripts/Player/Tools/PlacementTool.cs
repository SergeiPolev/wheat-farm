using UnityEngine;
using WheatFarm.Buildings;
using WheatFarm.Core.Data;
using WheatFarm.Farming;using WheatFarm.Inventory;


namespace WheatFarm.Player.Tools
{
    /// <summary>
    /// Unified placement tool: handles PlantData (crops/bushes/trees) and PlaceableData (buildings/decor/paths).
    /// Replaces PlanterTool + BuildTool.
    /// </summary>
    public class PlacementTool : ITool, IBrushAction
    {
        private readonly IPlantSystem _plantSystem;
        private readonly ITreePlacementService _treePlacement;
        private readonly IBrushService _brush;
        private readonly IPlacementService _placementService;
        private readonly IChunkSystem _chunkSystem;        private readonly IInventoryService _inventory;


        private PlantData _selectedPlant;
        private PlaceableData _selectedPlaceable;
        private float _pendingRotation;

        // Ghost preview
        private GameObject _ghostInstance;
        private Material _ghostMaterial;
        private bool _ghostValid;

        private static readonly Color GhostValidColor = new(0.2f, 0.9f, 0.2f, 0.4f);
        private static readonly Color GhostInvalidColor = new(0.9f, 0.2f, 0.2f, 0.4f);

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
            IInventoryService inventory)
        {
            _plantSystem = plantSystem;
            _treePlacement = treePlacement;
            _brush = brush;
            _placementService = placementService;
            _chunkSystem = chunkSystem;            _inventory = inventory;

        }

        public void SelectPlant(PlantData plant)
        {
            _selectedPlaceable = null;
            _selectedPlant = plant;
            _pendingRotation = 0f;
            DestroyGhost();
        }

        public void SelectPlaceable(PlaceableData placeable)
        {
            _selectedPlant = null;
            _selectedPlaceable = placeable;
            _pendingRotation = 0f;
            DestroyGhost();
            CreateGhost(placeable);
        }

        public void ClearSelection()
        {
            _selectedPlant = null;
            _selectedPlaceable = null;
            _pendingRotation = 0f;
            DestroyGhost();
        }

        public void OnEquip() { }

        public void OnUnequip()
        {
            DestroyGhost();
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
            if (_ghostInstance == null) return;

            Vector3 snappedPos = SnapPosition(cursorWorldPos);
            _ghostInstance.transform.position = snappedPos;
            _ghostInstance.transform.rotation = Quaternion.Euler(0, _pendingRotation, 0);

            // Tint valid/invalid
            bool canPlace = _selectedPlaceable != null && _placementService.CanPlace(_selectedPlaceable, cursorWorldPos);
            if (canPlace != _ghostValid)
            {
                _ghostValid = canPlace;
                SetGhostTint(_ghostValid ? GhostValidColor : GhostInvalidColor);
            }
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
            int idx = chunk.CellIndex(cellX, cellY);
            if (chunk.Cells[idx].HasPlant || chunk.Cells[idx].Occupied) return;

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

            // Don't paint over plants or existing buildings
            if (cell.HasPlant) return;

            // Map PathSubtype to GroundState
            var pathState = _selectedPlaceable.PathSubtype switch
            {
                1 => GroundState.PathWood,
                2 => GroundState.PathBrick,
                _ => GroundState.PathStone
            };

            // Skip if already this path type
            if (cell.GroundState == pathState) return;

            cell.GroundState = pathState;
            cell.Occupied = true;

            // Sync to GPU
            ref var props = ref chunk.MeshProps[idx];
            props.cropState.z = (float)pathState;
            props.cropState.w = UnityEngine.Time.time;

            chunk.Dirty = true;
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

        // --- Ghost preview system ---

        private void CreateGhost(PlaceableData data)
        {
            if (data == null || data.Prefab == null || data.Category == PlaceableCategory.Path)
                return;

            _ghostInstance = Object.Instantiate(data.Prefab);
            _ghostInstance.name = $"Ghost_{data.DisplayName}";

            // Remove colliders and scripts from ghost
            foreach (var col in _ghostInstance.GetComponentsInChildren<Collider>())
                Object.Destroy(col);
            foreach (var mb in _ghostInstance.GetComponentsInChildren<MonoBehaviour>())
                Object.Destroy(mb);

            // Create transparent material
            _ghostMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            _ghostMaterial.SetFloat("_Surface", 1); // Transparent
            _ghostMaterial.SetFloat("_Blend", 0); // Alpha
            _ghostMaterial.SetFloat("_AlphaClip", 0);
            _ghostMaterial.SetOverrideTag("RenderType", "Transparent");
            _ghostMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            _ghostMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            _ghostMaterial.SetInt("_ZWrite", 0);
            _ghostMaterial.renderQueue = 3000;
            _ghostMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");

            // Apply ghost material to all renderers
            foreach (var renderer in _ghostInstance.GetComponentsInChildren<Renderer>())
            {
                var mats = new Material[renderer.sharedMaterials.Length];
                for (int i = 0; i < mats.Length; i++)
                    mats[i] = _ghostMaterial;
                renderer.materials = mats;
            }

            _ghostValid = false;
            SetGhostTint(GhostInvalidColor);
        }

        private void SetGhostTint(Color color)
        {
            if (_ghostMaterial != null)
                _ghostMaterial.color = color;
        }

        private void DestroyGhost()
        {
            if (_ghostInstance != null)
            {
                Object.Destroy(_ghostInstance);
                _ghostInstance = null;
            }
            if (_ghostMaterial != null)
            {
                Object.Destroy(_ghostMaterial);
                _ghostMaterial = null;
            }
        }

        private Vector3 SnapPosition(Vector3 worldPos)
        {
            if (_selectedPlaceable != null && _selectedPlaceable.Level == PlacementLevel.Chunk)
            {
                var chunkCoord = _chunkSystem.WorldToChunkCoord(worldPos);
                return _chunkSystem.CellToWorld(chunkCoord, 0, 0);
            }
            else
            {
                var (chunkCoord, cellX, cellY) = _chunkSystem.WorldToCell(worldPos);
                return _chunkSystem.CellToWorld(chunkCoord, cellX, cellY);
            }
        }
    }
}
