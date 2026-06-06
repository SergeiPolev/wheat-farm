using UnityEngine;

namespace WheatFarm.Buildings
{
    /// <summary>
    /// Attached to building/decor GameObjects at instantiation time.
    /// Allows raycasting to identify which PlacedObject was clicked.
    /// </summary>
    public class BuildingMarker : MonoBehaviour
    {
        public PlacedObject PlacedObject { get; set; }
    }
}
