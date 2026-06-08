using VContainer.Unity;
using WheatFarm.Core;
using WheatFarm.Inventory;
using WheatFarm.Player.Tools;

namespace WheatFarm.UI
{
    /// <summary>
    /// Shows the currently selected crop's seed count on the HUD. Polls the
    /// PlacementTool selection + inventory each frame and updates on change.
    /// Shows âˆž when seeds are free (god mode / infinite seeds).
    /// </summary>
    public class SeedCounterPresenter : ITickable
    {
        private readonly HUDView _view;
        private readonly PlacementTool _placementTool;
        private readonly IInventoryService _inventory;
        private readonly IDebugFlags _debug;
        private string _last = null;

        public SeedCounterPresenter(
            HUDView view,
            PlacementTool placementTool,
            IInventoryService inventory,
            IDebugFlags debug = null)
        {
            _view = view;
            _placementTool = placementTool;
            _inventory = inventory;
            _debug = debug;
        }

        public void Tick()
        {
            var plant = _placementTool != null ? _placementTool.SelectedPlant : null;

            string label;
            if (plant == null)
            {
                label = "";
            }
            else if (_debug != null && _debug.SeedsAreFree)
            {
                label = $"{plant.DisplayName}: âˆ