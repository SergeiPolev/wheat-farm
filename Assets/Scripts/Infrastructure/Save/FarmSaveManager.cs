using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using WheatFarm.Buildings;
using WheatFarm.Core.Data;
using WheatFarm.DayNight;
using WheatFarm.Economy;
using WheatFarm.Farming;
using WheatFarm.Inventory;

namespace WheatFarm.Infrastructure.Save
{
    /// <summary>
    /// Orchestrates save/load across all game services.
    /// Lives in FarmScope — has access to all services via parent chain.
    /// </summary>
    public interface IFarmSaveManager
    {
        UniTask SaveGame();
        UniTask LoadGame();
        bool HasSave { get; }
    }

    public class FarmSaveManager : IFarmSaveManager
    {
        private readonly IFarmSaveService _saveService;
        private readonly IChunkSystem _chunkSystem;
        private readonly IWalletService _wallet;
        private readonly IInventoryService _inventory;
        private readonly IDayNightService _dayNight;
        private readonly IBuildingService _buildings;
        private readonly IPlacementService _placement;
        private readonly ITreePlacementService _trees;
        private readonly PlantDatabase _plantDb;
        private readonly BuildingDatabase _buildingDb;
        private readonly PlaceableDatabase _placeableDb;

        public bool HasSave => _saveService.HasSave();

        public FarmSaveManager(
            IFarmSaveService saveService,
            IChunkSystem chunkSystem,
            IWalletService wallet,
            IInventoryService inventory,
            IDayNightService dayNight,
            IBuildingService buildings,
            IPlacementService placement,
            ITreePlacementService trees,
            PlantDatabase plantDb,
            BuildingDatabase buildingDb = null,
            PlaceableDatabase placeableDb = null)
        {
            _saveService = saveService;
            _chunkSystem = chunkSystem;
            _wallet = wallet;
            _inventory = inventory;
            _dayNight = dayNight;
            _buildings = buildings;
            _placement = placement;
            _trees = trees;
            _plantDb = plantDb;
            _buildingDb = buildingDb;
            _placeableDb = placeableDb;
        }

        public async UniTask SaveGame()
        {
            var data = CollectSaveData();
            await _saveService.Save(data);
        }

        public async UniTask LoadGame()
        {
            var data = await _saveService.Load();
            RestoreFromData(data);
        }

        // ── Collect ──────────────────────────────────────────────

        private FarmSaveData CollectSaveData()
        {
            var data = new FarmSaveData
            {
                Coins = _wallet.Coins.CurrentValue,
                DayNightTime = _dayNight.TimeNormalized.CurrentValue
            };

            // Chunks + sub-cells
            foreach (var chunk in _chunkSystem.GetAllUnlockedChunks())
            {
                var chunkSave = new ChunkSaveData
                {
                    CoordX = chunk.ChunkCoord.x,
                    CoordY = chunk.ChunkCoord.y,
                    Unlocked = chunk.Unlocked,
                    SubCells = new SubCellSaveData[chunk.CellCount]
                };

                for (int i = 0; i < chunk.CellCount; i++)
                {
                    var cell = chunk.Cells[i];
                    chunkSave.SubCells[i] = new SubCellSaveData
                    {
                        PlantId = cell.PlantId ?? "",
                        Growth = cell.Growth,
                        Watered = cell.Watered,
                        FertilizerMultiplier = cell.FertilizerMultiplier,
                        Color = (Color32)cell.Color, // explicit Color→Color32 (0..1 → 0..255)
                        Occupied = cell.Occupied,
                        BaseScale = cell.BaseScale,
                        RotationY = cell.RotationY,
                        GroundState = (int)cell.GroundState
                    };
                }

                data.Chunks.Add(chunkSave);
            }

            // Buildings
            foreach (var b in _buildings.Buildings)
            {
                data.Buildings.Add(new PlacedBuildingSaveData
                {
                    BuildingId = b.Data.BuildingId,
                    ChunkCoordX = b.ChunkCoord.x,
                    ChunkCoordY = b.ChunkCoord.y,
                    Level = b.Level
                });
            }

            // Placed objects (buildings/decor via PlacementService)
            foreach (var obj in _placement.PlacedObjects)
            {
                data.PlacedObjects.Add(new PlacedObjectSaveData
                {
                    PlaceableId = obj.Data.PlaceableId,
                    ChunkCoordX = obj.ChunkCoord.x,
                    ChunkCoordY = obj.ChunkCoord.y,
                    CellX = obj.CellX,
                    CellY = obj.CellY,
                    RotationY = obj.RotationY,
                    Level = obj.Level
                });
            }

            // Trees
            foreach (var t in _trees.PlacedTrees)
            {
                data.Trees.Add(new TreeSaveData
                {
                    PlantId = t.Data.PlantId,
                    PosX = t.WorldPosition.x,
                    PosY = t.WorldPosition.y,
                    PosZ = t.WorldPosition.z,
                    Growth = 0f, // TODO: read from PlantSystem when tree growth tracking is added
                    Color = Color.white
                });
            }

            // Inventory
            foreach (var item in _inventory.Items)
            {
                data.Inventory.Add(new InventoryItemSaveData
                {
                    ItemId = item.ItemId,
                    Type = (int)item.Type,
                    Amount = item.Amount
                });
            }

            Debug.Log($"[FarmSaveManager] Collected: {data.Chunks.Count} chunks, " +
                      $"{data.Buildings.Count} buildings, {data.Trees.Count} trees, " +
                      $"{data.Inventory.Count} items, {data.Coins} coins");
            return data;
        }

