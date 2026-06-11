using System;
using System.Collections.Generic;
using UnityEngine;

namespace WheatFarm.Player.Preview
{
    /// <summary>
    /// Creates ghost materials from source materials, preserving the original
    /// texture and base color so the ghost looks like the object, not a flat blob.
    /// Caches per source material; validity tint is applied per-renderer via
    /// MaterialPropertyBlock, so cached materials never need mutation.
    /// </summary>
    public class GhostMaterialFactory : IDisposable
    {
        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        private readonly Dictionary<Material, Material> _cache = new();
        private Shader _ghostShader;

        public Material Get(Material source)
        {
            if (_cache.TryGetValue(source, out var cached) && cached != null)
                return cached;

            if (_ghostShader == null)
                _ghostShader = Shader.Find("WheatFarm/GhostPreview");

            var ghost = new Material(_ghostShader);
            if (source.HasProperty(BaseMapId))
                ghost.SetTexture(BaseMapId, source.GetTexture(BaseMapId));
            if (source.HasProperty(BaseColorId))
                ghost.SetColor(BaseColorId, source.GetColor(BaseColorId));

            _cache[source] = ghost;
            return ghost;
        }

        public void Dispose()
        {
            foreach (var mat in _cache.Values)
            {
                if (mat == null) continue;
                if (Application.isPlaying)
                    UnityEngine.Object.Destroy(mat);
                else
                    UnityEngine.Object.DestroyImmediate(mat); // EditMode tests dispose outside play mode
            }
            _cache.Clear();
        }
    }
}
