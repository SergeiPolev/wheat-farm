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
        Build
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
}
