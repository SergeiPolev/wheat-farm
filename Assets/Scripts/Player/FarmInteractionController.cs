using System;
using UnityEngine;
using VContainer;
using WheatFarm.Farming;
using WheatFarm.Player.Tools;

namespace WheatFarm.Player
{
    /// <summary>
    /// Handles mouse interaction with the farm grid.
    /// Click/hold left mouse -> raycast to ground plane -> use current tool.
    /// Number keys 1-7 switch tools. Q/E change brush size. Scroll rotates placement.
    /// Registered as component in FarmScope; VContainer injects dependencies.
    /// </summary>
    public class FarmInteractionController : MonoBehaviour
    {
        [Header("Interaction")]
        [SerializeField] private float maxRayDistance = 200f;

        private IToolService _toolService;
        private IBrushService _brushService;
        private PlacementTool _placementTool;
        private BulldozeTool _bulldozeTool;
        private WheatFarm.Player.Preview.IPlacementGhostService _ghostService;
        private WheatFarm.Player.Preview.IBrushPreviewService _brushPreview;
        private Camera _cam;
        private PlayerAnimationController _animController;
        private readonly Plane _groundPlane = new(Vector3.up, Vector3.zero);

        private static readonly int InteractionPositionId = Shader.PropertyToID("_Interaction_Position");

        /// <summary>Fired when player clicks on a building collider. Subscribers handle UI.</summary>
        public event Action<GameObject> OnBuildingClicked;

        [Inject]
        public void Construct(
            IToolService toolService,
            IBrushService brushService,
            WheatFarm.Player.Preview.IPlacementGhostService ghostService,
            WheatFarm.Player.Preview.IBrushPreviewService brushPreview,
            PlacementTool placementTool = null,
            BulldozeTool bulldozeTool = null)
        {
            _toolService = toolService;
            _brushService = brushService;
            _ghostService = ghostService;
            _brushPreview = brushPreview;
            _placementTool = placementTool;
            _bulldozeTool = bulldozeTool;
        }

        private void Start()
        {
            _cam = Camera.main;
            _animController = GetComponentInChildren<PlayerAnimationController>();
        }

        private void Update()
        {
            if (_toolService == null) return;

            UpdateInteractionPosition();
            HandleToolSwitching();
            HandleBrushSize();
            HandlePreview();
            HandlePlacementRotation();
            HandleToolUse();
        }

        private void UpdateInteractionPosition()
        {
            Shader.SetGlobalVector(InteractionPositionId, transform.position);
        }

        private void HandleToolUse()
        {
            // Left mouse button: use tool
            if (!Input.GetMouseButton(0)) return;

            // Don't interact when over UI
            if (UnityEngine.EventSystems.EventSystem.current != null &&
                UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
                return;

            // Single click: check if we hit a building first (Physics.Raycast)
            if (Input.GetMouseButtonDown(0) && _cam != null)
            {
                Ray ray = _cam.ScreenPointToRay(Input.mousePosition);
                if (Physics.Raycast(ray, out RaycastHit hit, maxRayDistance))
                {
                    OnBuildingClicked?.Invoke(hit.collider.gameObject);
                    return;
                }
            }

            Vector3? hitPoint = GetGroundHitPoint();
            if (hitPoint.HasValue)
            {
                if (_animController != null && !_animController.IsActing)
                {
                    int actionId = GetToolActionId();
                    if (actionId > 0)
                        _animController.PlayAction(actionId);
                }

                _toolService.UseCurrentTool(hitPoint.Value);
            }
        }

        private int GetToolActionId()
        {
            if (_toolService?.CurrentToolId?.CurrentValue == null) return 0;
            return _toolService.CurrentToolId.CurrentValue switch
            {
                ToolId.WateringCan => 1,  // Water animation
                ToolId.Planter => 1,      // Same watering motion for planting
                ToolId.Fertilizer => 1,   // Same watering motion for fertilizing
                ToolId.Dye => 1,          // Same watering motion for dyeing
                ToolId.Sickle => 3,       // Harvest animation
                ToolId.Uproot => 4,       // Uproot animation
                ToolId.Placement => 2,    // Plant/dig animation for buildings
                ToolId.Build => 2,
                ToolId.Bulldoze => 4,     // Uproot animation for demolishing
                _ => 0,
            };
        }

        private Vector3? GetGroundHitPoint()
        {
            if (_cam == null) return null;
            Ray ray = _cam.ScreenPointToRay(Input.mousePosition);

            // Raycast against mathematical Y=0 plane (no collider needed)
            if (_groundPlane.Raycast(ray, out float distance))
            {
                if (distance <= maxRayDistance)
                    return ray.GetPoint(distance);
            }

            return null;
        }

        private void HandleToolSwitching()
        {
            // Tool/item selection is now handled by CatalogTabBar UI.
            // Only Escape remains as a keyboard shortcut for cancel.
            if (Input.GetKeyDown(KeyCode.Escape) && _placementTool != null)
            {
                _placementTool.ClearSelection();
            }
        }

        private void HandleBrushSize()
        {
            if (_brushService == null) return;

            if (Input.GetKeyDown(KeyCode.Q))
                CycleBrushSize(-1);
            if (Input.GetKeyDown(KeyCode.E))
                CycleBrushSize(1);
        }

        private void HandlePreview()
        {
            bool overUI = UnityEngine.EventSystems.EventSystem.current != null &&
                          UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject();
            Vector3? hitPoint = overUI ? null : GetGroundHitPoint();

            // Building ghost (Placement tool only)
            if (_placementTool != null && _toolService.CurrentToolId.CurrentValue == ToolId.Placement)
            {
                _ghostService.SetVisible(hitPoint.HasValue);
                if (hitPoint.HasValue)
                    _placementTool.UpdatePreview(hitPoint.Value);
            }

            // Bulldoze hover highlight
            if (_bulldozeTool != null && _toolService.CurrentToolId.CurrentValue == ToolId.Bulldoze)
            {
                if (hitPoint.HasValue)
                    _bulldozeTool.UpdateHover(hitPoint.Value);
                else
                    _bulldozeTool.ClearHover();
            }

            // Brush cell preview (any tool that is IBrushAction + IBrushPreviewSource)
            if (!hitPoint.HasValue) return;
            var tool = _toolService.CurrentTool.CurrentValue;
            if (tool is IBrushAction action && tool is IBrushPreviewSource src && src.PreviewActive)
                _brushPreview.RenderBrush(hitPoint.Value, action, src.PreviewCellColor);
        }

        private void HandlePlacementRotation()
        {
            if (_placementTool == null) return;
            if (_toolService.CurrentToolId.CurrentValue != ToolId.Placement) return;

            float scroll = Input.mouseScrollDelta.y;
            if (Mathf.Abs(scroll) > 0.01f)
                _placementTool.AdjustRotation(scroll);
        }

        private void CycleBrushSize(int direction)
        {
            int current = (int)_brushService.CurrentSize.Value;
            int next = Mathf.Clamp(current + direction, (int)BrushSize.Small, (int)BrushSize.Large);
            _brushService.CurrentSize.Value = (BrushSize)next;
        }
    }
}
