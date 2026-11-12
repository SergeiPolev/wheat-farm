using WheatFarm.Core.Data;
using WheatFarm.Player.Tools;

namespace WheatFarm.Player.HeldTools
{
    /// <summary>The prop the farmer holds in the right hand.</summary>
    public enum HeldProp { None, WateringCan, SeedBag, PaintBucket, Sickle }

    /// <summary>Which prop goes with the equipped tool. Pure so it can be tested without a scene.</summary>
    public static class HeldPropRules
    {
        /// <param name="tool">Equipped tool.</param>
        /// <param name="selectedPlant">Category of the plant selected in the PlacementTool, or null if a placeable or nothing is selected.</param>
        public static HeldProp For(ToolId tool, PlantCategory? selectedPlant)
        {
            switch (tool)
            {
                case ToolId.WateringCan: return HeldProp.WateringCan;
                case ToolId.Dye: return HeldProp.PaintBucket;
                case ToolId.Sickle: return HeldProp.Sickle;
                case ToolId.Planter:
                case ToolId.Fertilizer: return HeldProp.SeedBag;
                case ToolId.Placement:
                    // Crops and bushes are sown from the bag; trees and buildings are placed, not sown.
                    return selectedPlant.HasValue && selectedPlant.Value != PlantCategory.Tree
                        ? HeldProp.SeedBag
                        : HeldProp.None;
                default: return HeldProp.None;
            }
        }
    }
}
