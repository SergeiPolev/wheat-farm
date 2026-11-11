using UnityEngine;
using VContainer.Unity;
using WheatFarm.Buildings;
using WheatFarm.Core.Data;
using WheatFarm.Infrastructure.Save;

namespace WheatFarm.Infrastructure
{
    /// <summary>
    /// Places the interactable "point" buildings (Market, Warehouse, Contracts, Well) near
    /// spawn on a brand-new game so the player has somewhere to walk to. Existing saves
    /// keep their own layout (the save restores whatever was placed).
    /// </summary>
    public class EconomyBuildingsBootstrap : IStartable
    {
        private static readonly (string id, Vector2Int coord, Vector2Int cell)[] Layout =
        {
            ("market", new Vector2Int(-2, -2), Vector2Int.zero),
            ("warehouse", new Vector2Int(0, -2), Vector2Int.zero),
            ("contracts", new Vector2Int(2, -2), Vector2Int.zero),
            // Starter Well: refill point for the watering can and paint bucket (tool-charge loop)
            ("well", new Vector2Int(-1, -1), new Vector2Int(10, 4)),
        };

        private readonly IPlacementService _placement;
        private readonly PlaceableDatabase _placeableDb;
        private readonly IFarmSaveManager _saveManager;

        public EconomyBuildingsBootstrap(
            IPlacementService placement,
            PlaceableDatabase placeableDb,
            IFarmSaveManager saveManager)
        {
            _placement = placement;
            _placeableDb = placeableDb;
            _saveManager = saveManager;
        }

        public void Start()
        {
            if (_saveManager.HasSave) return;       // a save will restore its own buildings
            if (_placeableDb == null) return;

            foreach (var (id, coord, cell) in Layout)
            {
                var data = _placeableDb.GetById(id);
                if (data == null) continue;
                _placement.RestorePlace(data, coord, cell.x, cell.y, 0, 0f, 1);
                Debug.Log($"[EconomyBuildings] Placed {id} at {coord}");
            }
        }
    }
}
