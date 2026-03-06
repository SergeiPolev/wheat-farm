using UnityEngine;

namespace WheatFarm.Player
{
    /// <summary>
    /// Isometric camera that follows the player. Smooth follow with scroll-wheel zoom.
    /// Attach to the Camera GameObject, assign Target in Inspector.
    /// </summary>
    public class CameraController : MonoBehaviour
    {
        [Header("Follow")]
        [SerializeField] private Transform target;
        [SerializeField] private Vector3 offset = new(8f, 12f, -8f);
        [SerializeField] private float smoothSpeed = 8f;

        [Header("Zoom")]
        [SerializeField] private float zoomSpeed = 3f;
        [SerializeField] private float minDistance = 8f;
        [SerializeField] private float maxDistance = 30f;

        private float _zoomFactor = 1f;

        /// <summary>Set follow target at runtime (e.g., from FarmScope).</summary>
        public void SetTarget(Transform t) => target = t;

        private void LateUpdate()
        {
            if (target == null) return;

            // Zoom
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (!Mathf.Approximately(scroll, 0f))
            {
                float currentDist = offset.magnitude * _zoomFactor;
                currentDist = Mathf.Clamp(currentDist - scroll * zoomSpeed * currentDist, minDistance, maxDistance);
                _zoomFactor = currentDist / offset.magnitude;
            }

            // Smooth follow
            Vector3 desiredPos = target.position + offset * _zoomFactor;
            transform.position = Vector3.Lerp(transform.position, desiredPos, smoothSpeed * Time.deltaTime);
            transform.LookAt(target.position);
        }
    }
}
