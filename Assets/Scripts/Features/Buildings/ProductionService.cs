using System.Collections.Generic;
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
        public float Progress => TotalTime > 0 ? 1f - (TimeRemaining / TotalTime) : 1f;
        public bool IsComplete => TimeRemaining <= 0f;
    }

    public interface IProductionService
    {
        Subject<RecipeData> OnProductionCompleted { get; }
        bool TryStartProduction(PlacedBuilding building, RecipeData recipe);
        List<ProductionSlot> GetSlots(PlacedBuilding building);
    }

    public class ProductionService : IProductionService, ITickable
    {
        private readonly IInventoryService _inventory;
        private readonly Dictionary<PlacedBuilding, List<ProductionSlot>> _active = new();

        public Subject<RecipeData> OnProductionCompleted { get; } = new();

        public ProductionService(IInventoryService inventory)
        {
            _inventory = inventory;
        }

        public bool TryStartProduction(PlacedBuilding building, RecipeData recipe)
        {
            if (recipe == null || building == null) return false;

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
                TotalTime = recipe.ProcessingTime
            });
            return true;
        }

        public List<ProductionSlot> GetSlots(PlacedBuilding building)
        {
            return _active.GetValueOrDefault(building);
        }

        public void Tick()
        {
            float dt = Time.deltaTime;

            foreach (var (building, slots) in _active)
            {
                for (int i = slots.Count - 1; i >= 0; i--)
                {
                    var slot = slots[i];
                    slot.TimeRemaining -= dt;

                    if (slot.IsComplete)
                    {
                        // Output product to inventory
                        var output = slot.Recipe.Output;
                        _inventory.TryAdd(new InventoryItem(output.ItemId, ItemType.Product, output.Amount));
                        OnProductionCompleted.OnNext(slot.Recipe);
                        slots.RemoveAt(i);
                    }
                }
            }
        }
    }
}
