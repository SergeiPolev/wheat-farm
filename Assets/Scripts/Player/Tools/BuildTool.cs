using UnityEngine;
using WheatFarm.Buildings;
using WheatFarm.Core.Data;
using WheatFarm.Farming;

namespace WheatFarm.Player.Tools
{
    /// <summary>
    /// Places buildings on the farm. Select a building with SelectBuilding(),
    /// then click to place. B key cycles through available buildings.
    /// </summary>
    public class BuildTool : ITool
    {
        private readonly IBuildingService _buildingService;
        private readonly IChunkSystem _chunkSystem;
        private readonly BuildingDatabase _buildingDb;

        private BuildingData _selectedBuilding;
        private int _selectedIndex;

        public ToolId Id => ToolId.Build;
        public bool RequiresResource => true;

        public BuildTool(IBuildingService buildingService, IChunkSystem chunkSystem, BuildingDatabase buildingDb)
        {
            _buildingService = buildingService;
            _chunkSystem = chunkSystem;
            _buildingDb = buildingDb;

            // Auto-select first building
            if (_buildingDb != null && _buildingDb.Buildings.Length > 0)
                _selectedBuilding = _buildingDb.Buildings[0];
        }

        public void SelectBuilding(BuildingData data) => _selectedBuilding = data;
        public BuildingData SelectedBuilding => _selectedBuilding;

        public void CycleBuilding()
        {
            if (_buildingDb == null || _buildingDb.Buildings.Length == 0) return;
            _selectedIndex = (_selectedIndex + 1) % _buildingDb.Buildings.Length;
            _selectedBuilding = _buildingDb.Buildings[_selectedIndex];
            Debug.Log($"[Build] Selected: {_selectedBuilding.DisplayName} ({_selectedBuilding.Cost} coins)");
        }

        public void OnEquip()
        {
            if (_selectedBuilding != null)
                Debug.Log($"[Build] Equipped. Current: {_selectedBuilding.DisplayName}. Press B to cycle.");
        }

        public void OnUnequip() { }

        public void UseAtPosition(Vector3 worldPos)
        {
            if (_selectedBuilding == null) return;

            var chunkCoord = _chunkSystem.WorldToChunkCoord(worldPos);
            var result = _buildingService.Place(_selectedBuilding, chunkCoord);

            if (result != null)
                Debug.Log($"[Build] Placed {_selectedBuilding.DisplayName} at chunk {chunkCoord}");
            else
                Debug.Log($"[Build] Cannot place {_selectedBuilding.DisplayName} at chunk {chunkCoord}");
        }
    }
}
