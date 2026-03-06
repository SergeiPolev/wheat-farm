using UnityEngine;
using WheatFarm.Farming;

namespace WheatFarm.Player.Tools
{
    /// <summary>
    /// Harvests mature plants in brush radius.
    /// Emits HarvestData events (picked up by economy system).
    /// </summary>
    public class SickleTool : ITool, IBrushAction
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

        public void Apply(ChunkData chunk, int cellX, int cellY)
        {
            _plantSystem.Harvest(chunk.ChunkCoord, cellX, cellY);
        }
    }
}
