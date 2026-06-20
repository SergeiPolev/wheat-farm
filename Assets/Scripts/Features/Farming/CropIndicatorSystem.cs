using System;
using System.Collections.Generic;
using UnityEngine;
using VContainer.Unity;

namespace WheatFarm.Farming
{
    /// <summary>
    /// Billboarded status markers above crops:
    ///   blue  = planted but not watered (needs watering)
    ///   gold  = fully grown (ready to harvest)
    /// Growing crops show no marker (their scale already conveys progress).
    /// Pure visual overlay — does not touch the GPU crop pipeline or save data.
    /// </summary>
    public class CropIndicatorSystem : ITickable, IDisposable
    {
        private const int MaxIcons = 160;
        private const float RescanInterval = 0.35f;
        private const float IconHeight = 0.6f;
        private const float IconSize = 0.18f;

        private static readonly Color NeedsWaterColor = new(0.25f, 0.6f, 1f, 0.95f);
        private static readonly Color ReadyColor = new(1f, 0.82f, 0.2f, 0.95f);
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        private readonly IChunkSystem _chunks;

        private Transform _root;
        private Mesh _quad;
        private Material _mat;
        private Camera _cam;
        private bool _setup;
        private float _timer;
        private int _activeCount;

        private readonly List<MeshRenderer> _pool = new();
        private readonly List<MaterialPropertyBlock> _mpb = new();
        private readonly List<Vector3> _basePos = new();
        private readonly List<bool> _isReady = new();

        public CropIndicatorSystem(IChunkSystem chunks)
        {
            _chunks = chunks;
        }

        public void Tick()
        {
            EnsureSetup();

            _timer += Time.deltaTime;
            if (_timer >= RescanInterval)
            {
                _timer = 0f;
                Rescan();
            }

            if (_cam == null) _cam = Camera.main;
            if (_cam == null) return;

            Quaternion face = _cam.transform.rotation;
            float bob = Mathf.Sin(Time.time * 3f) * 0.06f;
            var readyRoll = Quaternion.Euler(0, 0, 45f); // diamond for "ready"

            for (int i = 0; i < _activeCount; i++)
            {
                var t = _pool[i].transform;
                t.position = _basePos[i] + new Vector3(0f, bob, 0f);
                t.rotation = _isReady[i] ? face * readyRoll : face;
            }
        }

        private void Rescan()
        {
            int n = 0;
            var chunks = _chunks.GetAllUnlockedChunks();
            n = Collect(chunks, wantReady: true, n);   // prioritize ready markers
            n = Collect(chunks, wantReady: false, n);  // then needs-water

            for (int i = n; i < _activeCount; i++)
                _pool[i].enabled = false;
            _activeCount = n;
        }

        private int Collect(IReadOnlyList<ChunkData> chunks, bool wantReady, int n)
        {
            int res = _chunks.SubCellResolution;
            foreach (var chunk in chunks)
            {
                for (int idx = 0; idx < chunk.CellCount; idx++)
                {
                    if (n >= MaxIcons) return n;

                    var cell = chunk.Cells[idx];
                    if (!cell.HasPlant) continue;

                    bool ready = cell.Growth >= 1f;
                    bool needsWater = !cell.Watered && !ready;

                    if (wantReady ? !ready : !needsWater) continue;

                    int cx = idx % res;
                    int cy = idx / res;
                    Vector3 pos = _chunks.CellToWorld(chunk.ChunkCoord, cx, cy) + Vector3.up * IconHeight;
                    SetIcon(n, pos, ready);
                    n++;
                }
            }
            return n;
        }

        private void SetIcon(int i, Vector3 pos, bool ready)
        {
            while (_pool.Count <= i) CreateIcon();

            _basePos[i] = pos;
            _isReady[i] = ready;

            var r = _pool[i];
            r.enabled = true;
            r.transform.position = pos;
            r.transform.localScale = Vector3.one * IconSize;

            var mpb = _mpb[i];
            r.GetPropertyBlock(mpb);
            mpb.SetColor(BaseColorId, ready ? ReadyColor : NeedsWaterColor);
            r.SetPropertyBlock(mpb);
        }

        private void CreateIcon()
        {
            var go = new GameObject("CropIcon");
            go.transform.SetParent(_root, false);

            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = _quad;

            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = _mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mr.enabled = false;

            _pool.Add(mr);
            _mpb.Add(new MaterialPropertyBlock());
            _basePos.Add(Vector3.zero);
            _isReady.Add(false);
        }

        private void EnsureSetup()
        {
            if (_setup) return;
            _setup = true;

            _root = new GameObject("CropIndicators").transform;
            _quad = BuildQuad();

            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            _mat = new Material(shader);
            _mat.SetFloat("_Surface", 1f); // transparent
            _mat.SetFloat("_Blend", 0f);
            _mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            _mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            _mat.SetInt("_ZWrite", 0);
            _mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");            _mat.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off); // double-sided billboard

            _mat.renderQueue = 3100;
            _mat.SetColor(BaseColorId, Color.white);
        }

        private static Mesh BuildQuad()
        {
            var m = new Mesh { name = "CropIconQuad" };
            m.vertices = new[]
            {
                new Vector3(-0.5f, -0.5f, 0f),
                new Vector3(0.5f, -0.5f, 0f),
                new Vector3(0.5f, 0.5f, 0f),
                new Vector3(-0.5f, 0.5f, 0f)
            };
            m.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
            m.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        public void Dispose()
        {
            if (_root != null) UnityEngine.Object.Destroy(_root.gameObject);
            if (_mat != null) UnityEngine.Object.Destroy(_mat);
            if (_quad != null) UnityEngine.Object.Destroy(_quad);
            _pool.Clear();
            _mpb.Clear();
            _basePos.Clear();
            _isReady.Clear();
        }
    }
}