        // ── Restore ─────────────────────────────────────────────

        private void RestoreFromData(FarmSaveData data)
        {
            if (data == null) return;

            // Wallet
            _wallet.SetCoins(data.Coins);

            // Day/Night time
            _dayNight.SetTime(data.DayNightTime);

            // Inventory
            _inventory.Clear();
            foreach (var item in data.Inventory)
            {
                _inventory.TryAdd(new InventoryItem(item.ItemId, (ItemType)item.Type, item.Amount));
            }

            // Chunks + sub-cells
            foreach (var chunkSave in data.Chunks)
            {
                var coord = new Vector2Int(chunkSave.CoordX, chunkSave.CoordY);
                _chunkSystem.TryUnlockChunk(coord);
                var chunk = _chunkSystem.GetChunk(coord);
                if (chunk == null) continue;

                int count = Mathf.Min(chunkSave.SubCells?.Length ?? 0, chunk.CellCount);
                int res = _chunkSystem.SubCellResolution;
                for (int i = 0; i < count; i++)
                {
                    var saved = chunkSave.SubCells[i];
                    // Color32→Color implicit conversion (0..255 → 0..1) — no manual /255 needed
                    Color restoredColor = saved.Color;
                    chunk.Cells[i] = new SubCellState
                    {
                        PlantId = string.IsNullOrEmpty(saved.PlantId) ? null : saved.PlantId,
                        Growth = saved.Growth,
                        Watered = saved.Watered,
                        FertilizerMultiplier = saved.FertilizerMultiplier,
                        Color = restoredColor,
                        Occupied = saved.Occupied,
                        BaseScale = saved.BaseScale,
                        RotationY = saved.RotationY,
                        GroundState = (GroundState)saved.GroundState
                    };

                    // Sync GPU data
                    if (!string.IsNullOrEmpty(saved.PlantId))
                    {
                        ref var props = ref chunk.MeshProps[i];

                        // Rebuild TRS matrix: position relative to chunk bounds center (shader requirement)
                        int cellX = i % res;
                        int cellY = i / res;
                        Vector3 worldPos = _chunkSystem.CellToWorld(coord, cellX, cellY);
                        Vector3 relPos = worldPos - _chunkSystem.ChunkBoundsCenter(coord);

                        float growthFraction = Mathf.InverseLerp(0f, 1f, saved.Growth);
                        float visualScale = saved.BaseScale * Mathf.Lerp(0.3f, 1f, growthFraction);
                        props.m = Matrix4x4.TRS(
                            relPos,
                            Quaternion.Euler(0, saved.RotationY, 0),
                            new Vector3(visualScale, visualScale, visualScale));

                        var plantData = _plantDb?.GetById(saved.PlantId);
                        int meshId = plantData?.MeshId ?? 1;
                        props.cropState = new Vector4(meshId, saved.Growth, saved.GroundState, 0f);
                        props.color = new Vector4(restoredColor.r, restoredColor.g,
                            restoredColor.b, restoredColor.a);
                    }
                }
                chunk.Dirty = true;
            }

            // Trees (restore via PlantDatabase lookup)
            foreach (var treeSave in data.Trees)
            {
                var plantData = _plantDb?.GetById(treeSave.PlantId);
                if (plantData == null) continue;

                var worldPos = new Vector3(treeSave.PosX, treeSave.PosY, treeSave.PosZ);
                _trees.Place(plantData, worldPos);
            }

            // Buildings (restore via BuildingDatabase lookup)
            if (_buildingDb != null)
            {
                foreach (var bSave in data.Buildings)
                {
                    var buildingData = _buildingDb.GetById(bSave.BuildingId);
                    if (buildingData == null) continue;

                    var coord = new Vector2Int(bSave.ChunkCoordX, bSave.ChunkCoordY);
                    // Temporarily add cost so Place()'s wallet check passes, then undo if it fails.
                    int tempCost = buildingData.Cost;
                    _wallet.Add(tempCost);
                    var placed = _buildings.Place(buildingData, coord);
                    if (placed != null)
                    {
                        placed.Level = bSave.Level;
                    }
                    else
                    {
                        // Place failed — undo the temporary coins so wallet stays correct
                        _wallet.TrySpend(tempCost);
                    }
                }
            }

            // Placed objects (restore via PlaceableDatabase lookup)
            if (_placeableDb != null)
            {
                foreach (var poSave in data.PlacedObjects)
                {
                    var placeableData = _placeableDb.GetById(poSave.PlaceableId);
                    if (placeableData == null) continue;

                    var coord = new Vector2Int(poSave.ChunkCoordX, poSave.ChunkCoordY);
                    ((PlacementService)_placement).RestorePlace(
                        placeableData, coord, poSave.CellX, poSave.CellY,
                        poSave.RotationY, poSave.Level);
                }
            }

            Debug.Log($"[FarmSaveManager] Restored: {data.Chunks.Count} chunks, " +
                      $"{data.Buildings.Count} buildings, {data.PlacedObjects.Count} placed objects, " +
                      $"{data.Trees.Count} trees, {data.Inventory.Count} items, {data.Coins} coins");
        }
    }
}
