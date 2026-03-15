using UnityEngine;
using VContainer.Unity;

namespace WheatFarm.DayNight
{
    /// <summary>
    /// Drives the Directional Light based on DayNightService time.
    /// Rotates sun through an arc, adjusts color temperature and intensity.
    /// Pure aesthetic — no gameplay effect.
    /// </summary>
    public class LightingController : ITickable
    {
        private readonly IDayNightService _dayNight;
        private readonly Light _sun;

        // Sun arc: rotates from East (dawn) to overhead (noon) to West (dusk)
        // X rotation = altitude (0=horizon, 90=overhead)
        // Y rotation = azimuth (fixed isometric angle)
        private const float SunAzimuth = -30f; // match original scene rotation

        // Color gradient stops (warm dawn -> neutral day -> orange dusk -> blue night)
        private static readonly Color ColorDawn = new(1f, 0.75f, 0.5f);
        private static readonly Color ColorDay = new(1f, 0.97f, 0.92f);
        private static readonly Color ColorDusk = new(1f, 0.55f, 0.3f);
        private static readonly Color ColorNight = new(0.3f, 0.35f, 0.6f);

        // Intensity per phase
        private const float IntensityDawn = 0.6f;
        private const float IntensityDay = 1.0f;
        private const float IntensityDusk = 0.5f;
        private const float IntensityNight = 0.15f;

        public LightingController(IDayNightService dayNight, Light sun)
        {
            _dayNight = dayNight;
            _sun = sun;
        }

        public void Tick()
        {
            if (_sun == null) return;

            float t = _dayNight.TimeNormalized.CurrentValue;

            // Sun altitude: rises during dawn, peaks at noon, sets during dusk, below horizon at night
            // t=0 dawn start, t=0.14 dawn end, t=0.35 noon, t=0.57 day end, t=0.71 dusk end, t=1 midnight
            float altitude = CalculateAltitude(t);
            _sun.transform.rotation = Quaternion.Euler(altitude, SunAzimuth, 0f);

            // Color and intensity interpolation
            Color color;
            float intensity;

            if (t < 0.14f) // Dawn
            {
                float phase = t / 0.14f;
                color = Color.Lerp(ColorNight, ColorDawn, phase);
                intensity = Mathf.Lerp(IntensityNight, IntensityDawn, phase);
            }
            else if (t < 0.35f) // Dawn -> Noon
            {
                float phase = (t - 0.14f) / 0.21f;
                color = Color.Lerp(ColorDawn, ColorDay, phase);
                intensity = Mathf.Lerp(IntensityDawn, IntensityDay, phase);
            }
            else if (t < 0.57f) // Noon -> Day end
            {
                float phase = (t - 0.35f) / 0.22f;
                color = Color.Lerp(ColorDay, ColorDusk, phase * 0.3f); // slow warmup toward dusk
                intensity = Mathf.Lerp(IntensityDay, IntensityDay * 0.85f, phase);
            }
            else if (t < 0.71f) // Dusk
            {
                float phase = (t - 0.57f) / 0.14f;
                color = Color.Lerp(ColorDusk, ColorNight, phase);
                intensity = Mathf.Lerp(IntensityDusk, IntensityNight, phase);
            }
            else // Night
            {
                float phase = (t - 0.71f) / 0.29f;
                // Night holds steady with subtle variation
                color = Color.Lerp(ColorNight, ColorNight * 0.9f, Mathf.PingPong(phase * 2f, 1f));
                intensity = IntensityNight;
            }

            _sun.color = color;
            _sun.intensity = intensity;

            // Ambient light follows sun color at reduced intensity
            RenderSettings.ambientLight = color * intensity * 0.5f;
        }

        private static float CalculateAltitude(float t)
        {
            // Dawn (0-0.14): sun rises from -5° to 20°
            if (t < 0.14f)
                return Mathf.Lerp(-5f, 20f, t / 0.14f);

            // Morning to noon (0.14-0.35): 20° to 60°
            if (t < 0.35f)
                return Mathf.Lerp(20f, 60f, (t - 0.14f) / 0.21f);

            // Noon to afternoon (0.35-0.57): 60° to 25°
            if (t < 0.57f)
                return Mathf.Lerp(60f, 25f, (t - 0.35f) / 0.22f);

            // Dusk (0.57-0.71): 25° to -5°
            if (t < 0.71f)
                return Mathf.Lerp(25f, -5f, (t - 0.57f) / 0.14f);

            // Night (0.71-1.0): hold below horizon
            return -10f;
        }
    }
}
