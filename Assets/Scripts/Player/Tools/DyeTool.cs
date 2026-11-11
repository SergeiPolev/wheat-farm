using UnityEngine;
using WheatFarm.Farming;
using WheatFarm.Player.Charges;

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
        private readonly IToolChargeService _charges;

        // Visible default until a color-picker UI exists (SelectColor is never called yet);
        // white would make both dyeing and the brush preview look like nothing happened.
        private Color _selectedColor = new(0.85f, 0.3f, 0.75f, 1f);

        public ToolId Id => ToolId.Dye;
        public bool RequiresResource => true;

        public DyeTool(IPlantSystem plantSystem, IBrushService brush, IToolChargeService charges = null)
        {
            _plantSystem = plantSystem;
            _brush = brush;
            _charges = charges;
        }

        /// <summary>Set which color to apply. Called by UI/dye selection.</summary>
        public void SelectColor(Color color) => _selectedColor = color;
        public Color SelectedColor => _selectedColor;

        public void OnEquip() { }
        public void OnUnequip() { }

        public void UseAtPosition(Vector3 worldPos)
        {
            if (_charges != null && !_charges.HasDye)
            {
                _charges.ReportDepleted(ChargeKind.Dye);
                return;
            }
            _brush.ApplyAtWorldPos(worldPos, this);
        }

        /// <summary>Plants not already this color: holding over a painted patch costs no paint.</summary>
        public bool CanApply(ChunkData chunk, int cellX, int cellY)
        {
            ref readonly var cell = ref chunk.Cells[chunk.CellIndex(cellX, cellY)];
            if (!BrushPredicates.PlantTargeting(cell)) return false;
            if (_charges == null) return true;
            return _charges.HasDye && !SameColor(cell.Color, PlantSystem.VividDye(_selectedColor));
        }

        private static bool SameColor(Color a, Color b) =>
            Mathf.Abs(a.r - b.r) < 0.01f && Mathf.Abs(a.g - b.g) < 0.01f && Mathf.Abs(a.b - b.b) < 0.01f;

        public void Apply(ChunkData chunk, int cellX, int cellY)
        {
            if (_charges != null && !_charges.TryUseDye()) return;
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
