using UnityEngine;
using WheatFarm.Farming;

namespace WheatFarm.Player.Tools
{
    /// <summary>
    /// Applies a dye color to plants in brush radius.
    /// Requires a selected color (set via SelectColor before use).
    /// </summary>
    public class DyeTool : ITool, IBrushAction, IBrushPreviewSource
    {
        private readonly IPlantSystem _plantSystem;
        private readonly IBrushService _brush;

        // Visible default until a color-picker UI exists (SelectColor is never called yet);
        // white would make both dyeing and the brush preview look like nothing happened.
        private Color _selectedColor = new(0.85f, 0.3f, 0.75f, 1f);

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

        public bool CanApply(ChunkData chunk, int cellX, int cellY) =>
            BrushPredicates.PlantTargeting(chunk.Cells[chunk.CellIndex(cellX, cellY)]);

        public void Apply(ChunkData chunk, int cellX, int cellY)
        {
            _plantSystem.Dye(chunk.ChunkCoord, cellX, cellY, _selectedColor);
        }

        public bool PreviewActive => true;

        public Color PreviewCellColor
        {
            get
            {
                var c = _selectedColor;
                c.a = 0.45f;
                return c;
            }
        }
    }
}
