using System;
using R3;
using VContainer.Unity;
using WheatFarm.DayNight;
using WheatFarm.Economy;
using WheatFarm.Farming;
using WheatFarm.Player.Tools;

namespace WheatFarm.UI
{
    /// <summary>
    /// HUD presenter — subscribes to reactive services and drives HUDView.
    /// Pure C#, no MonoBehaviour. Registered as IInitializable + IDisposable.
    /// </summary>
    public class HUDPresenter : IInitializable, IDisposable
    {
        private readonly HUDView _view;
        private readonly IWalletService _wallet;
        private readonly IToolService _toolService;
        private readonly IDayNightService _dayNight;
        private readonly IBrushService _brush;
        private readonly CompositeDisposable _disposables = new();

        public HUDPresenter(
            HUDView view,
            IWalletService wallet,
            IToolService toolService,
            IDayNightService dayNight,
            IBrushService brush)
        {
            _view = view;
            _wallet = wallet;
            _toolService = toolService;
            _dayNight = dayNight;
            _brush = brush;
        }

        public void Initialize()
        {
            _wallet.Coins
                .Subscribe(c => _view.UpdateCoins(c))
                .AddTo(_disposables);

            _toolService.CurrentToolId
                .Subscribe(id => _view.HighlightTool((int)id))
                .AddTo(_disposables);

            _dayNight.CurrentPhase
                .Subscribe(phase => _view.UpdateTime(phase.ToString()))
                .AddTo(_disposables);

            _dayNight.TimeNormalized
                .Subscribe(t => _view.UpdateTimeFill(t))
                .AddTo(_disposables);

            _brush.CurrentSize
                .Subscribe(size => _view.UpdateBrushSize($"Brush: {size}"))
                .AddTo(_disposables);
        }

        public void Dispose() => _disposables.Dispose();
    }
}
