using VContainer.Unity;
using WheatFarm.Core.Data;
using WheatFarm.Player.Tools;

namespace WheatFarm.Player.HeldTools
{
    /// <summary>
    /// Keeps the prop in the farmer's hand in sync with the equipped tool and the PlacementTool selection.
    /// The selection has no change event, so it is polled; the view ignores repeats.
    /// </summary>
    public class HeldToolPresenter : ITickable
    {
        private readonly HeldToolView _view;
        private readonly IToolService _tools;
        private readonly PlacementTool _placement;

        public HeldToolPresenter(HeldToolView view, IToolService tools, PlacementTool placement = null)
        {
            _view = view;
            _tools = tools;
            _placement = placement;
        }

        public void Tick()
        {
            PlantCategory? plant = null;
            var selected = _placement?.SelectedPlant;
            if (selected != null) plant = selected.Category;
            _view.Show(HeldPropRules.For(_tools.CurrentToolId.CurrentValue, plant));
        }
    }
}
