using System;
using R3;
using VContainer.Unity;
using WheatFarm.Core.Data;
using WheatFarm.Economy;
using WheatFarm.Player.Tools;

namespace WheatFarm.UI
{
    /// <summary>
    /// Shows the dye palette only while the Dye tool is active. Clicking a swatch buys it (if locked
    /// and affordable) and selects it as the active dye color. Selection drives DyeTool.SelectColor,
    /// which the brush preview already reflects.
    /// </summary>
    public class DyeColorPalettePresenter : IInitializable, IDisposable
    {
        private readonly IDyeColorPaletteView _view;
        private readonly DyeDatabase _db;
        private readonly IDyeUnlockService _unlock;
        private readonly IToolService _tools;
        private readonly DyeTool _dyeTool;
        private readonly CompositeDisposable _disposables = new();

        private int _selectedIndex = -1;

        public DyeColorPalettePresenter(IDyeColorPaletteView view, DyeDatabase db,
            IDyeUnlockService unlock, IToolService tools, DyeTool dyeTool)
        {
            _view = view;
            _db = db;
            _unlock = unlock;
            _tools = tools;
            _dyeTool = dyeTool;
        }

        public void Initialize()
        {
            _view.Build(_db.All);
            _view.SwatchClicked += OnSwatchClicked;
            _unlock.Changed += RefreshStates;

            _tools.CurrentToolId
                .Subscribe(id => _view.SetVisible(id == ToolId.Dye))
                .AddTo(_disposables);

            RefreshStates();
        }

        private void OnSwatchClicked(int index)
        {
            var dyes = _db.All;
            if (index < 0 || index >= dyes.Count) return;
            var dye = dyes[index];

            // Unlocked → select. Locked → buy (one-time); select only if the purchase succeeds.
            if (_unlock.IsUnlocked(dye) || _unlock.TryUnlock(dye))
            {
                _selectedIndex = index;
                _dyeTool.SelectColor(dye.Color);
                RefreshStates();
            }
        }

        private void RefreshStates()
        {
            var dyes = _db.All;
            for (int i = 0; i < dyes.Count; i++)
                _view.SetSwatchState(i, _unlock.IsUnlocked(dyes[i]), i == _selectedIndex, dyes[i].Cost);
        }

        public void Dispose()
        {
            _view.SwatchClicked -= OnSwatchClicked;
            _unlock.Changed -= RefreshStates;
            _disposables.Dispose();
        }
    }
}
