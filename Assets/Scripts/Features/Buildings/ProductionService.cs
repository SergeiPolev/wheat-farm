using System.Collections.Generic;
using System.Linq;
using R3;
using UnityEngine;
using VContainer.Unity;
using WheatFarm.Core.Data;
using WheatFarm.Inventory;

namespace WheatFarm.Buildings
{
    public class ProductionSlot
    {
        public RecipeData Recipe;
        public float TimeRemaining;
        public float TotalTime;
        public bool AutoRepeat;
        public float Progress => TotalTime > 0 ? 1f - (TimeRemaining / TotalTime) : 1f;
        public bool IsComplete => TimeRemaining <= 0f;
    }

    public interface IProductionService
    {
        Subject<RecipeData> OnProductionCompleted { get; }
        Subject<PlacedObject> OnSlotsChanged { get; }
        bool TryStartProduction(PlacedObject building, RecipeData recipe, bool autoRepeat = true);
        bool TryStopProduction(PlacedObject building, int slotIndex);
        List<ProductionSlot> GetSlots(PlacedObject building);
        int GetMaxSlots(PlacedObject building);
        bool IsProducing(PlacedObject building);
        void SetAutoRepeat(PlacedObject building, bool enabled);
    }

    public class ProductionService : IProductionService, ITickable
    {
        private readonly IInventoryService _inventory;
        private readonly Dictionary<PlacedObject, List<ProductionSlot>> _active = new();

        public Subject<RecipeData> OnProductionCompleted { get; } = new();
        public Subject<PlacedObject> OnSlotsChanged { get; } = new();

        public ProductionService(IInventoryService inventory)
        {
            _inventory = inventory;
        }

        public int GetMaxSlots(PlacedObject building)
        {
            if (building?.Data == null) return 0;
            return Mathf.Clamp(building.Level, 1, building.Data.MaxLevel);
        }

        public bool IsProducing(PlacedObject building)
        {
            return _active.TryGetValue(building, out var slots) && slots.Count > 0;
        }

        public bool TryStartProduction(PlacedObject building, RecipeData recipe, bool autoRepeat = true)
        {
            if (recipe == null || building?.Data == null) return false;

            // Check slot limit
            int maxSlots = GetMaxSlots(building);
            if (_active.TryGetValue(building, out var existingSlots) && existingSlots.Count >= maxSlots)
                return false;

            // Check inputs in inventory
            foreach (var input in recipe.Inputs)
            {
                if (!_inventory.HasItem(input.ItemId, input.Amount)) return false;
            }

            // Consume inputs
            foreach (var input in recipe.Inputs)
            {
                _inventory.TryConsume(input.ItemId, input.Amount);
            }

            if (!_active.TryGetValue(building, out var slots))
            {
                slots = new List<ProductionSlot>();
                _active[building] = slots;
            }

            slots.Add(new ProductionSlot
            {
                Recipe = recipe,
                TimeRemaining = recipe.ProcessingTime,
                TotalTime = recipe.ProcessingTime,
                AutoRepeat = autoRepeat
            });

            EnableSmoke(building, true);
            OnSlotsChanged.OnNext(building);
            return true;
        }

        public bool TryStopProduction(PlacedObject building, int slotIndex)
        {
            if (!_active.TryGetValue(building, out var slots)) return false;
            if (slotIndex < 0 || slotIndex >= slots.Count) return false;

            var slot = slots[slotIndex];
            // Refund ingredients
            foreach (var input in slot.Recipe.Inputs)
            {
                _inventory.TryAdd(new InventoryItem(input.ItemId, ItemType.Harvest, input.Amount));
            }

            slots.RemoveAt(slotIndex);
            if (slots.Count == 0)
            {
                _active.Remove(building);
                EnableSmoke(building, false);
            }
            OnSlotsChanged.OnNext(building);
            return true;
        }

        public List<ProductionSlot> GetSlots(PlacedObject building)
        {
            return _active.GetValueOrDefault(building);
        }

        public void SetAutoRepeat(PlacedObject building, bool enabled)
        {
            if (!_active.TryGetValue(building, out var slots)) return;
            foreach (var slot in slots) slot.AutoRepeat = enabled;
        }

        public void Tick()
        {
            float dt = Time.deltaTime;
            var completed = new List<(PlacedObject building, ProductionSlot slot)>();

            foreach (var (building, slots) in _active)
            {
                for (int i = slots.Count - 1; i >= 0; i--)
                {
                    var slot = slots[i];
                    slot.TimeRemaining -= dt;

                    if (slot.IsComplete)
                    {
                        var output = slot.Recipe.Output;
                        _inventory.TryAdd(new InventoryItem(output.ItemId, ItemType.Product, output.Amount));
                        OnProductionCompleted.OnNext(slot.Recipe);
                        completed.Add((building, slot));
                        slots.RemoveAt(i);
                    }
                }
            }

            // Auto-repeat for completed slots
            foreach (var (building, slot) in completed)
            {
                if (slot.AutoRepeat)
                {
                    TryStartProduction(building, slot.Recipe, true);
                }
                OnSlotsChanged.OnNext(building);
            }

            // Clean up empty entries and disable smoke
            var emptyKeys = _active.Where(kv => kv.Value.Count == 0).Select(kv => kv.Key).ToList();
            foreach (var key in emptyKeys)
            {
                _active.Remove(key);
                EnableSmoke(key, false);
            }
        }

        // --- Visual feedback ---

        private void EnableSmoke(PlacedObject building, bool enabled)
        {
            if (building?.Instance == null) return;
            var smoke = building.Instance.transform.Find("SmokeEffect");
            if (smoke == null) return;

            var ps = smoke.GetComponent<ParticleSystem>();
            if (ps == null) return;

            if (enabled && !ps.isPlaying) ps.Play();
            else if (!enabled && ps.isPlaying) ps.Stop();
        }

        // --- Save/Load support ---

        public List<ProductionSlotSaveData> GetSaveData()
        {
            var result = new List<ProductionSlotSaveData>();
            foreach (var (building, slots) in _active)
            {
                foreach (var slot in slots)
                {
                    result.Add(new ProductionSlotSaveData
                    {
                        PlaceableId = building.Data.PlaceableId,
                        ChunkCoord = building.ChunkCoord,
                        RecipeId = slot.Recipe.RecipeId,
                        TimeRemaining = slot.TimeRemaining,
                        AutoRepeat = slot.AutoRepeat
                    });
                }
            }
            return result;
        }

        public void RestoreSlot(PlacedObject building, RecipeData recipe, float timeRemaining, bool autoRepeat)
        {
            if (building == null || recipe == null) return;

            if (!_active.TryGetValue(building, out var slots))
            {
                slots = new List<ProductionSlot>();
                _active[building] = slots;
            }

            slots.Add(new ProductionSlot
            {
                Recipe = recipe,
                TimeRemaining = timeRemaining,
                TotalTime = recipe.ProcessingTime,
                AutoRepeat = autoRepeat
            });

            EnableSmoke(building, true);
        }
    }

    [System.Serializable]
    public struct ProductionSlotSaveData
    {
        public string PlaceableId;
        public Vector2Int ChunkCoord;
        public string RecipeId;
        public float TimeRemaining;
        public bool AutoRepeat;
    }
}
