using UnityEngine;
using WheatFarm.Farming;
using WheatFarm.Player.Charges;

namespace WheatFarm.Player.Tools
{
    /// <summary>
    /// Harvests mature plants in brush radius.
    /// Emits HarvestData events (picked up by economy system).
    /// </summary>
    public class SickleTool : ITool, IBrushAction, IBrushPreviewSource
    {
        private readonly IPlantSystem _plantSystem;
        private readonly IBrushService _brush;
        private readonly IToolChargeService _charges;

        public ToolId Id => ToolId.Sickle;
        public bool RequiresResource => false;

        public SickleTool(IPlantSystem plantSystem, IBrushService brush, IToolChargeService charges = null)
        {
            _plantSystem = plantSystem;
            _brush = brush;
            _charges = charges;
        }

        public void OnEquip() { }
        public void OnUnequip() { }

        public void UseAtPosition(Vector3 worldPos)
        {
            if (_charges != null && !_charges.BasketHasRoom)
            {
                _charges.ReportDepleted(ChargeKind.Basket);
                return;
            }
            _brush.ApplyAtWorldPos(worldPos, this);
        }

        /// <summary>Harvest goes into the basket (HarvestRewardHandler); a full basket stops the sickle.</summary>
        public bool CanApply(ChunkData chunk, int cellX, int cellY) =>
            BrushPredicates.Harvestable(chunk.Cells[chunk.CellIndex(cellX, cellY)])
            && (_charges == null || _charges.BasketHasRoom);

        public void Apply(ChunkData chunk, int cellX, int cellY)
        {
            _plantSystem.Harvest(chunk.ChunkCoord, cellX, cellY);
        }

        public bool PreviewActive => true;
        public Color PreviewCellColor => new(0.95f, 0.85f, 0.3f, 0.45f);
    }
}
