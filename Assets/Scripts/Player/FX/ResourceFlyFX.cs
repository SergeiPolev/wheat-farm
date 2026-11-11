using System;
using System.Collections.Generic;
using UnityEngine;
using VContainer.Unity;

namespace WheatFarm.Player.FX
{
    /// <summary>What a flying token looks like.</summary>
    public enum FlyToken { Water, Paint, Seed, Crop, Coin }

    /// <summary>
    /// "Physical" transfer feedback: small 3D tokens hop along an arc between the player and a
    /// building (Well, Warehouse, Market, Contracts) or from a field cell to the player.
    /// Targets that move (the player) are tracked every frame; arrival gives the receiver a
    /// squash-and-stretch bounce. Pooled, no gameplay effect.
    /// </summary>
    public interface IResourceFlyFX
    {
        /// <summary>Tokens from a world point (or object) to the player.</summary>
        void ToPlayer(FlyToken token, Vector3 from, int count = 1, Transform fromObject = null, float delay = 0f);
        /// <summary>Tokens from the player to a world point / object (the object bounces on arrival).</summary>
        void FromPlayer(FlyToken token, Vector3 to, int count = 1, Transform toObject = null, float delay = 0f);
        /// <summary>Generic flight between two points.</summary>
        void Fly(FlyToken token, Vector3 from, Vector3 to, int count = 1, Transform bounceOnArrive = null);
    }

    public class ResourceFlyFX : IResourceFlyFX, ITickable, IDisposable
    {
        private const int MaxTokens = 220;
        private const float Duration = 0.55f;
        private const float Stagger = 0.045f;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        private class Token
        {
            public Transform Tr;
            public MeshRenderer Renderer;
            public MeshFilter Filter;
            public TrailRenderer Trail;
            public bool Active;
            public float Start;          // may be in the future (stagger)
            public Vector3 From, To, Side;
            public bool ToPlayer, FromPlayerStart;
            public Transform Bounce;
            public FlyToken Kind;
            public float Spin;
        }

        private class BounceState { public Vector3 BaseScale; public float T; }

        private readonly List<Token> _tokens = new();
        private readonly Dictionary<Transform, BounceState> _bounces = new();
        private readonly List<Transform> _bounceDone = new();
        private readonly Dictionary<FlyToken, Material> _materials = new();
        private readonly Dictionary<FlyToken, Mesh> _meshes = new();
        private readonly System.Random _rng = new(3);

        private Transform _root;
        private Transform _player;
        private Transform _playerModel;
        private Transform _playerHead;

        // ── API ─────────────────────────────────────────────

        public void ToPlayer(FlyToken token, Vector3 from, int count = 1, Transform fromObject = null, float delay = 0f)
        {
            if (!FindPlayer()) return;
            Spawn(token, from, Vector3.zero, count, toPlayer: true, fromPlayer: false, bounce: _playerModel, delay);
            if (fromObject != null) Bounce(fromObject, 0.5f); // little "push" when the source gives
        }

        public void FromPlayer(FlyToken token, Vector3 to, int count = 1, Transform toObject = null, float delay = 0f)
        {
            if (!FindPlayer()) return;
            Spawn(token, Vector3.zero, to, count, toPlayer: false, fromPlayer: true, bounce: toObject, delay);
        }

        public void Fly(FlyToken token, Vector3 from, Vector3 to, int count = 1, Transform bounceOnArrive = null) =>
            Spawn(token, from, to, count, toPlayer: false, fromPlayer: false, bounce: bounceOnArrive);

        private void Spawn(FlyToken kind, Vector3 from, Vector3 to, int count, bool toPlayer, bool fromPlayer, Transform bounce, float delay = 0f)
        {
            if (!Application.isPlaying) return;
            EnsureRoot();
            if (fromPlayer) from = PlayerAnchor(); // launch point = where the player is now
            count = Mathf.Clamp(count, 1, 24);
            for (int i = 0; i < count; i++)
            {
                var t = GetFree();
                if (t == null) return;
                t.Active = true;
                t.Kind = kind;
                t.Start = Time.time + delay + i * Stagger;
                t.From = from + Jitter(0.12f);
                t.To = to + Jitter(0.08f);
                t.ToPlayer = toPlayer;
                t.FromPlayerStart = false; // launch point already resolved
                t.Bounce = bounce;
                t.Side = Vector3.Cross(Vector3.up, (to - from).normalized) * ((float)_rng.NextDouble() - 0.5f) * 0.9f;
                t.Spin = 200f + (float)_rng.NextDouble() * 400f;
                t.Filter.sharedMesh = MeshFor(kind);
                t.Renderer.sharedMaterial = MaterialFor(kind);
                t.Tr.localScale = Vector3.zero;
                t.Tr.position = toPlayer || !fromPlayer ? t.From : from;
                var c = ColorFor(kind);
                t.Trail.startColor = new Color(c.r, c.g, c.b, 0.7f);
                t.Trail.endColor = new Color(c.r, c.g, c.b, 0f);
                t.Trail.Clear();
                t.Tr.gameObject.SetActive(true);
            }
        }

