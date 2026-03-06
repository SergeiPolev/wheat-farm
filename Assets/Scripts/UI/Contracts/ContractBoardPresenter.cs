using System;
using System.Text;
using ObservableCollections;
using R3;
using VContainer.Unity;
using WheatFarm.Economy;

namespace WheatFarm.UI
{
    /// <summary>
    /// Contract board presenter — subscribes to active contracts, drives ContractBoardView.
    /// </summary>
    public class ContractBoardPresenter : IInitializable, IDisposable
    {
        private readonly ContractBoardView _view;
        private readonly IContractService _contracts;
        private readonly CompositeDisposable _disposables = new();

        public ContractBoardPresenter(ContractBoardView view, IContractService contracts)
        {
            _view = view;
            _contracts = contracts;
        }

        public void Initialize()
        {
            _view.OnCompleteClicked += OnComplete;

            // Subscribe to collection changes via typed event handler
            _contracts.ActiveContracts.CollectionChanged += OnContractsChanged;

            RefreshAll();
        }

        public void Dispose()
        {
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
            var active = _contracts.ActiveContracts;

            if (active.Count == 0)
            {
                _view.ShowEmpty("No active contracts");
                return;
            }

            var descriptions = new string[active.Count];
            var progress = new float[active.Count];
            var canComplete = new bool[active.Count];

            for (int i = 0; i < active.Count; i++)
            {
                var c = active[i];
                descriptions[i] = FormatContract(c);
                progress[i] = CalculateProgress(c);
                canComplete[i] = c.IsComplete;
            }

            _view.SetContracts(descriptions, progress, canComplete);
        }

        private void OnComplete(int index)
        {
            _contracts.TryCompleteContract(index);
        }

        private static string FormatContract(ActiveContract contract)
        {
            var sb = new StringBuilder();
            sb.Append(contract.Data.Description);
            sb.Append(" (");
            for (int i = 0; i < contract.Data.Required.Length; i++)
            {
                if (i > 0) sb.Append(", ");
                var req = contract.Data.Required[i];
                sb.Append($"{contract.Progress[i]}/{req.Amount} {req.ItemId}");
            }
            sb.Append($") +{contract.Data.CoinReward} coins");
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
