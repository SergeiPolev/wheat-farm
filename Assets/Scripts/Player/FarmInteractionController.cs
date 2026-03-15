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
    /// Number keys 1-6 switch tools. Scroll changes brush size.
    /// Registered as component in FarmScope; VContainer injects dependencies.
    /// </summary>
    public class FarmInteractionController : MonoBehaviour
    {
        [Header("Interaction")]
        [SerializeField] private float maxRayDistance = 200f;

        private IToolService _toolService;
        private IBrushService _brushService;
        private Tools.BuildTool _buildTool;
        private Camera _cam;
        private readonly Plane _groundPlane = new(Vector3.up, Vector3.zero);

        private static readonly int InteractionPositionId = Shader.PropertyToID("_Interaction_Position");

        /// <summary>Fired when player clicks on a building collider. Subscribers handle UI.</summary>
        public event Action<GameObject> OnBuildingClicked;

        [Inject]
        public void Construct(IToolService toolService, IBrushService brushService, Tools.BuildTool buildTool = null)
        {
            _toolService = toolService;
            _brushService = brushService;
            _buildTool = buildTool;
        }

        private void Start()
        {
            _cam = Camera.main;
        }

        private void Update()
        {
            if (_toolService == null) return;

            UpdateInteractionPosition();
            HandleToolSwitching();
            HandleBrushSize();
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
                _toolService.UseCurrentTool(hitPoint.Value);
        }

        private Vector3? GetGroundHitPoint()
        {
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
            if (Input.GetKeyDown(KeyCode.Alpha1)) _toolService.EquipTool(ToolId.Planter);
            if (Input.GetKeyDown(KeyCode.Alpha2)) _toolService.EquipTool(ToolId.WateringCan);
            if (Input.GetKeyDown(KeyCode.Alpha3)) _toolService.EquipTool(ToolId.Sickle);
            if (Input.GetKeyDown(KeyCode.Alpha4)) _toolService.EquipTool(ToolId.Dye);
            if (Input.GetKeyDown(KeyCode.Alpha5)) _toolService.EquipTool(ToolId.Fertilizer);
            if (Input.GetKeyDown(KeyCode.Alpha6)) _toolService.EquipTool(ToolId.Uproot);
            if (Input.GetKeyDown(KeyCode.Alpha7)) _toolService.EquipTool(ToolId.Build);

            // B to cycle buildings when BuildTool is active
            if (Input.GetKeyDown(KeyCode.B) && _toolService.CurrentToolId.CurrentValue == ToolId.Build)
            {
                if (_buildTool != null) _buildTool.CycleBuilding();
            }
        }

        private void HandleBrushSize()
        {
            if (_brushService == null) return;

            // Q/E to cycle brush size, or scroll with Shift held
            if (Input.GetKeyDown(KeyCode.Q))
                CycleBrushSize(-1);
            if (Input.GetKeyDown(KeyCode.E))
                CycleBrushSize(1);
        }

        private void CycleBrushSize(int direction)
        {
            int current = (int)_brushService.CurrentSize.Value;
            int next = Mathf.Clamp(current + direction, (int)BrushSize.Small, (int)BrushSize.Large);
            _brushService.CurrentSize.Value = (BrushSize)next;
        }
    }
}
