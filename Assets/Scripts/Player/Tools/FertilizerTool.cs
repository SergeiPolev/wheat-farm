using UnityEngine;
using WheatFarm.Farming;

namespace WheatFarm.Player.Tools
{
    /// <summary>
    /// Applies fertilizer to plants in brush radius, boosting growth speed.
    /// </summary>
    public class FertilizerTool : ITool, IBrushAction, IBrushPreviewSource
    {
        private readonly IPlantSystem _plantSystem;
        private readonly IBrushService _brush;

        /// <summary>Growth multiplier applied by fertilizer (2x default).</summary>
        public float FertilizerMultiplier { get; set; } = 2f;

        public ToolId Id => ToolId.Fertilizer;
        public bool RequiresResource => true;

        public FertilizerTool(IPlantSystem plantSystem, IBrushService brush)
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
            BrushPredicates.PlantTargeting(chunk.Cells[chunk.CellIndex(cellX, cellY)]);

        public void Apply(ChunkData chunk, int cellX, int cellY)
        {
            _plantSystem.Fertilize(chunk.ChunkCoord, cellX, cellY, FertilizerMultiplier);
        }

        public bool PreviewActive => true;
        public Color PreviewCellColor => new(0.85f, 0.65f, 0.2f, 0.45f);
    }
}
