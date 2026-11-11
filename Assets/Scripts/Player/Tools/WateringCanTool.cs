using UnityEngine;
using WheatFarm.Farming;
using WheatFarm.Player.Charges;

namespace WheatFarm.Player.Tools
{
    /// <summary>
    /// Waters plants in brush radius, enabling growth.
    /// </summary>
    public class WateringCanTool : ITool, IBrushAction, IBrushPreviewSource
    {
        private readonly IPlantSystem _plantSystem;
        private readonly IBrushService _brush;
        private readonly IToolChargeService _charges;

        public ToolId Id => ToolId.WateringCan;
        public bool RequiresResource => false;

        public WateringCanTool(IPlantSystem plantSystem, IBrushService brush, IToolChargeService charges = null)
        {
            _plantSystem = plantSystem;
            _brush = brush;
            _charges = charges;
        }

        public void OnEquip() { }
        public void OnUnequip() { }

        public void UseAtPosition(Vector3 worldPos)
        {
            if (_charges != null && !_charges.HasWater)
            {
                _charges.ReportDepleted(ChargeKind.Water);
                return;
            }
            _brush.ApplyAtWorldPos(worldPos, this);
        }

        /// <summary>Only thirsty plants: holding the button over a watered patch costs nothing.</summary>
        public bool CanApply(ChunkData chunk, int cellX, int cellY)
        {
            ref readonly var cell = ref chunk.Cells[chunk.CellIndex(cellX, cellY)];
            return BrushPredicates.PlantTargeting(cell) && !cell.Watered
                   && (_charges == null || _charges.HasWater);
        }

        public void Apply(ChunkData chunk, int cellX, int cellY)
        {
            if (_charges != null && !_charges.TryUseWater()) return;
            _plantSystem.Water(chunk.ChunkCoord, cellX, cellY);
        }

        public bool PreviewActive => true;
        public Color PreviewCellColor => new(0.3f, 0.6f, 1f, 0.45f);
    }
}
