using System.Collections.Generic;
using ObservableCollections;
using UnityEngine;
using WheatFarm.Core.Data;
using WheatFarm.Economy;
using WheatFarm.Farming;

namespace WheatFarm.Buildings
{
    /// <summary>Runtime data for a placed object (building, decor). Paths are tracked via cell GroundState only.</summary>
    public class PlacedObject
    {
        public PlaceableData Data;
        public Vector2Int ChunkCoord;
        public int CellX, CellY;
        public float RotationY;
        public int Level = 1;
        public GameObject Instance;
    }

    public interface IPlacementService
    {
        ObservableList<PlacedObject> PlacedObjects { get; }
        PlacedObject Place(PlaceableData data, Vector3 worldPos, float rotationY = 0f);
        bool CanPlace(PlaceableData data, Vector3 worldPos);
        bool Remove(PlacedObject obj);
    }

    public class PlacementService : IPlacementService
    {
        private readonly IChunkSystem _chunkSystem;
        private readonly IWalletService _wallet;
        private readonly HashSet<Vector2Int> _occupiedChunks = new();

        public ObservableList<PlacedObject> PlacedObjects { get; } = new();

        public PlacementService(IChunkSystem chunkSystem, IWalletService wallet)
        {
            _chunkSystem = chunkSystem;
            _wallet = wallet;
        }

        public bool CanPlace(PlaceableData data, Vector3 worldPos)
        {
            if (data == null) return false;
            if (data.Category == PlaceableCategory.Path) return false; // paths use brush, not this method

            if (data.Level == PlacementLevel.Chunk)
                return CanPlaceChunkLevel(data, worldPos);
            else
                return CanPlaceCellLevel(data, worldPos);
        }

        public PlacedObject Place(PlaceableData data, Vector3 worldPos, float rotationY = 0f)
        {
            if (data == null) return null;
            if (!CanPlace(data, worldPos)) return null;
            if (!_wallet.TrySpend(data.Cost)) return null;

            if (data.Level == PlacementLevel.Chunk)
                return PlaceChunkLevel(data, worldPos, rotationY);
            else
                return PlaceCellLevel(data, worldPos, rotationY);
        }

        public bool Remove(PlacedObject obj)
        {
            if (obj == null) return false;

            if (obj.Data.Level == PlacementLevel.Chunk)
                FreeChunkLevel(obj);
            else
                FreeCellLevel(obj);

            if (obj.Instance != null)
                Object.Destroy(obj.Instance);

            PlacedObjects.Remove(obj);

            // Partial refund (50%)
            int refund = Mathf.FloorToInt(obj.Data.Cost * 0.5f);
            if (refund > 0) _wallet.Add(refund);

            return true;
        }

        // --- Chunk-level placement (buildings) ---