        // ── Simulation ──────────────────────────────────────

        public void Tick()
        {
            if (_root == null) return;
            FindPlayer();
            Vector3 playerAnchor = PlayerAnchor();
            float now = Time.time;

            foreach (var t in _tokens)
            {
                if (!t.Active) continue;
                float u = (now - t.Start) / Duration;
                if (u < 0f) { t.Tr.localScale = Vector3.zero; continue; }

                // A token flying to the player homes in on where the player is now
                Vector3 from = t.From;
                Vector3 to = t.ToPlayer ? playerAnchor : t.To;

                if (u >= 1f)
                {
                    t.Active = false;
                    t.Trail.Clear();
                    t.Tr.gameObject.SetActive(false);
                    if (t.Bounce != null) Bounce(t.Bounce, 1f);
                    continue;
                }

                float e = EaseInOut(u);
                float dist = Vector3.Distance(from, to);
                // Even a short hop (player right next to the Well) gets a visible arc
                Vector3 mid = (from + to) * 0.5f + Vector3.up * (1.1f + dist * 0.3f) + t.Side;
                Vector3 p = Bezier(from, mid, to, e);

                float pop = u < 0.18f ? EaseOutBack(u / 0.18f) : 1f;
                float shrink = u > 0.8f ? Mathf.Lerp(1f, 0.55f, (u - 0.8f) / 0.2f) : 1f;
                float s = BaseSize(t.Kind) * pop * shrink;

                t.Tr.position = p;
                t.Tr.localScale = ShapeScale(t.Kind) * s;
                t.Tr.rotation = t.Kind == FlyToken.Coin
                    ? Quaternion.Euler(70f, now * t.Spin, 0f)
                    : Quaternion.Euler(0f, now * t.Spin * 0.5f, Mathf.Sin(now * 9f) * 20f);
            }

            TickBounces();
        }

        private Vector3 PlayerAnchor()
        {
            if (_playerHead != null) return _playerHead.position - Vector3.up * 0.25f;
            return _player != null ? _player.position + Vector3.up * 0.4f : Vector3.zero;
        }

        // ── Receiver bounce (squash & stretch) ──────────────

        private void Bounce(Transform tr, float strength)
        {
            if (tr == null) return;
            if (!_bounces.TryGetValue(tr, out var b))
            {
                b = new BounceState { BaseScale = tr.localScale };
                _bounces[tr] = b;
            }
            b.T = Mathf.Max(b.T, 0.28f * strength);
        }

        private void TickBounces()
        {
            if (_bounces.Count == 0) return;
            float dt = Time.deltaTime;
            _bounceDone.Clear();
            foreach (var kv in _bounces)
            {
                var tr = kv.Key;
                var b = kv.Value;
                if (tr == null) { _bounceDone.Add(tr); continue; }
                b.T = Mathf.Max(0f, b.T - dt);
                float k = b.T / 0.28f;                       // 1 → 0
                float wobble = Mathf.Sin((1f - k) * Mathf.PI * 3f) * k * 0.09f;
                tr.localScale = new Vector3(
                    b.BaseScale.x * (1f + wobble),
                    b.BaseScale.y * (1f - wobble),
                    b.BaseScale.z * (1f + wobble));
                if (b.T <= 0f) { tr.localScale = b.BaseScale; _bounceDone.Add(tr); }
            }
            foreach (var tr in _bounceDone) _bounces.Remove(tr);
        }

        // ── Looks ───────────────────────────────────────────

        private static float BaseSize(FlyToken k) => k switch
        {
            FlyToken.Coin => 0.26f,
            FlyToken.Crop => 0.26f,
            FlyToken.Seed => 0.18f,
            _ => 0.22f
        };

        private static Vector3 ShapeScale(FlyToken k) => k switch
        {
            FlyToken.Water => new Vector3(0.85f, 1.25f, 0.85f),  // droplet
            FlyToken.Paint => new Vector3(1f, 0.8f, 1f),          // blob
            FlyToken.Seed => new Vector3(0.7f, 1.1f, 0.7f),
            FlyToken.Crop => new Vector3(0.6f, 1.4f, 0.6f),       // little sheaf
            FlyToken.Coin => new Vector3(1f, 0.18f, 1f),          // flat disc
            _ => Vector3.one
        };

