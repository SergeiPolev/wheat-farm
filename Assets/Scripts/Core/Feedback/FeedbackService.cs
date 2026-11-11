using System;
using System.Collections.Generic;
using UnityEngine;

namespace WheatFarm.Core
{
    /// <summary>Farm action that can trigger a visual/particle burst.</summary>
    public enum FarmFxType { Plant, Water, Harvest, Uproot, Build, Remove, Dye }

    /// <summary>
    /// Central hook for action feedback (particles). Call PlayEffect at the action's
    /// world position. Uses authored ParticleSystem prefabs from FeedbackConfig when
    /// available, otherwise a simple procedural burst.
    /// </summary>
    public interface IFeedbackService
    {
        void PlayEffect(FarmFxType type, Vector3 worldPos);
        /// <summary>Same, with a per-burst color override (e.g. the dye being applied).</summary>
        void PlayEffect(FarmFxType type, Vector3 worldPos, Color color);
    }

    public class FeedbackService : IFeedbackService, IDisposable
    {
        private readonly FeedbackConfig _config;
        private Transform _root;
        private Material _proceduralMat;
        private readonly Dictionary<FarmFxType, ParticleSystem> _instances = new();

        public FeedbackService(FeedbackConfig config = null)
        {
            _config = config;
        }

        // Brush strokes call PlayEffect once per affected cell, every frame the button is held.
        // Emitting (instead of restarting one system) lets every cell puff; a per-frame budget
        // keeps big brushes from flooding the screen.
        private const int MaxParticlesPerTypePerFrame = 90;
        private int _budgetFrame = -1;
        private readonly Dictionary<FarmFxType, int> _budget = new();

        public void PlayEffect(FarmFxType type, Vector3 worldPos) => Emit(type, worldPos, null);

        public void PlayEffect(FarmFxType type, Vector3 worldPos, Color color) => Emit(type, worldPos, color);

        private void Emit(FarmFxType type, Vector3 worldPos, Color? color)
        {
            EnsureRoot();
            var ps = GetInstance(type);
            if (ps == null) return;

            int frame = Time.frameCount;
            if (frame != _budgetFrame) { _budgetFrame = frame; _budget.Clear(); }
            _budget.TryGetValue(type, out int used);
            int count = PerCellFor(type);
            if (used + count > MaxParticlesPerTypePerFrame) return;
            _budget[type] = used + count;

            var ep = new ParticleSystem.EmitParams
            {
                position = worldPos + Vector3.up * 0.1f,
                applyShapeToPosition = true
            };
            if (color.HasValue) ep.startColor = color.Value;
            ps.Emit(ep, count);
        }

        private static int PerCellFor(FarmFxType t) => t switch
        {
            FarmFxType.Build => 22,
            FarmFxType.Remove => 18,
            FarmFxType.Harvest => 5,
            FarmFxType.Plant => 3,
            _ => 4
        };

        private void EnsureRoot()
        {
            if (_root != null) return;
            _root = new GameObject("FarmFX").transform;
        }

        private ParticleSystem GetInstance(FarmFxType type)
        {
            if (_instances.TryGetValue(type, out var existing) && existing != null)
                return existing;

            var prefab = _config != null ? _config.GetPrefab(type) : null;
            ParticleSystem ps;

            if (prefab != null)
            {
                var go = UnityEngine.Object.Instantiate(prefab, _root);
                ps = go.GetComponent<ParticleSystem>();
                if (ps == null) ps = go.GetComponentInChildren<ParticleSystem>();
            }
            else
            {
                ps = BuildProcedural(type);
            }

            if (ps != null)
            {
                var main = ps.main;
                main.playOnAwake = false;
                main.simulationSpace = ParticleSystemSimulationSpace.World; // Emit at arbitrary world positions
                main.maxParticles = Mathf.Max(main.maxParticles, 600);
                ps.transform.position = Vector3.zero;
                // Manual Emit only: keep the system playing (so emitted particles simulate)
                // but with automatic emission/bursts off.
                var emission = ps.emission;
                emission.enabled = false;
                main.loop = true;
                ps.Play(true);
                _instances[type] = ps;
            }
            return ps;
        }

        private ParticleSystem BuildProcedural(FarmFxType type)
        {
            var go = new GameObject($"FarmFX_{type}");
            go.transform.SetParent(_root, false);
            var ps = go.AddComponent<ParticleSystem>();

            var main = ps.main;
            main.loop = false;
            main.playOnAwake = false;
            main.duration = 1f;
            main.startLifetime = 0.5f;
            main.startSpeed = 1.6f;
            main.startSize = 0.14f;
            main.gravityModifier = 0.6f;
            main.maxParticles = 250;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startColor = ColorFor(type);

            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)CountFor(type)) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Hemisphere;
            shape.radius = 0.12f;

            var psRenderer = ps.GetComponent<ParticleSystemRenderer>();
            if (_proceduralMat == null)
            {
                var shader = Shader.Find("Sprites/Default");
                if (shader != null) _proceduralMat = new Material(shader);
            }
            if (_proceduralMat != null) psRenderer.material = _proceduralMat;

            return ps;
        }

        private static Color ColorFor(FarmFxType t) => t switch
        {
            FarmFxType.Plant => new Color(0.40f, 0.85f, 0.35f),
            FarmFxType.Water => new Color(0.30f, 0.60f, 1.00f),
            FarmFxType.Harvest => new Color(1.00f, 0.82f, 0.20f),
            FarmFxType.Uproot => new Color(0.60f, 0.40f, 0.20f),
            FarmFxType.Build => Color.white,
            FarmFxType.Remove => new Color(0.70f, 0.70f, 0.70f),
            FarmFxType.Dye => new Color(0.88f, 0.38f, 0.80f),
            _ => Color.white
        };

        private static int CountFor(FarmFxType t) => t switch
        {
            FarmFxType.Harvest => 16,
            FarmFxType.Build => 22,
            FarmFxType.Remove => 18,
            _ => 10
        };

        public void Dispose()
        {
            if (_root != null) UnityEngine.Object.Destroy(_root.gameObject);
            _instances.Clear();
        }
    }
}
