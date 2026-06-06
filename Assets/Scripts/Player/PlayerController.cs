using UnityEngine;
using VContainer;
using WheatFarm.Player.Tools;

namespace WheatFarm.Player
{
    /// <summary>
    /// WASD movement controller. Camera-relative isometric movement with independent facing:
    /// - When any tool is equipped: player body faces the mouse cursor (aiming).
    /// - When no tool is equipped: player body faces the movement direction (classic).
    ///
    /// Movement direction and facing are decoupled, so legs can play strafe/back animations
    /// via a 2D blend tree (MoveX/MoveZ in local space relative to facing). The base layer
    /// uses a nested blend tree: Simple1D(Speed) over [Idle, WalkTree2D, RunTree2D].
    ///
    /// Hold Shift to run. Speed parameter: 0 = idle, 0.5 = walk, 1.0 = run.
    /// </summary>
    public class PlayerController : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField] private float walkSpeed = 3f;
        [SerializeField] private float runSpeed = 6f;
        [SerializeField] private float rotationSpeed = 10f;

        [Header("Input")]
        [SerializeField] private KeyCode runKey = KeyCode.LeftShift;

        private Camera _cam;
        private Vector3 _moveDir;
        private bool _isRunning;
        private PlayerAnimationController _animController;
        private IToolService _toolService;

        private readonly Plane _groundPlane = new(Vector3.up, Vector3.zero);

        [Inject]
        public void Construct(IToolService toolService)
        {
            _toolService = toolService;
        }

        private void Start()
        {
            _cam = Camera.main;
            _animController = GetComponentInChildren<PlayerAnimationController>();
        }

        private void Update()
        {
            ReadInput();
            MovePlayer();
            UpdateFacing();
            UpdateAnimator();
        }

        private void ReadInput()
        {
            float h = Input.GetAxisRaw("Horizontal");
            float v = Input.GetAxisRaw("Vertical");
            _isRunning = Input.GetKey(runKey);

            if (_cam == null)
            {
                _moveDir = Vector3.zero;
                return;
            }

            if (Mathf.Approximately(h, 0f) && Mathf.Approximately(v, 0f))
            {
                _moveDir = Vector3.zero;
                return;
            }

            // Camera-relative direction on XZ plane.
            Vector3 camForward = _cam.transform.forward;
            Vector3 camRight = _cam.transform.right;
            camForward.y = 0f;
            camRight.y = 0f;
            camForward.Normalize();
            camRight.Normalize();

            _moveDir = (camForward * v + camRight * h).normalized;
        }

        private void MovePlayer()
        {
            if (_moveDir.sqrMagnitude < 0.0001f) return;
            float currentSpeed = _isRunning ? runSpeed : walkSpeed;
            transform.position += _moveDir * (currentSpeed * Time.deltaTime);
        }

        private void UpdateFacing()
        {
            Vector3 targetForward;

            if (HasActiveTool())
            {
                // Tool equipped → aim at cursor on ground plane.
                if (!TryGetCursorWorldPoint(out var cursorPoint))
                    return;

                Vector3 toCursor = cursorPoint - transform.position;
                toCursor.y = 0f;
                if (toCursor.sqrMagnitude < 0.0001f) return;
                targetForward = toCursor.normalized;
            }
            else
            {
                // No tool → face movement direction (classic).
                if (_moveDir.sqrMagnitude < 0.0001f) return;
                targetForward = _moveDir;
            }

            Quaternion targetRot = Quaternion.LookRotation(targetForward, Vector3.up);
            transform.rotation = Quaternion.Slerp(
                transform.rotation, targetRot, rotationSpeed * Time.deltaTime);
        }

        private void UpdateAnimator()
        {
            if (_animController == null) return;

            // Local-space velocity relative to facing. X = strafe, Z = forward/back.
            Vector3 localMove = transform.InverseTransformDirection(_moveDir);
            float localX = localMove.x;
            float localZ = localMove.z;

            _animController.SetMove(localX, localZ);

            // Speed parameter controls Walk↔Run blend: 0 idle, 0.5 walk, 1 run.
            float speedParam;
            if (_moveDir.sqrMagnitude < 0.0001f)
                speedParam = 0f;
            else
                speedParam = _isRunning ? 1f : 0.5f;

            _animController.SetSpeed(speedParam);
        }

        private bool HasActiveTool()
        {
            return _toolService?.CurrentTool?.CurrentValue != null;
        }

        private bool TryGetCursorWorldPoint(out Vector3 point)
        {
            point = default;
            if (_cam == null) return false;

            Ray ray = _cam.ScreenPointToRay(Input.mousePosition);
            if (_groundPlane.Raycast(ray, out float distance))
            {
                point = ray.GetPoint(distance);
                return true;
            }
            return false;
        }
    }
}
