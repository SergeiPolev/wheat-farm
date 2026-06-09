using System;
using UnityEngine;
using VContainer.Unity;
using WheatFarm.Core;

namespace WheatFarm.UI
{
    /// <summary>
    /// Drives the debug/cheat menu: F1 toggles the panel; toggles flip DebugFlags.
    /// </summary>
    public class DebugMenuPresenter : IInitializable, ITickable, IDisposable
    {
        private readonly DebugMenuView _view;
        private readonly IDebugFlags _flags;

        public DebugMenuPresenter(DebugMenuView view, IDebugFlags flags)
        {
            _view = view;
            _flags = flags;
        }

        public void Initialize()
        {
            _view.OnToggleChanged += OnToggle;
            _view.SetToggle("god", _flags.GodMode);
            _view.SetToggle("seeds", _flags.InfiniteSeeds);
            _view.SetToggle("coins", _flags.InfiniteCoins);
            _view.SetToggle("resources", _flags.InfiniteResources);
            _view.SetToggle("growth", _flags.InstantGrowth);

        }

        public void Dispose()
        {
            _view.OnToggleChanged -= OnToggle;
        }

        public void Tick()
        {
            if (Input.GetKeyDown(KeyCode.F1))
            {
                if (_view.IsOpen) _view.Hide();
                else _view.Show();
            }
        }

        private void OnToggle(string key, bool value)
        {
            switch (key)
            {
                case "god": _flags.GodMode = value; break;
                case "seeds": _flags.InfiniteSeeds = value; break;
                case "coins": _flags.InfiniteCoins = value; break;
                case "resources": _flags.InfiniteResources = value; break;
                case "growth": _flags.InstantGrowth = value; break;

            }
        }
    }
}
