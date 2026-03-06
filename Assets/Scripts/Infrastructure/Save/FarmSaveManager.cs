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
        private readonly ITreePlacementService _trees;
        private readonly PlantDatabase _plantDb;
        private readonly BuildingDatabase _buildingDb;

        public bool HasSave => _saveService.HasSave();

        public FarmSaveManager(
            IFarmSaveService saveService,
            IChunkSystem chunkSystem,
            IWalletService wallet,
            IInventoryService inventory,
            IDayNightService dayNight,
            IBuildingService buildings,
            ITreePlacementService trees,
            PlantDatabase plantDb,
            BuildingDatabase buildingDb = null)
        {
            _saveService = saveService;
            _chunkSystem = chunkSystem;
            _wallet = wallet;
            _inventory = inventory;
            _dayNight = dayNight;
            _buildings = buildings;
            _trees = trees;
            _plantDb = plantDb;
            _buildingDb = buildingDb;
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
                        Color = cell.Color,
                        Occupied = cell.Occupied
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
                for (int i = 0; i < count; i++)
                {
                    var saved = chunkSave.SubCells[i];
                    chunk.Cells[i] = new SubCellState
                    {
                        PlantId = string.IsNullOrEmpty(saved.PlantId) ? null : saved.PlantId,
                        Growth = saved.Growth,
                        Watered = saved.Watered,
                        FertilizerMultiplier = saved.FertilizerMultiplier,
                        Color = saved.Color,
                        Occupied = saved.Occupied
                    };

                    // Sync GPU data
                    if (!string.IsNullOrEmpty(saved.PlantId))
                    {
                        var props = chunk.MeshProps[i];
                        props.cropState = new Vector4(1f, saved.Growth, 0f, 0f);
                        props.color = new Vector4(saved.Color.r / 255f, saved.Color.g / 255f,
                            saved.Color.b / 255f, saved.Color.a / 255f);
                        chunk.MeshProps[i] = props;
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
                    // Use Place which handles chunk occupation; wallet is already set so cost check
                    // may fail. For restore, we temporarily add the cost then place.
                    int tempCost = buildingData.Cost;
                    _wallet.Add(tempCost);
                    var placed = _buildings.Place(buildingData, coord);
                    if (placed != null)
                        placed.Level = bSave.Level;
                }
            }

            Debug.Log($"[FarmSaveManager] Restored: {data.Chunks.Count} chunks, " +
                      $"{data.Buildings.Count} buildings, {data.Trees.Count} trees, " +
                      $"{data.Inventory.Count} items, {data.Coins} coins");
        }
    }
}
