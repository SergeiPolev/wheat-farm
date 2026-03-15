using UnityEngine;
using WheatFarm.Core.Data;
using WheatFarm.Farming;

namespace WheatFarm.Player.Tools
{
    /// <summary>
    /// Plants crops/bushes in brush radius, or places trees via TreePlacementService.
    /// Requires a selected PlantData (set via SelectPlant before use).
    /// </summary>
    public class PlanterTool : ITool, IBrushAction
    {
        private readonly IPlantSystem _plantSystem;
        private readonly ITreePlacementService _treePlacement;
        private readonly IBrushService _brush;

        private PlantData _selectedPlant;

        public ToolId Id => ToolId.Planter;
        public bool RequiresResource => true;

        public PlanterTool(IPlantSystem plantSystem, ITreePlacementService treePlacement, IBrushService brush)
        {
            _plantSystem = plantSystem;
            _treePlacement = treePlacement;
            _brush = brush;
        }

        /// <summary>Set which plant to place. Called by UI/inventory.</summary>
        public void SelectPlant(PlantData plant) => _selectedPlant = plant;
        public PlantData SelectedPlant => _selectedPlant;

        public void OnEquip() { }
        public void OnUnequip() { }

        public void UseAtPosition(Vector3 worldPos)
        {
            if (_selectedPlant == null) return;

            // Trees: single placement (no brush), uses TreePlacementService for multi-cell trunk
            if (_selectedPlant.Category == PlantCategory.Tree)
            {
                _treePlacement.Place(_selectedPlant, worldPos);
                return;
            }

            // Crops and bushes: brush-based placement
            _brush.ApplyAtWorldPos(worldPos, this);
        }

        public void Apply(ChunkData chunk, int cellX, int cellY)
        {
            if (_selectedPlant == null) return;
            int idx = chunk.CellIndex(cellX, cellY);
            if (chunk.Cells[idx].HasPlant || chunk.Cells[idx].Occupied) return;

            _plantSystem.Plant(chunk.ChunkCoord, cellX, cellY, _selectedPlant);
        }
    }
}
