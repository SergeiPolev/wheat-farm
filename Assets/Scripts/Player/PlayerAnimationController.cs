using UnityEngine;

namespace WheatFarm.Player
{
    /// <summary>
    /// Controls the Animator on the farmer model. Driven by PlayerController (locomotion) and FarmInteractionController (actions).
    ///
    /// Animator layers (configured via FarmerAnimatorSetup editor tool):
    ///   - Base Layer (LowerBody mask): Locomotion = 1D(Speed) over [Idle, WalkTree2D, RunTree2D]
    ///   - Upper Idle Layer (UpperBody mask, weight 1): neutral idle pose for arms
    ///   - Actions Layer (UpperBody mask, weight 1): tool actions, overrides Upper Idle when ToolAction is set
    ///
    /// Parameters:
    ///   - MoveX, MoveZ (float): local-space velocity relative to facing, for strafe blending
    ///   - Speed (float): 0 idle, 0.5 walk, 1 run — drives the outer 1D blend
    ///   - ToolAction (int): action id for the Actions Layer (0 = none)
    /// </summary>
    public class PlayerAnimationController : MonoBehaviour
    {
        [Tooltip("How quickly MoveX/MoveZ/Speed parameters follow their targets. Higher = snappier.")]
        [SerializeField] private float blendDamping = 12f;

        private Animator _animator;

        private static readonly int SpeedHash = Animator.StringToHash("Speed");
        private static readonly int MoveXHash = Animator.StringToHash("MoveX");
        private static readonly int MoveZHash = Animator.StringToHash("MoveZ");
        private static readonly int ToolActionHash = Animator.StringToHash("ToolAction");

        private float _moveX;
        private float _moveZ;
        private float _speed;

        private float _targetMoveX;
        private float _targetMoveZ;
        private float _targetSpeed;

        /// <summary>True while an action animation is playing.</summary>
        public bool IsActing { get; private set; }

        private void Awake()
        {
            _animator = GetComponent<Animator>();
        }

        /// <summary>Set local-space movement for 2D blend tree. X = strafe, Z = forward/back. Range [-1,1].</summary>
        public void SetMove(float localX, float localZ)
        {
            _targetMoveX = Mathf.Clamp(localX, -1f, 1f);
            _targetMoveZ = Mathf.Clamp(localZ, -1f, 1f);
        }

        /// <summary>Set blend parameter for locomotion outer 1D tree. 0 = idle, 0.5 = walk, 1 = run.</summary>
        public void SetSpeed(float speed)
        {
            _targetSpeed = Mathf.Clamp01(speed);
        }

        /// <summary>Trigger a tool action animation. Ignored if already acting.</summary>
        public void PlayAction(int actionId)
        {
            if (_animator == null || IsActing) return;
            IsActing = true;
            _animator.SetInteger(ToolActionHash, actionId);
        }

        /// <summary>Reset action state. Called when action animation completes.</summary>
        public void OnActionComplete()
        {
            IsActing = false;
            if (_animator != null)
                _animator.SetInteger(ToolActionHash, 0);
        }

        private void Update()
        {
            if (_animator == null) return;

            // Smoothly damp blend parameters for nice transitions.
            float t = 1f - Mathf.Exp(-blendDamping * Time.deltaTime);
            _moveX = Mathf.Lerp(_moveX, _targetMoveX, t);
            _moveZ = Mathf.Lerp(_moveZ, _targetMoveZ, t);
            _speed = Mathf.Lerp(_speed, _targetSpeed, t);

            _animator.SetFloat(MoveXHash, _moveX);
            _animator.SetFloat(MoveZHash, _moveZ);
            _animator.SetFloat(SpeedHash, _speed);

            if (!IsActing) return;

            // Check Actions Layer (last layer) — if back in Empty state, clear acting flag.
            int actionsLayerIndex = _animator.layerCount - 1;
            var stateInfo = _animator.GetCurrentAnimatorStateInfo(actionsLayerIndex);
            if (stateInfo.IsName("Empty"))
            {
                OnActionComplete();
            }
        }
    }
}
