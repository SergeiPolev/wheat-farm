using UnityEngine;
using WheatFarm.Farming;

namespace WheatFarm.Player.Tools
{
    /// <summary>
    /// Applies a dye color to plants in brush radius.
    /// Requires a selected color (set via SelectColor before use).
    /// </summary>
    public class DyeTool : ITool, IBrushAction
    {
        private readonly IPlantSystem _plantSystem;
        private readonly IBrushService _brush;

        private Color _selectedColor = Color.white;

        public ToolId Id => ToolId.Dye;
        public bool RequiresResource => true;

        public DyeTool(IPlantSystem plantSystem, IBrushService brush)
        {
            _plantSystem = plantSystem;
            _brush = brush;
        }

        /// <summary>Set which color to apply. Called by UI/dye selection.</summary>
        public void SelectColor(Color color) => _selectedColor = color;
        public Color SelectedColor => _selectedColor;

        public void OnEquip() { }
        public void OnUnequip() { }

        public void UseAtPosition(Vector3 worldPos)
        {
            _brush.ApplyAtWorldPos(worldPos, this);
        }

        public void Apply(ChunkData chunk, int cellX, int cellY)
        {
            _plantSystem.Dye(chunk.ChunkCoord, cellX, cellY, _selectedColor);
        }
    }
}
