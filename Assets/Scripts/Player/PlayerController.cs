using UnityEngine;

namespace WheatFarm.Player
{
    /// <summary>
    /// WASD movement controller. Camera-relative isometric movement.
    /// Attach to player GameObject in scene.
    /// </summary>
    public class PlayerController : MonoBehaviour
    {
        [SerializeField] private float moveSpeed = 6f;
        [SerializeField] private float rotationSpeed = 10f;

        private Camera _cam;
        private Vector3 _moveDir;

        private void Start()
        {
            _cam = Camera.main;
        }

        private void Update()
        {
            float h = Input.GetAxisRaw("Horizontal");
            float v = Input.GetAxisRaw("Vertical");

            if (Mathf.Approximately(h, 0f) && Mathf.Approximately(v, 0f))
            {
                _moveDir = Vector3.zero;
                return;
            }

            // Camera-relative direction on XZ plane
            Vector3 camForward = _cam.transform.forward;
            Vector3 camRight = _cam.transform.right;
            camForward.y = 0f;
            camRight.y = 0f;
            camForward.Normalize();
            camRight.Normalize();

            _moveDir = (camForward * v + camRight * h).normalized;
            transform.position += _moveDir * (moveSpeed * Time.deltaTime);

            // Rotate to face movement direction
            if (_moveDir.sqrMagnitude > 0.01f)
            {
                Quaternion targetRot = Quaternion.LookRotation(_moveDir, Vector3.up);
                transform.rotation = Quaternion.Slerp(
                    transform.rotation, targetRot, rotationSpeed * Time.deltaTime);
            }
        }
    }
}
