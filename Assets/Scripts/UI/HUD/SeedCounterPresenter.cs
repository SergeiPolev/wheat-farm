using VContainer.Unity;
using WheatFarm.Inventory;
using WheatFarm.Player.Tools;
using WheatFarm.Player.Charges;

namespace WheatFarm.UI
{
    /// <summary>
    /// Shows the currently selected crop's seed count on the HUD. Polls the
    /// PlacementTool selection + inventory each frame and updates on change.
    /// </summary>
    public class SeedCounterPresenter : ITickable
    {
        private readonly HUDView _view;
        private readonly PlacementTool _placementTool;
        private readonly IInventoryService _inventory;
        private string _last = null;

        private readonly IToolChargeService _charges;

        public SeedCounterPresenter(HUDView view, PlacementTool placementTool, IInventoryService inventory,
            IToolChargeService charges = null)
        {
            _charges = charges;
            _view = view;
            _placementTool = placementTool;
            _inventory = inventory;
        }

        public void Tick()
        {
            var plant = _placementTool != null ? _placementTool.SelectedPlant : null;

            string label;
            if (plant == null)
            {
                label = "";
            }
            else
            {
                int n = _inventory.GetAmount($"seed_{plant.PlantId}");
                if (_charges != null)
                {
                    int bag = _charges.SeedPlantId.CurrentValue == plant.PlantId ? _charges.Seeds.CurrentValue : 0;
                    label = $"{plant.DisplayName} seeds: {bag} in bag, {n} stored";
                }
                else
                    label = $"{plant.DisplayName}: {n} seed" + (n == 1 ? "" : "s");
            }

            if (label != _last)
            {
                _last = label;
                _view.UpdateSeedCount(label);
            }
        }
    }
}
