using System;
using System.Collections.Generic;
using R3;
using UnityEngine;
using VContainer.Unity;
using WheatFarm.Farming;

namespace WheatFarm.Player
{
    /// <summary>
    /// Fake "resource collected" feedback: on harvest, a small token flies in an arc
    /// from the crop into the player. Pooled billboard quads, no gameplay effect.
    /// </summary>
    public class HarvestCollectFX : IInitializable, ITickable, IDisposable
    {
        private const float Duration = 0.5f;
        private const float ArcHeight = 0.8f;
        private const int MaxTokens = 64;
        private static readonly Color TokenColor = new(1f, 0.82f, 0.2f, 1f);
        private static readonly int BaseColorId = Shader.PropertyToID("_Color");

        private readonly IPlantSystem _plantSystem;
        private IDisposable _sub;

        private Transform _root;
        private Transform _player;
        private Camera _cam;
        private Mesh _quad;
        private Material _mat;
        private bool _setup;

        private readonly List<Transform> _pool = new();
        private readonly List<Vector3> _start = new();
        private readonly List<float> _startTime = new();
        private readonly List<bool> _active = new();

        public HarvestCollectFX(IPlantSystem plantSystem)
        {
            _plantSystem = plantSystem;
        }

        public void Initialize()
        {
            _sub = _plantSystem.OnHarvested.Subscribe(OnHarvested);
        }

        private void OnHarvested(HarvestData data)
        {
            EnsureSetup();
            int i = GetFreeToken();
            if (i < 0) return;

            _start[i] = data.WorldPosition + Vector3.up * 0.3f;
            _startTime[i] = Time.time;
            _active[i] = true;
            _pool[i].gameObject.SetActive(true);
            _pool[i].position = _start[i];
        }

        public void Tick()
        {
            if (!_setup) return;
            if (_player == null) _player = FindPlayer();
            if (_cam == null) _cam = Camera.main;

            Quaternion face = _cam != null ? _cam.transform.rotation : Quaternion.identity;
            Vector3 target = _player != null ? _player.position + Vector3.up * 1.2f : Vector3.zero;

            for (int i = 0; i < _pool.Count; i++)
            {
                if (!_active[i]) continue;

                float t = (Time.time - _startTime[i]) / Duration;
                if (t >= 1f || _player == null)
                {
                    _active[i] = false;
                    _pool[i].gameObject.SetActive(false);
                    continue;
                }

                Vector3 pos = Vector3.Lerp(_start[i], target, t);
                pos.y += Mathf.Sin(t * Mathf.PI) * ArcHeight;

                var tr = _pool[i];
                tr.position = pos;
                tr.rotation = face;
                tr.localScale = Vector3.one * Mathf.Lerp(0.25f, 0.08f, t);
            }
        }

        private int GetFreeToken()
        {
            for (int i = 0; i < _pool.Count; i++)
                if (!_active[i]) return i;
            if (_pool.Count < MaxTokens) { CreateToken(); return _pool.Count - 1; }
            return -1;
        }

        private void CreateToken()
        {
            var go = new GameObject("HarvestToken");
            go.transform.SetParent(_root, false);
            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = _quad;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = _mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            go.SetActive(false);

            _pool.Add(go.transform);
            _start.Add(Vector3.zero);
            _startTime.Add(0f);
            _active.Add(false);
        }

        private void EnsureSetup()
        {
            if (_setup) return;
            _setup = true;

            _root = new GameObject("HarvestCollectFX").transform;
            _quad = BuildQuad();

            var shader = Shader.Find("Sprites/Default");
            _mat = new Material(shader);
            _mat.color = TokenColor;
            if (_mat.HasProperty(BaseColorId)) _mat.SetColor(BaseColorId, TokenColor);
        }

        private Transform FindPlayer()
        {
            var pc = UnityEngine.Object.FindFirstObjectByType<PlayerController>();
            return pc != null ? pc.transform : null;
        }

        private static Mesh BuildQuad()
        {
            var m = new Mesh { name = "HarvestTokenQuad" };
            m.vertices = new[]
            {
                new Vector3(-0.5f, -0.5f, 0f),
                new Vector3(0.5f, -0.5f, 0f),
                new Vector3(0.5f, 0.5f, 0f),
                new Vector3(-0.5f, 0.5f, 0f)
            };
            m.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
            m.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            m.RecalculateBounds();
            return m;
        }

        public void Dispose()
        {
            _sub?.Dispose();
            if (_root != null) UnityEngine.Object.Destroy(_root.gameObject);
            _pool.Clear();
            _start.Clear();
            _startTime.Clear();
            _active.Clear();
        }
    }
}
