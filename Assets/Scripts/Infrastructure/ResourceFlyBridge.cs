using System;
using System.Collections.Generic;
using R3;
using UnityEngine;
using VContainer.Unity;
using WheatFarm.Buildings;
using WheatFarm.Economy;
using WheatFarm.Farming;
using WheatFarm.Player.Charges;
using WheatFarm.Player.FX;

namespace WheatFarm.Infrastructure
{
    /// <summary>
    /// Turns resource transfers into visible flights (IResourceFlyFX):
    /// Well → player (water, paint), Warehouse → player (seeds), player → Warehouse (basket crops),
    /// field → player (harvest), player ↔ Contracts board (delivery + reward coins).
    /// The Market wires its own flights in MarketPresenter (it knows which stall was clicked).
    /// </summary>
    public class ResourceFlyBridge : IInitializable, ITickable, IDisposable
    {
        private const float MinGapPerKind = 0.07f;
        private const int HarvestTokensPerFrame = 6;
        private const string ContractsId = "contracts";

        private readonly IResourceFlyFX _fly;
        private readonly IToolChargeService _charges;
        private readonly IToolStationService _stations;
        private readonly IPlantSystem _plants;
        private readonly IPlacementService _placement;
        private readonly IContractService _contracts;
        private readonly CompositeDisposable _subs = new();
        private readonly Dictionary<ChargeKind, float> _lastFly = new();
        private int _harvestFrame = -1, _harvestThisFrame;

        public ResourceFlyBridge(
            IResourceFlyFX fly,
            IToolChargeService charges,
            IToolStationService stations,
            IPlantSystem plants,
            IPlacementService placement,
            IContractService contracts = null)
        {
            _fly = fly;
            _charges = charges;
            _stations = stations;
            _plants = plants;
            _placement = placement;
            _contracts = contracts;
        }

        public void Initialize()
        {
            _charges.OnTransferred.Subscribe(OnTransferred).AddTo(_subs);
            _plants.OnHarvested.Subscribe(OnHarvested).AddTo(_subs);
            _contracts?.OnContractCompleted.Subscribe(_ => OnContractCompleted()).AddTo(_subs);
        }

        public void Tick() { }

        private void OnTransferred((ChargeKind kind, int amount) t)
        {
            float now = Time.time;
            if (_lastFly.TryGetValue(t.kind, out var last) && now - last < MinGapPerKind) return;
            _lastFly[t.kind] = now;

            var stationKind = t.kind == ChargeKind.Basket || t.kind == ChargeKind.Seeds
                ? StationKind.Warehouse
                : _stations.StationFor(t.kind);
            if (!_stations.TryGetStation(stationKind, Vector3.zero, out var center, out var inst)) return;
            // Prefer the station the player is actually standing at (several Wells may exist)
            if (_stations.CurrentStation.CurrentValue == stationKind)
            {
                var player = UnityEngine.Object.FindFirstObjectByType<WheatFarm.Player.PlayerController>();
                if (player != null) _stations.TryGetStation(stationKind, player.transform.position, out center, out inst);
            }

            center += Vector3.up * 0.5f; // pop out of the upper part of the building
            switch (t.kind)
            {
                case ChargeKind.Water: _fly.ToPlayer(FlyToken.Water, center, 1, inst); break;
                case ChargeKind.Dye: _fly.ToPlayer(FlyToken.Paint, center, 1, inst); break;
                case ChargeKind.Seeds: _fly.ToPlayer(FlyToken.Seed, center, 1, inst); break;
                case ChargeKind.Basket: _fly.FromPlayer(FlyToken.Crop, center, Mathf.Min(2, t.amount), inst); break;
            }
        }

        private void OnHarvested(HarvestData data)
        {
            int f = Time.frameCount;
            if (f != _harvestFrame) { _harvestFrame = f; _harvestThisFrame = 0; }
            if (_harvestThisFrame++ >= HarvestTokensPerFrame) return;
            _fly.ToPlayer(FlyToken.Crop, data.WorldPosition + Vector3.up * 0.3f);
        }

        private void OnContractCompleted()
        {
            foreach (var p in _placement.PlacedObjects)
            {
                if (p.Data == null || p.Data.PlaceableId != ContractsId || p.Instance == null) continue;
                var pos = p.Instance.transform.position + Vector3.up * 1.2f;
                _fly.FromPlayer(FlyToken.Crop, pos, 8, p.Instance.transform);
                _fly.ToPlayer(FlyToken.Coin, pos, 8, p.Instance.transform, delay: 0.45f);
                return;
            }
        }

        public void Dispose() => _subs.Dispose();
    }
}
