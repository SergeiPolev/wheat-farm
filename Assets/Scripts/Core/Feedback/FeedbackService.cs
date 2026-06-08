using System;
using System.Collections.Generic;
using UnityEngine;

namespace WheatFarm.Core
{
    /// <summary>Farm action that can trigger a visual/particle burst.</summary>
    public enum FarmFxType { Plant, Water, Harvest, Uproot, Build, Remove }

    /// <summary>
    /// Central hook for action feedback (particles). Call PlayEffect at the action's
    /// world position. Uses authored ParticleSystem prefabs from FeedbackConfig when
    /// available, otherwise a simple procedural burst.
    /// </summary>
    public interface IFeedbackService
    {
        void PlayEffect(FarmFxType type, Vector3 worldPos);
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

        public void PlayEffect(FarmFxType type, Vector3 worldPos)
        {
            EnsureRoot();
            var ps = GetInstance(type);
            if (ps == null) return;

            ps.transform.position = worldPos + Vector3.up * 0.1f;
            ps.Play(true);
        }

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
                ps.Stop();
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
