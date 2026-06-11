using UnityEngine;

namespace WheatFarm.Player.Tools
{
    public enum ToolId
    {
        Planter,
        WateringCan,
        Fertilizer,
        Dye,
        Sickle,
        Uproot,
        Build,
        Placement,
        Bulldoze
    }

    /// <summary>
    /// A tool the player can equip and use on the farm.
    /// Tools that work with brush areas also implement IBrushAction.
    /// </summary>
    public interface ITool
    {
        ToolId Id { get; }
        bool RequiresResource { get; }
        void OnEquip();
        void OnUnequip();
        void UseAtPosition(Vector3 worldPos);
    }

    /// <summary>
    /// Brush tools that want a cell-highlight preview implement this in addition
    /// to IBrushAction. CanApply (from IBrushAction) decides which cells light up.
    /// </summary>
    public interface IBrushPreviewSource
    {
        /// <summary>False disables the preview (e.g. PlacementTool in building-ghost mode).</summary>
        bool PreviewActive { get; }
        /// <summary>Cell highlight color (path tint for paths, tool color otherwise).</summary>
        UnityEngine.Color PreviewCellColor { get; }
    }
}
