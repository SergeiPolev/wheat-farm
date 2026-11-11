using System;
using System.Collections.Generic;
using UnityEngine;

namespace WheatFarm.Core
{
    public enum SfxId { Plant, Water, Harvest, Paint, Uproot, Build, Remove, RefillTick, Full, Empty, Unload, ToolSwitch }

    /// <summary>
    /// Tiny procedural sound kit: every clip is synthesized at startup (no audio assets yet),
    /// played through a small pool of 2D AudioSources with per-id rate limiting so brush strokes
    /// that touch 30 cells a frame still sound like one gesture.
    /// </summary>
    public interface ISfxService
    {
        void Play(SfxId id, float pitch = 1f, float volume = 1f);
        float MasterVolume { get; set; }
    }

    public class SfxService : ISfxService, IDisposable
    {
        private const int SampleRate = 44100;
        private const int Voices = 10;

        private readonly Dictionary<SfxId, AudioClip> _clips = new();
        private readonly Dictionary<SfxId, float> _lastPlayed = new();
        private readonly System.Random _rng = new(7);
        private GameObject _root;
        private AudioSource[] _sources;
        private int _next;

        public float MasterVolume { get; set; } = 0.55f;

        public void Play(SfxId id, float pitch = 1f, float volume = 1f)
        {
            if (!Application.isPlaying) return;
            float now = Time.unscaledTime;
            float gap = MinGap(id);
            if (_lastPlayed.TryGetValue(id, out var last) && now - last < gap) return;
            _lastPlayed[id] = now;

            EnsureInit();
            var src = _sources[_next];
            _next = (_next + 1) % _sources.Length;
            src.pitch = pitch * (1f + ((float)_rng.NextDouble() - 0.5f) * Jitter(id));
            src.PlayOneShot(GetClip(id), Mathf.Clamp01(volume * BaseVolume(id) * MasterVolume));
        }

        private static float MinGap(SfxId id) => id switch
        {
            SfxId.Plant or SfxId.Harvest => 0.07f,
            SfxId.Water or SfxId.Paint => 0.16f,
            SfxId.RefillTick => 0.09f,
            SfxId.Empty => 0.5f,
            _ => 0.05f
        };

        private static float Jitter(SfxId id) => id switch
        {
            SfxId.Full or SfxId.Unload or SfxId.Empty or SfxId.ToolSwitch => 0f,
            _ => 0.18f
        };

        private static float BaseVolume(SfxId id) => id switch
        {
            SfxId.Water => 0.55f,
            SfxId.Paint => 0.6f,
            SfxId.RefillTick => 0.5f,
            SfxId.ToolSwitch => 0.45f,
            _ => 0.8f
        };

        private void EnsureInit()
        {
            if (_root != null) return;
            _root = new GameObject("SFX");
            UnityEngine.Object.DontDestroyOnLoad(_root);
            _sources = new AudioSource[Voices];
            for (int i = 0; i < Voices; i++)
            {
                var s = _root.AddComponent<AudioSource>();
                s.playOnAwake = false;
                s.spatialBlend = 0f;
                _sources[i] = s;
            }
        }

        private AudioClip GetClip(SfxId id)
        {
            if (_clips.TryGetValue(id, out var c) && c != null) return c;
            c = Synthesize(id);
            _clips[id] = c;
            return c;
        }

        // ── Synthesis ─────────────────────────────────────────

        /// <summary>Public for tests / previews.</summary>
        public static AudioClip Synthesize(SfxId id)
        {
            switch (id)
            {
                case SfxId.Plant:   return Tone("sfx_plant", 0.07f, 520f, 240f, 0.9f, noise: 0.15f, decay: 38f);
                case SfxId.Harvest: return Snip();
                case SfxId.Water:   return Noise("sfx_water", 0.22f, cutoff: 0.10f, decay: 12f, attack: 0.02f);
                case SfxId.Paint:   return Noise("sfx_paint", 0.14f, cutoff: 0.05f, decay: 20f, attack: 0.005f, wobble: 9f);
                case SfxId.Uproot:  return Tone("sfx_uproot", 0.12f, 180f, 90f, 0.8f, noise: 0.5f, decay: 22f);
                case SfxId.Build:   return Tone("sfx_build", 0.18f, 140f, 110f, 1f, noise: 0.35f, decay: 18f);
                case SfxId.Remove:  return Tone("sfx_remove", 0.16f, 260f, 120f, 0.8f, noise: 0.45f, decay: 20f);
                case SfxId.RefillTick: return Tone("sfx_glug", 0.08f, 230f, 460f, 0.9f, noise: 0.05f, decay: 30f);
                case SfxId.Full:    return Chime("sfx_full", new[] { 784f, 1175f }, 0.32f);
                case SfxId.Unload:  return Chime("sfx_unload", new[] { 988f, 1319f }, 0.22f);
                case SfxId.Empty:   return Tone("sfx_empty", 0.16f, 170f, 120f, 1f, noise: 0.05f, decay: 14f);
                case SfxId.ToolSwitch: return Tone("sfx_switch", 0.04f, 900f, 1300f, 0.6f, noise: 0f, decay: 60f);
                default: return Tone("sfx_default", 0.05f, 440f, 440f, 0.5f, 0f, 40f);
            }
        }

