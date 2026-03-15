using System.Collections.Generic;
using ObservableCollections;
using UnityEngine;
using WheatFarm.Core.Data;
using WheatFarm.Economy;
using WheatFarm.Farming;

namespace WheatFarm.Buildings
{
    public class PlacedBuilding
    {
        public BuildingData Data;
        public Vector2Int ChunkCoord;
        public int Level = 1;
        public GameObject Instance;
    }

    public interface IBuildingService
    {
        ObservableList<PlacedBuilding> Buildings { get; }
        bool CanPlace(BuildingData data, Vector2Int chunkCoord);
        PlacedBuilding Place(BuildingData data, Vector2Int chunkCoord);
        void Move(PlacedBuilding building, Vector2Int newCoord);
        void Remove(PlacedBuilding building);
        void Upgrade(PlacedBuilding building);
    }

    public class BuildingService : IBuildingService
    {
        private readonly IChunkSystem _chunkSystem;
        private readonly IWalletService _wallet;
        private readonly HashSet<Vector2Int> _occupiedChunks = new();

        public ObservableList<PlacedBuilding> Buildings { get; } = new();

        public BuildingService(IChunkSystem chunkSystem, IWalletService wallet)
        {
            _chunkSystem = chunkSystem;
            _wallet = wallet;
        }

        public bool CanPlace(BuildingData data, Vector2Int chunkCoord)
        {
            if (data == null) return false;

            // Check all chunks the building would occupy
            for (int dx = 0; dx < data.GridSize.x; dx++)
            {
                for (int dy = 0; dy < data.GridSize.y; dy++)
                {
                    var coord = chunkCoord + new Vector2Int(dx, dy);
                    var chunk = _chunkSystem.GetChunk(coord);
                    if (chunk == null || !chunk.Unlocked) return false;
                    if (_occupiedChunks.Contains(coord)) return false;
                }
            }
            return true;
        }

        public PlacedBuilding Place(BuildingData data, Vector2Int chunkCoord)
        {
            if (!CanPlace(data, chunkCoord)) return null;
            if (!_wallet.TrySpend(data.Cost)) return null;

            // Mark chunks as occupied
            for (int dx = 0; dx < data.GridSize.x; dx++)
            {
                for (int dy = 0; dy < data.GridSize.y; dy++)
                {
                    _occupiedChunks.Add(chunkCoord + new Vector2Int(dx, dy));
                }
            }

            // Mark sub-cells as occupied
            MarkSubCellsOccupied(data, chunkCoord, true);

            var building = new PlacedBuilding
            {
                Data = data,
                ChunkCoord = chunkCoord,
                Level = 1
            };

            // Instantiate prefab if available
            if (data.Prefab != null)
            {
                var worldPos = _chunkSystem.CellToWorld(chunkCoord, 0, 0);
                building.Instance = Object.Instantiate(data.Prefab, worldPos, Quaternion.identity);

                // Tag instance for raycast identification
                var marker = building.Instance.AddComponent<BuildingMarker>();
                marker.Building = building;
            }

            Buildings.Add(building);
            return building;
        }

        public void Move(PlacedBuilding building, Vector2Int newCoord)
        {
            if (!CanPlace(building.Data, newCoord)) return;

            // Free old chunks
            FreeChunks(building);
            MarkSubCellsOccupied(building.Data, building.ChunkCoord, false);

            // Claim new chunks
            building.ChunkCoord = newCoord;
            for (int dx = 0; dx < building.Data.GridSize.x; dx++)
            {
                for (int dy = 0; dy < building.Data.GridSize.y; dy++)
                {
                    _occupiedChunks.Add(newCoord + new Vector2Int(dx, dy));
                }
            }
            MarkSubCellsOccupied(building.Data, newCoord, true);

            // Move instance
            if (building.Instance != null)
            {
                var worldPos = _chunkSystem.CellToWorld(newCoord, 0, 0);
                building.Instance.transform.position = worldPos;
            }
        }

        public void Remove(PlacedBuilding building)
        {
            FreeChunks(building);
            MarkSubCellsOccupied(building.Data, building.ChunkCoord, false);

            if (building.Instance != null)
                Object.Destroy(building.Instance);

            Buildings.Remove(building);
        }

        public void Upgrade(PlacedBuilding building)
        {
            if (building.Level >= building.Data.MaxLevel) return;
            // Cost for upgrade could be level-based; for now, use base cost
            if (!_wallet.TrySpend(building.Data.Cost)) return;
            building.Level++;
        }

        private void FreeChunks(PlacedBuilding building)
        {
            for (int dx = 0; dx < building.Data.GridSize.x; dx++)
            {
                for (int dy = 0; dy < building.Data.GridSize.y; dy++)
                {
                    _occupiedChunks.Remove(building.ChunkCoord + new Vector2Int(dx, dy));
                }
            }
        }

        private void MarkSubCellsOccupied(BuildingData data, Vector2Int chunkCoord, bool occupied)
        {
            for (int dx = 0; dx < data.GridSize.x; dx++)
            {
                for (int dy = 0; dy < data.GridSize.y; dy++)
                {
                    var coord = chunkCoord + new Vector2Int(dx, dy);
                    var chunk = _chunkSystem.GetChunk(coord);
                    if (chunk == null) continue;

                    for (int i = 0; i < chunk.CellCount; i++)
                    {
                        chunk.Cells[i].Occupied = occupied;
                    }
                    chunk.Dirty = true;
                }
            }
        }
    }
}
