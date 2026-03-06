using UnityEngine;
using WheatFarm.Farming;

namespace WheatFarm.Player.Tools
{
    /// <summary>
    /// Removes plants from cells in brush radius. No harvest yield.
    /// </summary>
    public class UprootTool : ITool, IBrushAction
    {
        private readonly IPlantSystem _plantSystem;
        private readonly IBrushService _brush;

        public ToolId Id => ToolId.Uproot;
        public bool RequiresResource => false;

        public UprootTool(IPlantSystem plantSystem, IBrushService brush)
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
            _plantSystem.Uproot(chunk.ChunkCoord, cellX, cellY);
        }
    }
}