        private static Color ColorFor(FlyToken k) => k switch
        {
            FlyToken.Water => new Color(0.35f, 0.7f, 1f),
            FlyToken.Paint => new Color(0.9f, 0.4f, 0.85f),
            FlyToken.Seed => new Color(0.78f, 0.62f, 0.36f),
            FlyToken.Crop => new Color(1f, 0.82f, 0.25f),
            FlyToken.Coin => new Color(1f, 0.85f, 0.2f),
            _ => Color.white
        };

        private Mesh MeshFor(FlyToken k)
        {
            if (_meshes.TryGetValue(k, out var m) && m != null) return m;
            var prim = k is FlyToken.Coin ? PrimitiveType.Cylinder
                     : k is FlyToken.Crop or FlyToken.Seed ? PrimitiveType.Capsule
                     : PrimitiveType.Sphere;
            var tmp = GameObject.CreatePrimitive(prim);
            m = tmp.GetComponent<MeshFilter>().sharedMesh;
            UnityEngine.Object.Destroy(tmp);
            _meshes[k] = m;
            return m;
        }

        private Material MaterialFor(FlyToken k)
        {
            if (_materials.TryGetValue(k, out var mat) && mat != null) return mat;
            var shader = Shader.Find("Universal Render Pipeline/Simple Lit") ?? Shader.Find("Universal Render Pipeline/Lit");
            mat = new Material(shader);
            var c = ColorFor(k);
            if (mat.HasProperty(BaseColorId)) mat.SetColor(BaseColorId, c);
            if (mat.HasProperty(ColorId)) mat.SetColor(ColorId, c);
            // A touch of glow so tokens read against bright fields
            if (mat.HasProperty("_EmissionColor"))
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", c * 0.35f);
            }
            _materials[k] = mat;
            return mat;
        }

        // ── Pool / helpers ──────────────────────────────────

        private Token GetFree()
        {
            foreach (var t in _tokens) if (!t.Active) return t;
            if (_tokens.Count >= MaxTokens) return null;
            var go = new GameObject("FlyToken");
            go.transform.SetParent(_root, false);
            var mf = go.AddComponent<MeshFilter>();
            var mr = go.AddComponent<MeshRenderer>();
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            var trail = go.AddComponent<TrailRenderer>();
            trail.time = 0.18f;
            trail.minVertexDistance = 0.03f;
            trail.widthMultiplier = 0.09f;
            trail.widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            trail.receiveShadows = false;
            trail.sharedMaterial = TrailMaterial();
            go.SetActive(false);
            var tok = new Token { Tr = go.transform, Renderer = mr, Filter = mf, Trail = trail };
            _tokens.Add(tok);
            return tok;
        }

        private Material _trailMat;
        private Material TrailMaterial()
        {
            if (_trailMat != null) return _trailMat;
            var shader = Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Unlit");
            _trailMat = new Material(shader);
            return _trailMat;
        }

        private void EnsureRoot()
        {
            if (_root == null) _root = new GameObject("ResourceFlyFX").transform;
        }

        private bool FindPlayer()
        {
            if (_player != null) return true;
            var pc = UnityEngine.Object.FindFirstObjectByType<PlayerController>();
            if (pc == null) return false;
            _player = pc.transform;
            var anim = pc.GetComponentInChildren<Animator>();
            if (anim != null)
            {
                _playerModel = anim.transform;
                if (anim.isHuman) _playerHead = anim.GetBoneTransform(HumanBodyBones.Head);
            }
            return true;
        }

        private Vector3 Jitter(float r) =>
            new(((float)_rng.NextDouble() - 0.5f) * 2f * r, ((float)_rng.NextDouble() - 0.5f) * r, ((float)_rng.NextDouble() - 0.5f) * 2f * r);

        private static Vector3 Bezier(Vector3 a, Vector3 b, Vector3 c, float t)
        {
            float u = 1f - t;
            return u * u * a + 2f * u * t * b + t * t * c;
        }

        private static float EaseInOut(float t) => t < 0.5f ? 2f * t * t : 1f - Mathf.Pow(-2f * t + 2f, 2f) / 2f;

        private static float EaseOutBack(float t)
        {
            const float c1 = 1.70158f, c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
        }

        public void Dispose()
        {
            foreach (var kv in _bounces) if (kv.Key != null) kv.Key.localScale = kv.Value.BaseScale;
            _bounces.Clear();
            if (_root != null) UnityEngine.Object.Destroy(_root.gameObject);
            foreach (var m in _materials.Values) if (m != null) UnityEngine.Object.Destroy(m);
            _materials.Clear();
            _tokens.Clear();
        }
    }
}
