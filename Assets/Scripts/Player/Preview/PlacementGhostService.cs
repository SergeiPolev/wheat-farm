using UnityEngine;

namespace WheatFarm.Player.Preview
{
    public interface IPlacementGhostService
    {
        void Show(GameObject prefab);
        void UpdatePose(Vector3 position, float rotationY);
        void SetValid(bool valid);
        void SetVisible(bool visible);
        void Hide();
    }

    /// <summary>
    /// Owns the placement ghost instance: strips physics/scripts, swaps materials
    /// to GhostPreview (textures preserved), applies validity tint via
    /// MaterialPropertyBlock, and feeds _PreviewHighlightColor to the outline feature.
    /// </summary>
    public class PlacementGhostService : IPlacementGhostService, System.IDisposable
    {
        private static readonly int ValidityTintId = Shader.PropertyToID("_ValidityTint");
        private static readonly int PreviewHighlightColorId = Shader.PropertyToID("_PreviewHighlightColor");

        private static readonly Color ValidColor = new(0.2f, 0.9f, 0.2f, 1f);
        private static readonly Color InvalidColor = new(0.9f, 0.2f, 0.2f, 1f);

        private readonly GhostMaterialFactory _materials = new();
        private readonly MaterialPropertyBlock _mpb = new();

        private GameObject _instance;
        private Renderer[] _renderers = System.Array.Empty<Renderer>();
        private bool _valid;

        public void Show(GameObject prefab)
        {
            Hide();
            if (prefab == null) return;

            _instance = Object.Instantiate(prefab);
            _instance.name = $"Ghost_{prefab.name}";

            foreach (var col in _instance.GetComponentsInChildren<Collider>(true))
                Object.Destroy(col);
            foreach (var mb in _instance.GetComponentsInChildren<MonoBehaviour>(true))
                Object.Destroy(mb);

            int layer = LayerMask.NameToLayer("PlacementPreview");
            foreach (var t in _instance.GetComponentsInChildren<Transform>(true))
                t.gameObject.layer = layer;

            _renderers = _instance.GetComponentsInChildren<Renderer>(true);
            foreach (var r in _renderers)
            {
                var src = r.sharedMaterials;
                var mats = new Material[src.Length];
                for (int i = 0; i < mats.Length; i++)
                    mats[i] = _materials.Get(src[i]);
                r.sharedMaterials = mats;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
            }

            _valid = false;
            ApplyTint(InvalidColor);
        }

        public void UpdatePose(Vector3 position, float rotationY)
        {
            if (_instance == null) return;
            _instance.transform.SetPositionAndRotation(position, Quaternion.Euler(0, rotationY, 0));
        }

        public void SetValid(bool valid)
        {
            if (_instance == null || valid == _valid) return;
            _valid = valid;
            ApplyTint(valid ? ValidColor : InvalidColor);
        }

        public void SetVisible(bool visible)
        {
            if (_instance != null && _instance.activeSelf != visible)
                _instance.SetActive(visible);
        }

        public void Hide()
        {
            if (_instance != null)
                Object.Destroy(_instance);
            _instance = null;
            _renderers = System.Array.Empty<Renderer>();
        }

        private void ApplyTint(Color color)
        {
            _mpb.Clear();
            _mpb.SetColor(ValidityTintId, color);
            foreach (var r in _renderers)
                if (r != null)
                    r.SetPropertyBlock(_mpb);
            Shader.SetGlobalColor(PreviewHighlightColorId, color);
        }

        public void Dispose()
        {
            Hide();
            _materials.Dispose();
        }
    }
}
