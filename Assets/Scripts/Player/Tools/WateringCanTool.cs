using UnityEngine;
using WheatFarm.Farming;

namespace WheatFarm.Player.Tools
{
    /// <summary>
    /// Waters plants in brush radius, enabling growth.
    /// </summary>
    public class WateringCanTool : ITool, IBrushAction
    {
        private readonly IPlantSystem _plantSystem;
        private readonly IBrushService _brush;

        public ToolId Id => ToolId.WateringCan;
        public bool RequiresResource => false;

        public WateringCanTool(IPlantSystem plantSystem, IBrushService brush)
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
            _plantSystem.Water(chunk.ChunkCoord, cellX, cellY);
        }
    }
}
