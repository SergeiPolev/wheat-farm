using UnityEngine;

namespace WheatFarm.Buildings
{
    /// <summary>
    /// Attached to building GameObjects at instantiation time.
    /// Allows raycasting to identify which PlacedBuilding was clicked.
    /// </summary>
    public class BuildingMarker : MonoBehaviour
    {
        public PlacedBuilding Building { get; set; }
    }
}