        /// <summary>Pitch-swept sine with optional noise, exponential decay.</summary>
        private static AudioClip Tone(string name, float seconds, float f0, float f1, float amp, float noise, float decay)
        {
            int n = Mathf.CeilToInt(seconds * SampleRate);
            var data = new float[n];
            var rng = new System.Random(name.GetHashCode());
            double phase = 0;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SampleRate;
                float k = i / (float)n;
                float f = Mathf.Lerp(f0, f1, k);
                phase += 2.0 * Math.PI * f / SampleRate;
                float env = Mathf.Exp(-decay * t) * Mathf.Clamp01(t / 0.003f);
                float s = (float)Math.Sin(phase) * (1f - noise) + ((float)rng.NextDouble() * 2f - 1f) * noise;
                data[i] = s * env * amp;
            }
            return Make(name, data);
        }

        /// <summary>One-pole low-passed noise: water swish / paint splat.</summary>
        private static AudioClip Noise(string name, float seconds, float cutoff, float decay, float attack, float wobble = 0f)
        {
            int n = Mathf.CeilToInt(seconds * SampleRate);
            var data = new float[n];
            var rng = new System.Random(name.GetHashCode());
            float y = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SampleRate;
                float c = cutoff * (1f + (wobble > 0 ? 0.5f * Mathf.Sin(t * wobble * 6.283f) : 0f));
                y += c * (((float)rng.NextDouble() * 2f - 1f) - y);
                float env = Mathf.Clamp01(t / attack) * Mathf.Exp(-decay * t);
                data[i] = y * env * 2.2f;
            }
            return Make(name, data);
        }

        /// <summary>Short bright click + high blip: a sickle snip.</summary>
        private static AudioClip Snip()
        {
            int n = Mathf.CeilToInt(0.06f * SampleRate);
            var data = new float[n];
            var rng = new System.Random(11);
            double phase = 0;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SampleRate;
                phase += 2.0 * Math.PI * Mathf.Lerp(1800f, 1100f, i / (float)n) / SampleRate;
                float click = ((float)rng.NextDouble() * 2f - 1f) * Mathf.Exp(-180f * t);
                float tone = (float)Math.Sin(phase) * Mathf.Exp(-45f * t) * 0.5f;
                data[i] = (click * 0.6f + tone) * 0.9f;
            }
            return Make("sfx_snip", data);
        }

        /// <summary>Arpeggiated soft bell notes.</summary>
        private static AudioClip Chime(string name, float[] notes, float seconds)
        {
            int n = Mathf.CeilToInt(seconds * SampleRate);
            var data = new float[n];
            float step = seconds * 0.28f;
            for (int k = 0; k < notes.Length; k++)
            {
                int start = Mathf.RoundToInt(k * step * SampleRate);
                for (int i = start; i < n; i++)
                {
                    float t = (i - start) / (float)SampleRate;
                    float env = Mathf.Exp(-9f * t) * Mathf.Clamp01(t / 0.004f);
                    float s = Mathf.Sin(2f * Mathf.PI * notes[k] * t) + 0.25f * Mathf.Sin(4f * Mathf.PI * notes[k] * t);
                    data[i] += s * env * 0.45f;
                }
            }
            return Make(name, data);
        }

        private static AudioClip Make(string name, float[] data)
        {
            var clip = AudioClip.Create(name, data.Length, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        public void Dispose()
        {
            if (_root != null) UnityEngine.Object.Destroy(_root);
            foreach (var c in _clips.Values) if (c != null) UnityEngine.Object.Destroy(c);
            _clips.Clear();
        }
    }
}
