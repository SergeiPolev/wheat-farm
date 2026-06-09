using UnityEngine;
using VContainer.Unity;
using WheatFarm.Buildings;
using WheatFarm.Core.Data;
using WheatFarm.Infrastructure.Save;

namespace WheatFarm.Infrastructure
{
    /// <summary>
    /// Places the interactable "point" buildings (Market, Warehouse, Contracts) near
    /// spawn on a brand-new game so the player has somewhere to walk to. Existing saves
    /// keep their own layout (the save restores whatever was placed).
    /// </summary>
    public class EconomyBuildingsBootstrap : IStartable
    {
        private static readonly (string id, Vector2Int coord)[] Layout =
        {
            ("market", new Vector2Int(-2, -2)),
            ("warehouse", new Vector2Int(0, -2)),
            ("contracts", new Vector2Int(2, -2)),
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

            foreach (var (id, coord) in Layout)
            {
                var data = _placeableDb.GetById(id);
                if (data == null) continue;
                _placement.RestorePlace(data, coord, 0, 0, 0f, 1);
                Debug.Log($"[EconomyBuildings] Placed {id} at {coord}");
            }
        }
    }
}
