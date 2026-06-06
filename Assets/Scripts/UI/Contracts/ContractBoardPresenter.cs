using System;
using System.Collections.Generic;
using System.Text;
using ObservableCollections;
using R3;
using VContainer.Unity;
using WheatFarm.Core.Data;
using WheatFarm.Economy;

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
        private readonly ContractDatabase _contractDb;
        private readonly CompositeDisposable _disposables = new();

        public ContractBoardPresenter(
            ContractBoardView view,
            IContractService contracts,
            ContractDatabase contractDb)
        {
            _view = view;
            _contracts = contracts;
            _contractDb = contractDb;
        }

        public void Initialize()
        {
            _view.OnAcceptClicked += OnAccept;
            _view.OnCompleteClicked += OnComplete;

            _contracts.ActiveContracts.CollectionChanged += OnContractsChanged;

            RefreshAll();
        }

        public void Dispose()
        {
            _view.OnAcceptClicked -= OnAccept;
            _view.OnCompleteClicked -= OnComplete;
            _contracts.ActiveContracts.CollectionChanged -= OnContractsChanged;
            _disposables.Dispose();
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
            if (_contractDb == null || _contractDb.Contracts == null)
            {
                _view.SetAvailableContracts(Array.Empty<string>(), Array.Empty<bool>());
                return;
            }

            // Filter out contracts already accepted
            var activeIds = new HashSet<string>();
            for (int i = 0; i < _contracts.ActiveContracts.Count; i++)
                activeIds.Add(_contracts.ActiveContracts[i].Data.ContractId);

            var available = new List<ContractData>();
            foreach (var c in _contractDb.Contracts)
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
                canComplete[i] = c.IsComplete;
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

        private static string FormatAvailable(ContractData contract)
        {
            var sb = new StringBuilder();
            sb.Append(contract.Description);
            sb.Append("  [");
            for (int i = 0; i < contract.Required.Length; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append($"{contract.Required[i].Amount} {contract.Required[i].ItemId}");
            }
            sb.Append($"]  +{contract.CoinReward}c");
            return sb.ToString();
        }

        private static string FormatActive(ActiveContract contract)
        {
            var sb = new StringBuilder();
            sb.Append(contract.Data.Description);
            sb.Append("  (");
            for (int i = 0; i < contract.Data.Required.Length; i++)
            {
                if (i > 0) sb.Append(", ");
                var req = contract.Data.Required[i];
                sb.Append($"{contract.Progress[i]}/{req.Amount} {req.ItemId}");
            }
            sb.Append($")  +{contract.Data.CoinReward}c");
            return sb.ToString();
        }

        private static float CalculateProgress(ActiveContract contract)
        {
            if (contract.Data.Required.Length == 0) return 1f;

            float total = 0f;
            for (int i = 0; i < contract.Data.Required.Length; i++)
            {
                float req = contract.Data.Required[i].Amount;
                if (req > 0)
                    total += contract.Progress[i] / req;
            }
            return total / contract.Data.Required.Length;
        }
    }
}
