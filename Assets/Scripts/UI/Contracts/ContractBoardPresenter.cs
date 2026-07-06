using System;
using System.Collections.Generic;
using System.Text;
using ObservableCollections;
using R3;
using VContainer.Unity;
using WheatFarm.Core.Data;
using WheatFarm.Economy;
using WheatFarm.Inventory;

namespace WheatFarm.UI
{
    /// <summary>
    /// Contract board presenter — shows available contracts from database
    /// and active contracts with progress. Drives ContractBoardView.
    /// </summary>
    public class ContractBoardPresenter : IInitializable, IDisposable
    {
        private readonly ContractBoardView _view;
        private readonly IContractService _contracts;
        private readonly ContractRotationService _rotation;
        private readonly IInventoryService _inventory;
        private readonly PlantDatabase _plantDb;
        private readonly DyeDatabase _dyeDb;
        private readonly CompositeDisposable _disposables = new();

        public ContractBoardPresenter(
            ContractBoardView view,
            IContractService contracts,
            ContractRotationService rotation,
            IInventoryService inventory,
            PlantDatabase plantDb,
            DyeDatabase dyeDb)
        {
            _view = view;
            _contracts = contracts;
            _rotation = rotation;
            _inventory = inventory;
            _plantDb = plantDb;
            _dyeDb = dyeDb;
        }

        public void Initialize()
        {
            _view.OnAcceptClicked += OnAccept;
            _view.OnCompleteClicked += OnComplete;
            _view.OnAbandonClicked += OnAbandon;

            _contracts.ActiveContracts.CollectionChanged += OnContractsChanged;
            _inventory.Items.CollectionChanged += OnInventoryChanged;
            _rotation.Available.CollectionChanged += OnAvailableChanged;

            RefreshAll();
        }

        public void Dispose()
        {
            _view.OnAcceptClicked -= OnAccept;
            _view.OnCompleteClicked -= OnComplete;
            _view.OnAbandonClicked -= OnAbandon;
            _contracts.ActiveContracts.CollectionChanged -= OnContractsChanged;
            _inventory.Items.CollectionChanged -= OnInventoryChanged;
            _rotation.Available.CollectionChanged -= OnAvailableChanged;
            _disposables.Dispose();
        }

        private void OnInventoryChanged(in NotifyCollectionChangedEventArgs<InventoryItem> e)
        {
            RefreshActive(); // progress is inventory-derived
        }

        private void OnAvailableChanged(in NotifyCollectionChangedEventArgs<ContractData> e)
        {
            RefreshAvailable();
        }

        private void OnContractsChanged(in NotifyCollectionChangedEventArgs<ActiveContract> e)
        {
            RefreshAll();
        }

        private void RefreshAll()
        {
            RefreshAvailable();
            RefreshActive();
        }

        private void RefreshAvailable()
        {
            // Filter out contracts already accepted (rotation excludes them only at rotate time)
            var activeIds = new HashSet<string>();
            for (int i = 0; i < _contracts.ActiveContracts.Count; i++)
                activeIds.Add(_contracts.ActiveContracts[i].Data.ContractId);

            var available = new List<ContractData>();
            foreach (var c in _rotation.Available)
            {
                if (!activeIds.Contains(c.ContractId))
                    available.Add(c);
            }

            var descriptions = new string[available.Count];
            var canAccept = new bool[available.Count];

            for (int i = 0; i < available.Count; i++)
            {
                descriptions[i] = FormatAvailable(available[i]);
                canAccept[i] = true;
            }

            _view.SetAvailableContracts(descriptions, canAccept);

            // Cache available list for accept handler
            _cachedAvailable = available;
        }

        private void RefreshActive()
        {
            var active = _contracts.ActiveContracts;

            if (active.Count == 0)
            {
                _view.SetContracts(Array.Empty<string>(), Array.Empty<float>(), Array.Empty<bool>());
                return;
            }

            var descriptions = new string[active.Count];
            var progress = new float[active.Count];
            var canComplete = new bool[active.Count];

            for (int i = 0; i < active.Count; i++)
            {
                var c = active[i];
                descriptions[i] = FormatActive(c);
                progress[i] = CalculateProgress(c);
                canComplete[i] = _contracts.CanComplete(c);
            }

            _view.SetContracts(descriptions, progress, canComplete);
        }

        private List<ContractData> _cachedAvailable = new();

        private void OnAccept(int index)
        {
            if (index < 0 || index >= _cachedAvailable.Count) return;
            _contracts.AcceptContract(_cachedAvailable[index]);
            // RefreshAll triggered by CollectionChanged
        }

        private void OnComplete(int index)
        {
            _contracts.TryCompleteContract(index);
        }

        private void OnAbandon(int index)
        {
            _contracts.AbandonContract(index);
        }

        private string FormatAvailable(ContractData contract)
        {
            var sb = new StringBuilder();
            sb.Append(contract.Description);
            sb.Append("  [");
            for (int i = 0; i < contract.Required.Length; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append($"{contract.Required[i].Amount} {contract.Required[i].ItemId}");
            }
            sb.Append(']');
            AppendReward(sb, contract);
            return sb.ToString();
        }

        private string FormatActive(ActiveContract contract)
        {
            var sb = new StringBuilder();
            sb.Append(contract.Data.Description);
            sb.Append("  (");
            for (int i = 0; i < contract.Data.Required.Length; i++)
            {
                if (i > 0) sb.Append(", ");
                var req = contract.Data.Required[i];
                int have = Math.Min(_inventory.GetAmount(req.ItemId), req.Amount);
                sb.Append($"{have}/{req.Amount} {req.ItemId}");
            }
            sb.Append(')');
            AppendReward(sb, contract.Data);
            return sb.ToString();
        }

        private void AppendReward(StringBuilder sb, ContractData contract)
        {
            sb.Append($"  +{contract.CoinReward}c");
            if (!string.IsNullOrEmpty(contract.UnlockPlantId))
            {
                var plant = _plantDb != null ? _plantDb.GetById(contract.UnlockPlantId) : null;
                sb.Append($" +{plant?.DisplayName ?? contract.UnlockPlantId}");
            }
            if (!string.IsNullOrEmpty(contract.UnlockDyeId))
            {
                var dye = _dyeDb != null ? _dyeDb.GetById(contract.UnlockDyeId) : null;
                sb.Append($" +{dye?.DisplayName ?? contract.UnlockDyeId} dye");
            }
        }

        /// <summary>Inventory coverage: sum of min(have, need) over sum of need.</summary>
        private float CalculateProgress(ActiveContract contract)
        {
            if (contract.Data.Required.Length == 0) return 1f;

            int have = 0, need = 0;
            for (int i = 0; i < contract.Data.Required.Length; i++)
            {
                var req = contract.Data.Required[i];
                have += Math.Min(_inventory.GetAmount(req.ItemId), req.Amount);
                need += req.Amount;
            }
            return need > 0 ? (float)have / need : 1f;
        }
    }
}
