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

        // The Actions layer only leaves "Empty" once the Animator has evaluated the new ToolAction,
        // which can be a frame or more after PlayAction. Completion requires having seen the action
        // state first; the timeout covers a missing/never-entered state.
        private bool _actionEntered;
        private float _actionStartTime;
        private int _actionId;
        private const int HeldActionId = 6; // KneelingWork exits on ToolAction == 0, so it is held, not one-shot
        private const float ActionTimeout = 4f;

        // "Upper Idle" layer hides the old rifle-pose arms of the strafe/backpedal clips. Forward
        // walk/jog and idle are clean full-body clips, so the layer fades in only for sideways/back.
        private int _upperLayer = -1;
        private float _upperWeight;

        // "Actions Full" is synced to "Actions" without the upper-body mask: planting, harvesting
        // and the paint throw are whole-body moves, so they use the legs when the farmer stands still
        // and fall back to upper-body only while walking (feet keep stepping).
        private int _actionsLayer = -1;
        private int _fullActionsLayer = -1;
        private float _fullWeight;

        private void Awake()
        {
            _animator = GetComponent<Animator>();
            if (_animator != null)
            {
                _upperLayer = _animator.GetLayerIndex("Upper Idle");
                _actionsLayer = _animator.GetLayerIndex("Actions");
                _fullActionsLayer = _animator.GetLayerIndex("Actions Full");
            }
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
            _actionEntered = false;
            _actionStartTime = Time.time;
            _actionId = actionId;
            _animator.SetInteger(ToolActionHash, actionId);
        }

        /// <summary>Reset action state. Called when action animation completes.</summary>
        public void OnActionComplete()
        {
            IsActing = false;
            _actionEntered = false;
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

            if (_upperLayer >= 0)
            {
                float strafe = Mathf.Abs(_moveX) + Mathf.Max(0f, -_moveZ);
                float target = _speed > 0.05f ? Mathf.Clamp01(strafe * 1.6f) : 0f;
                _upperWeight = Mathf.Lerp(_upperWeight, target, t);
                _animator.SetLayerWeight(_upperLayer, _upperWeight);
            }

            if (_fullActionsLayer >= 0)
            {
                float target = IsActing && _speed < 0.1f ? 1f : 0f;
                _fullWeight = Mathf.MoveTowards(_fullWeight, target, Time.deltaTime * 6f);
                _animator.SetLayerWeight(_fullActionsLayer, _fullWeight);
            }

            if (!IsActing) return;

            // Check Actions Layer (last layer) — if back in Empty state, clear acting flag.
            int actionsLayerIndex = _actionsLayer >= 0 ? _actionsLayer : _animator.layerCount - 1;
            var stateInfo = _animator.GetCurrentAnimatorStateInfo(actionsLayerIndex);
            bool inEmpty = stateInfo.IsName("Empty");
            bool transitioning = _animator.IsInTransition(actionsLayerIndex);

            if (!inEmpty || transitioning)
                _actionEntered = true;
            // Stop asking for the action once it is underway, so it plays once instead of looping.
            if (_actionEntered && !inEmpty && _actionId != HeldActionId)
                _animator.SetInteger(ToolActionHash, 0);

            bool timedOut = _actionId != HeldActionId && Time.time - _actionStartTime > ActionTimeout;
            if ((_actionEntered && inEmpty && !transitioning) || timedOut)
            {
                OnActionComplete();
            }
        }
    }
}
