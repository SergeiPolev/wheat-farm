using R3;
using UnityEngine;
using VContainer.Unity;

namespace WheatFarm.DayNight
{
    public enum TimeOfDay { Dawn, Day, Dusk, Night }

    public interface IDayNightService
    {
        ReadOnlyReactiveProperty<float> TimeNormalized { get; }
        ReadOnlyReactiveProperty<TimeOfDay> CurrentPhase { get; }
        ReactiveProperty<float> TimeScale { get; }
        void SetTime(float normalized);
    }

    /// <summary>
    /// 14-minute day/night cycle. Aesthetic only — does not affect gameplay.
    /// Dawn(~2min) -> Day(~6min) -> Dusk(~2min) -> Night(~4min)
    /// </summary>
    public class DayNightService : IDayNightService, ITickable
    {
        private const float CycleDuration = 14f * 60f; // 14 minutes in seconds

        private readonly ReactiveProperty<float> _time = new(0.25f); // start at day
        private readonly ReactiveProperty<TimeOfDay> _phase = new(TimeOfDay.Day);

        public ReadOnlyReactiveProperty<float> TimeNormalized => _time;
        public ReadOnlyReactiveProperty<TimeOfDay> CurrentPhase => _phase;
        public ReactiveProperty<float> TimeScale { get; } = new(1f);

        public void SetTime(float normalized)
        {
            _time.Value = Mathf.Repeat(normalized, 1f);
            _phase.Value = _time.Value switch
            {
                < 0.14f => TimeOfDay.Dawn,
                < 0.57f => TimeOfDay.Day,
                < 0.71f => TimeOfDay.Dusk,
                _ => TimeOfDay.Night
            };
        }

        public void Tick()
        {
            float dt = Time.deltaTime;
            _time.Value = (_time.Value + dt * TimeScale.Value / CycleDuration) % 1f;

            _phase.Value = _time.Value switch
            {
                < 0.14f => TimeOfDay.Dawn,
                < 0.57f => TimeOfDay.Day,
                < 0.71f => TimeOfDay.Dusk,
                _ => TimeOfDay.Night
            };
        }
    }
}
