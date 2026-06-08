using System;
using UnityEngine;

namespace WheatFarm.Core
{
    /// <summary>Farm action that can trigger a visual/particle burst.</summary>
    public enum FarmFxType { Plant, Water, Harvest, Uproot, Build, Remove }

    /// <summary>
    /// Central hook for action feedback (particles). Call PlayEffect at the action's
    /// world position. Currently spawns a simple procedural burst — swap in authored
    /// ParticleSystem prefabs per type later without touching call sites.
    /// </summary>
    public interface IFeedbackService
    {
        void PlayEffect(FarmFxType type, Vector3 worldPos);
    }

    public class FeedbackService : IFeedbackService, IDisposable
    {
        private Transform _root;
        private ParticleSystem _ps;
        private bool _setup;

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

        public void PlayEffect(FarmFxType type, Vector3 worldPos)
        {
            EnsureSetup();
            if (_ps == null) return;

            _ps.transform.position = worldPos + Vector3.up * 0.1f;
            var main = _ps.main;
            main.startColor = ColorFor(type);
            _ps.Emit(CountFor(type));
        }

        private void EnsureSetup()
        {
            if (_setup) return;
            _setup = true;

            _root = new GameObject("FarmFX").transform;

            var go = new GameObject("FarmFX_Particles");
            go.transform.SetParent(_root, false);
            _ps = go.AddComponent<ParticleSystem>();

            var main = _ps.main;
            main.loop = false;
            main.playOnAwake = false;
            main.duration = 1f;
            main.startLifetime = 0.5f;
            main.startSpeed = 1.6f;
            main.startSize = 0.14f;
            main.gravityModifier = 0.6f;
            main.maxParticles = 250;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = _ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f; // bursts only, via Emit()

            var shape = _ps.shape;
            shape.shapeType = ParticleSystemShapeType.Hemisphere;
            shape.radius = 0.12f;

            var psRenderer = _ps.GetComponent<ParticleSystemRenderer>();
            var shader = Shader.Find("Sprites/Default");
            if (shader != null) psRenderer.material = new Material(shader);

            _ps.Play(); // playing with 0 rate so Emit() bursts simulate
        }

        public void Dispose()
        {
            if (_root != null) UnityEngine.Object.Destroy(_root.gameObject);
        }
    }
}
