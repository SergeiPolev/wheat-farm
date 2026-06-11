using UnityEngine;
using WheatFarm.Farming;

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

        public ToolId Id => ToolId.Sickle;
        public bool RequiresResource => false;

        public SickleTool(IPlantSystem plantSystem, IBrushService brush)
        {
            _plantSystem = plantSystem;
            _brush = brush;
        }

        public void OnEquip() { }
        public void OnUnequip() { }

        public void UseAtPosition(Vector3 worldPos)
        {
            _brush.ApplyAtWorldPos(worldPos, this);
        }

        public bool CanApply(ChunkData chunk, int cellX, int cellY) =>
            BrushPredicates.Harvestable(chunk.Cells[chunk.CellIndex(cellX, cellY)]);

        public void Apply(ChunkData chunk, int cellX, int cellY)
        {
            _plantSystem.Harvest(chunk.ChunkCoord, cellX, cellY);
        }

        public bool PreviewActive => true;
        public Color PreviewCellColor => new(0.95f, 0.85f, 0.3f, 0.45f);
    }
}
