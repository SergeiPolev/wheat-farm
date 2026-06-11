using System.Collections.Generic;
using UnityEngine;
using WheatFarm.Farming;

namespace WheatFarm.Player.Preview
{
    public interface IBrushPreviewService
    {
        /// <summary>Call every frame while a brush tool is active; draws this frame only.</summary>
        void RenderBrush(Vector3 worldPos, IBrushAction action, Color cellColor);
        /// <summary>One-frame footprint quad (building ghost), colored by validity.</summary>
        void RenderFootprint(Vector3 center, Vector2 worldSize, bool valid);
    }

    /// <summary>
    /// Immediate-mode brush preview: instanced cell quads over exactly the cells
    /// the stroke will change (BrushService enumeration + tool CanApply) plus a
    /// radius ring. Nothing persists — skipping a frame hides the preview.
    /// </summary>
    public class BrushPreviewService : IBrushPreviewService, System.IDisposable
    {
        private const float QuadY = 0.03f; // above ground tiles at y≈0.01

        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly Color ValidColor = new(0.2f, 0.9f, 0.2f, 0.35f);
        private static readonly Color InvalidColor = new(0.9f, 0.2f, 0.2f, 0.35f);

        private readonly IBrushService _brush;
        private readonly IChunkSystem _chunks;
        private readonly Mesh _quad;
        private readonly Material _cellMaterial;
        private readonly Material _ringMaterial;
        private readonly MaterialPropertyBlock _mpb = new();
        private readonly List<Matrix4x4> _matrices = new(256);
        private readonly int _layer;

        public BrushPreviewService(IBrushService brush, IChunkSystem chunks)
        {
            _brush = brush;
            _chunks = chunks;
            _quad = BuildQuad();
            _cellMaterial = new Material(Shader.Find("WheatFarm/BrushCellPreview"));
            _ringMaterial = new Material(Shader.Find("WheatFarm/BrushRing"));
            _layer = LayerMask.NameToLayer("PlacementPreview");
        }

        public void RenderBrush(Vector3 worldPos, IBrushAction action, Color cellColor)
        {
            _matrices.Clear();
            float cell = _chunks.CellWorldSize;
            var scale = new Vector3(cell, 1f, cell);

            foreach (var (chunk, x, y) in _brush.GetAffectableCells(worldPos))
            {
                if (!action.CanApply(chunk, x, y)) continue;
                var p = _chunks.CellToWorld(chunk.ChunkCoord, x, y);
                p.y = QuadY;
                _matrices.Add(Matrix4x4.TRS(p, Quaternion.identity, scale));
            }

            if (_matrices.Count > 0)
            {
                _mpb.Clear();
                _mpb.SetColor(ColorId, cellColor);
                var rp = new RenderParams(_cellMaterial)
                {
                    layer = _layer,
                    matProps = _mpb,
                    shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off,
                    receiveShadows = false
                };
                Graphics.RenderMeshInstanced(rp, _quad, 0, _matrices);
            }

            // Radius ring (always shown, even over empty cells)
            float d = _brush.WorldRadius * 2f;
            var ringPos = new Vector3(worldPos.x, QuadY + 0.005f, worldPos.z);
            var ringRp = new RenderParams(_ringMaterial)
            {
                layer = _layer,
                shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off,
                receiveShadows = false
            };
            Graphics.RenderMesh(ringRp, _quad, 0,
                Matrix4x4.TRS(ringPos, Quaternion.identity, new Vector3(d, 1f, d)));
        }

        public void RenderFootprint(Vector3 center, Vector2 worldSize, bool valid)
        {
            _mpb.Clear();
            _mpb.SetColor(ColorId, valid ? ValidColor : InvalidColor);
            var rp = new RenderParams(_cellMaterial)
            {
                layer = _layer,
                matProps = _mpb,
                shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off,
                receiveShadows = false
            };
            var pos = new Vector3(center.x, QuadY, center.z);
            Graphics.RenderMesh(rp, _quad, 0,
                Matrix4x4.TRS(pos, Quaternion.identity, new Vector3(worldSize.x, 1f, worldSize.y)));
        }

        private static Mesh BuildQuad()
        {
            // XZ-plane unit quad centered at origin (built-in Quad faces +Z)
            var m = new Mesh { name = "BrushPreviewQuad" };
            m.vertices = new[]
            {
                new Vector3(-0.5f, 0, -0.5f), new Vector3(0.5f, 0, -0.5f),
                new Vector3(-0.5f, 0,  0.5f), new Vector3(0.5f, 0,  0.5f)
            };
            m.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1) };
            m.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
            m.triangles = new[] { 0, 2, 1, 2, 3, 1 };
            return m;
        }

        public void Dispose()
        {
            if (Application.isPlaying)
            {
                Object.Destroy(_cellMaterial);
                Object.Destroy(_ringMaterial);
                Object.Destroy(_quad);
            }
            else
            {
                Object.DestroyImmediate(_cellMaterial);
                Object.DestroyImmediate(_ringMaterial);
                Object.DestroyImmediate(_quad);
            }
        }
    }
}
