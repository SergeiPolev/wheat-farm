using UnityEngine;
using WheatFarm.Farming;

namespace WheatFarm.Player.Tools
{
    /// <summary>
    /// Applies fertilizer to plants in brush radius, boosting growth speed.
    /// </summary>
    public class FertilizerTool : ITool, IBrushAction
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

        public void Apply(ChunkData chunk, int cellX, int cellY)
        {
            _plantSystem.Fertilize(chunk.ChunkCoord, cellX, cellY, FertilizerMultiplier);
        }
    }
}