        private bool CanPlaceChunkLevel(PlaceableData data, Vector3 worldPos)
        {
            var chunkCoord = _chunkSystem.WorldToChunkCoord(worldPos);
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

        private PlacedObject PlaceChunkLevel(PlaceableData data, Vector3 worldPos, float rotationY)
        {
            var chunkCoord = _chunkSystem.WorldToChunkCoord(worldPos);

            // Mark chunks occupied
            for (int dx = 0; dx < data.GridSize.x; dx++)
            {
                for (int dy = 0; dy < data.GridSize.y; dy++)
                {
                    var coord = chunkCoord + new Vector2Int(dx, dy);
                    _occupiedChunks.Add(coord);
                }
            }

            // Mark all sub-cells as occupied
            MarkChunkSubCellsOccupied(data, chunkCoord, true);

            var placed = new PlacedObject
            {
                Data = data,
                ChunkCoord = chunkCoord,
                CellX = 0,
                CellY = 0,
                RotationY = rotationY,
                Level = 1
            };

            if (data.Prefab != null)
            {
                var spawnPos = _chunkSystem.CellToWorld(chunkCoord, 0, 0);
                placed.Instance = Object.Instantiate(data.Prefab, spawnPos, Quaternion.Euler(0, rotationY, 0));

                if (data.Interactable)
                {
                    var marker = placed.Instance.AddComponent<BuildingMarker>();
                    marker.PlacedObject = placed;
                }
            }

            PlacedObjects.Add(placed);
            return placed;
        }

        private void FreeChunkLevel(PlacedObject obj)
        {
            for (int dx = 0; dx < obj.Data.GridSize.x; dx++)
            {
                for (int dy = 0; dy < obj.Data.GridSize.y; dy++)
                {
                    _occupiedChunks.Remove(obj.ChunkCoord + new Vector2Int(dx, dy));
                }
            }
            MarkChunkSubCellsOccupied(obj.Data, obj.ChunkCoord, false);
        }

        private void MarkChunkSubCellsOccupied(PlaceableData data, Vector2Int chunkCoord, bool occupied)
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

        // --- Cell-level placement (decor) ---

        private bool CanPlaceCellLevel(PlaceableData data, Vector3 worldPos)
        {
            var (chunkCoord, cellX, cellY) = _chunkSystem.WorldToCell(worldPos);
            var chunk = _chunkSystem.GetChunk(chunkCoord);
            if (chunk == null || !chunk.Unlocked) return false;

            int res = _chunkSystem.SubCellResolution;
            for (int dx = 0; dx < data.GridSize.x; dx++)
            {
                for (int dy = 0; dy < data.GridSize.y; dy++)
                {
                    int cx = cellX + dx;
                    int cy = cellY + dy;
                    if (cx < 0 || cx >= res || cy < 0 || cy >= res) return false;
                    int idx = cy * res + cx;
                    ref var cell = ref chunk.Cells[idx];
                    if (cell.Occupied || cell.HasPlant) return false;
                }
            }
            return true;
        }

        private PlacedObject PlaceCellLevel(PlaceableData data, Vector3 worldPos, float rotationY)
        {
            var (chunkCoord, cellX, cellY) = _chunkSystem.WorldToCell(worldPos);
            var chunk = _chunkSystem.GetChunk(chunkCoord);

            int res = _chunkSystem.SubCellResolution;
            for (int dx = 0; dx < data.GridSize.x; dx++)
            {
                for (int dy = 0; dy < data.GridSize.y; dy++)
                {
                    int cx = cellX + dx;
                    int cy = cellY + dy;
                    int idx = cy * res + cx;
                    chunk.Cells[idx].Occupied = true;
                }
            }
            chunk.Dirty = true;

            var placed = new PlacedObject
            {
                Data = data,
                ChunkCoord = chunkCoord,
                CellX = cellX,
                CellY = cellY,
                RotationY = rotationY,
                Level = 1
            };

            if (data.Prefab != null)
            {
                var spawnPos = _chunkSystem.CellToWorld(chunkCoord, cellX, cellY);
                placed.Instance = Object.Instantiate(data.Prefab, spawnPos, Quaternion.Euler(0, rotationY, 0));
            }

            PlacedObjects.Add(placed);
            return placed;
        }

        private void FreeCellLevel(PlacedObject obj)
        {
            var chunk = _chunkSystem.GetChunk(obj.ChunkCoord);
            if (chunk == null) return;

            int res = _chunkSystem.SubCellResolution;
            for (int dx = 0; dx < obj.Data.GridSize.x; dx++)
            {
                for (int dy = 0; dy < obj.Data.GridSize.y; dy++)
                {
                    int cx = obj.CellX + dx;
                    int cy = obj.CellY + dy;
                    if (cx >= 0 && cx < res && cy >= 0 && cy < res)
                    {
                        int idx = cy * res + cx;
                        chunk.Cells[idx].Occupied = false;
                    }
                }
            }
            chunk.Dirty = true;
        }

        /// <summary>
        /// Restore a placed object during save-load without spending coins or validation.
        /// </summary>
        public PlacedObject RestorePlace(PlaceableData data, Vector2Int chunkCoord, int cellX, int cellY,
            float rotationY, int level)
        {
            if (data == null) return null;

            var placed = new PlacedObject
            {
                Data = data,
                ChunkCoord = chunkCoord,
                CellX = cellX,
                CellY = cellY,
                RotationY = rotationY,
                Level = level
            };

            // Mark cells
            if (data.Level == PlacementLevel.Chunk)
            {
                for (int dx = 0; dx < data.GridSize.x; dx++)
                    for (int dy = 0; dy < data.GridSize.y; dy++)
                        _occupiedChunks.Add(chunkCoord + new Vector2Int(dx, dy));
                MarkChunkSubCellsOccupied(data, chunkCoord, true);
            }
            else
            {
                var chunk = _chunkSystem.GetChunk(chunkCoord);
                if (chunk != null)
                {
                    int res = _chunkSystem.SubCellResolution;
                    for (int dx = 0; dx < data.GridSize.x; dx++)
                    {
                        for (int dy = 0; dy < data.GridSize.y; dy++)
                        {
                            int cx = cellX + dx;
                            int cy = cellY + dy;
                            if (cx >= 0 && cx < res && cy >= 0 && cy < res)
                                chunk.Cells[cy * res + cx].Occupied = true;
                        }
                    }
                    chunk.Dirty = true;
                }
            }

            // Instantiate prefab
            if (data.Prefab != null)
            {
                Vector3 spawnPos;
                if (data.Level == PlacementLevel.Chunk)
                    spawnPos = _chunkSystem.CellToWorld(chunkCoord, 0, 0);
                else
                    spawnPos = _chunkSystem.CellToWorld(chunkCoord, cellX, cellY);

                placed.Instance = Object.Instantiate(data.Prefab, spawnPos, Quaternion.Euler(0, rotationY, 0));

                if (data.Interactable)
                {
                    var marker = placed.Instance.AddComponent<BuildingMarker>();
                    marker.PlacedObject = placed;
                }
            }

            PlacedObjects.Add(placed);
            return placed;
        }
    }
}
