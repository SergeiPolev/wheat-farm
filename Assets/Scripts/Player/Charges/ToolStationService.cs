using System;
using System.Collections.Generic;
using R3;
using UnityEngine;
using VContainer.Unity;
using WheatFarm.Buildings;
using WheatFarm.Core.Data;
using WheatFarm.Player.Tools;

namespace WheatFarm.Player.Charges
{
    public enum StationKind { None, Well, Warehouse }

    public interface IToolStationService
    {
        /// <summary>Station the player is currently standing at (None when away).</summary>
        ReadOnlyReactiveProperty<StationKind> CurrentStation { get; }
        /// <summary>Which station refills a container (Well may fall back to Warehouse if no Well is placed).</summary>
        StationKind StationFor(ChargeKind kind);
        /// <summary>World position above the nearest station of this kind (for markers). False if none placed.</summary>
        bool TryGetStationAnchor(StationKind kind, Vector3 from, out Vector3 anchor);
    }

    /// <summary>
    /// Proximity refill loop. Standing next to the Well tops up the watering can and paint bucket;
    /// standing at the Warehouse unloads the harvest basket and loads the seed bag with the selected
    /// plant's seeds from inventory. Transfers are paced (FullRefillSeconds empty→full) so the meter
    /// visibly fills instead of snapping.
    /// </summary>
    public class ToolStationService : IToolStationService, ITickable, IDisposable
    {
        private readonly IToolChargeService _charges;
        private readonly IPlacementService _placement;
        private readonly PlacementTool _placementTool;
        private readonly ReactiveProperty<StationKind> _current = new(StationKind.None);
        private readonly Dictionary<GameObject, Bounds> _boundsCache = new();

        private Transform _player;
        private float _waterAcc, _dyeAcc, _seedAcc, _basketAcc;
        private bool _starterSeedsLoaded;

        public ReadOnlyReactiveProperty<StationKind> CurrentStation => _current;

        public ToolStationService(IToolChargeService charges, IPlacementService placement, PlacementTool placementTool = null)
        {
            _charges = charges;
            _placement = placement;
            _placementTool = placementTool;
        }

        public StationKind StationFor(ChargeKind kind) => kind switch
        {
            ChargeKind.Water or ChargeKind.Dye => HasAny(ToolChargeConfig.WellId) ? StationKind.Well : StationKind.Warehouse,
            _ => StationKind.Warehouse
        };

        public void Tick()
        {
            if (_player == null)
            {
                var pc = UnityEngine.Object.FindFirstObjectByType<PlayerController>();
                if (pc == null) return;
                _player = pc.transform;
            }

            LoadStarterSeeds();

            var pos = _player.position;
            bool hasWell = HasAny(ToolChargeConfig.WellId);
            bool atWell = IsNear(ToolChargeConfig.WellId, pos);
            bool atWarehouse = IsNear(ToolChargeConfig.WarehouseId, pos);

            _current.Value = atWell ? StationKind.Well : atWarehouse ? StationKind.Warehouse : StationKind.None;

            float dt = Time.deltaTime;
            float secs = Mathf.Max(0.05f, _charges.Config.FullRefillSeconds);

            // Water + paint at the Well (or the Warehouse when no Well exists)
            if (atWell || (!hasWell && atWarehouse))
            {
                _charges.RefillWater(Step(ref _waterAcc, _charges.Config.WaterMax / secs * dt));
                _charges.RefillDye(Step(ref _dyeAcc, _charges.Config.DyeMax / secs * dt));
            }
            else
            {
                _waterAcc = _dyeAcc = 0f;
            }

            if (atWarehouse)
            {
                _charges.UnloadBasket(Step(ref _basketAcc, _charges.Config.BasketMax / secs * dt));

                var plant = _placementTool?.SelectedPlant;
                if (plant != null && plant.Category != PlantCategory.Tree)
                {
                    // Switching seed type at the Warehouse: swap the bag immediately.
                    if (_charges.SeedPlantId.CurrentValue != null && _charges.SeedPlantId.CurrentValue != plant.PlantId)
                        _charges.ReturnSeeds();
                    _charges.LoadSeeds(plant.PlantId, Step(ref _seedAcc, _charges.Config.SeedMax / secs * dt));
                }
            }
            else
            {
                _basketAcc = _seedAcc = 0f;
            }
        }

        /// <summary>A fresh game starts with the selected plant's seeds already in the bag.</summary>
        private void LoadStarterSeeds()
        {
            if (_starterSeedsLoaded) return;
            var plant = _placementTool?.SelectedPlant;
            if (plant == null) return;
            // Starting inventory may be granted a few frames after the first selection: keep trying briefly.
            if (_charges.Seeds.CurrentValue > 0 || Time.timeSinceLevelLoad > 3f) { _starterSeedsLoaded = true; return; }
            if (plant.Category != PlantCategory.Tree &&
                _charges.LoadSeeds(plant.PlantId, _charges.Config.SeedMax) > 0)
                _starterSeedsLoaded = true;
        }

        private static int Step(ref float acc, float add)
        {
            acc += add;
            int whole = Mathf.FloorToInt(acc);
            acc -= whole;
            return whole;
        }

        private bool HasAny(string id)
        {
            foreach (var p in _placement.PlacedObjects)
                if (p.Data != null && p.Data.PlaceableId == id && p.Instance != null) return true;
            return false;
        }

        private bool IsNear(string id, Vector3 pos)
        {
            float r = _charges.Config.StationRadius;
            foreach (var p in _placement.PlacedObjects)
            {
                if (p.Data == null || p.Data.PlaceableId != id || p.Instance == null) continue;
                var b = GetBounds(p.Instance);
                if (b.SqrDistance(new Vector3(pos.x, b.center.y, pos.z)) <= r * r) return true;
            }
            return false;
        }

        public bool TryGetStationAnchor(StationKind kind, Vector3 from, out Vector3 anchor)
        {
            anchor = default;
            string id = kind == StationKind.Well ? ToolChargeConfig.WellId
                      : kind == StationKind.Warehouse ? ToolChargeConfig.WarehouseId : null;
            if (id == null) return false;

            float best = float.MaxValue;
            foreach (var p in _placement.PlacedObjects)
            {
                if (p.Data == null || p.Data.PlaceableId != id || p.Instance == null) continue;
                var b = GetBounds(p.Instance);
                float d = (b.center - from).sqrMagnitude;
                if (d >= best) continue;
                best = d;
                anchor = new Vector3(b.center.x, b.max.y + 0.4f, b.center.z);
            }
            return best < float.MaxValue;
        }

        private Bounds GetBounds(GameObject go)
        {
            if (_boundsCache.TryGetValue(go, out var b)) return b;
            var renderers = go.GetComponentsInChildren<Renderer>();
            b = new Bounds(go.transform.position, Vector3.one * 0.5f);
            bool first = true;
            foreach (var r in renderers)
            {
                if (r is ParticleSystemRenderer || r.GetComponent<TextMesh>() != null) continue;
                if (first) { b = r.bounds; first = false; }
                else b.Encapsulate(r.bounds);
            }
            if (_boundsCache.Count > 64) _boundsCache.Clear();
            _boundsCache[go] = b;
            return b;
        }

        public void Dispose() => _current.Dispose();
    }
}
