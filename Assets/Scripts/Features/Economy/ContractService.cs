using System.Collections.Generic;
using ObservableCollections;
using R3;
using WheatFarm.Core.Data;
using WheatFarm.Inventory;

namespace WheatFarm.Economy
{
    public struct ActiveContract
    {
        public ContractData Data;
        public int[] Progress; // per required item

        public bool IsComplete
        {
            get
            {
                for (int i = 0; i < Data.Required.Length; i++)
                    if (Progress[i] < Data.Required[i].Amount) return false;
                return true;
            }
        }
    }

    public interface IContractService
    {
        ObservableList<ActiveContract> ActiveContracts { get; }
        Subject<ActiveContract> OnContractCompleted { get; }
        void AcceptContract(ContractData contract);
        void TryCompleteContract(int index);
        void ContributeItem(string itemId, int amount);
    }

    public class ContractService : IContractService
    {
        private readonly IWalletService _wallet;
        private readonly IInventoryService _inventory;

        public ObservableList<ActiveContract> ActiveContracts { get; } = new();
        public Subject<ActiveContract> OnContractCompleted { get; } = new();

        public ContractService(IWalletService wallet, IInventoryService inventory)
        {
            _wallet = wallet;
            _inventory = inventory;
        }

        public void AcceptContract(ContractData contract)
        {
            var active = new ActiveContract
            {
                Data = contract,
                Progress = new int[contract.Required.Length]
            };
            ActiveContracts.Add(active);
        }

        public void ContributeItem(string itemId, int amount)
        {
            for (int c = 0; c < ActiveContracts.Count; c++)
            {
                var contract = ActiveContracts[c];
                for (int i = 0; i < contract.Data.Required.Length; i++)
                {
                    if (contract.Data.Required[i].ItemId != itemId) continue;
                    int needed = contract.Data.Required[i].Amount - contract.Progress[i];
                    if (needed <= 0) continue;

                    int contribute = System.Math.Min(amount, needed);
                    contract.Progress[i] += contribute;
                    amount -= contribute;
                    ActiveContracts[c] = contract;
                }
                if (amount <= 0) break;
            }
        }

        public void TryCompleteContract(int index)
        {
            if (index < 0 || index >= ActiveContracts.Count) return;
            var contract = ActiveContracts[index];
            if (!contract.IsComplete) return;

            _wallet.Add(contract.Data.CoinReward);
            OnContractCompleted.OnNext(contract);
            ActiveContracts.RemoveAt(index);
        }
    }
}
