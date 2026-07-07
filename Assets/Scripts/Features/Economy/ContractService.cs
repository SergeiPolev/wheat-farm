using System;
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

    public interface IContractService : IDisposable
    {
        ObservableList<ActiveContract> ActiveContracts { get; }
        Subject<ActiveContract> OnContractCompleted { get; }
        void AcceptContract(ContractData contract);
        bool TryCompleteContract(int index);
        void AbandonContract(int index);
        bool CanComplete(ActiveContract contract);
    }

    public class ContractService : IContractService
    {
        private readonly IWalletService _wallet;
        private readonly IInventoryService _inventory;
        private readonly IPlantUnlockService _plants;
        private readonly IDyeUnlockService _dyes;
        private readonly IBuildingUnlockService _buildings;

        public ObservableList<ActiveContract> ActiveContracts { get; } = new();
        public Subject<ActiveContract> OnContractCompleted { get; } = new();

        public ContractService(
            IWalletService wallet,
            IInventoryService inventory,
            IPlantUnlockService plants,
            IDyeUnlockService dyes,
            IBuildingUnlockService buildings)
        {
            _wallet = wallet;
            _inventory = inventory;
            _plants = plants;
            _dyes = dyes;
            _buildings = buildings;
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

        /// <summary>Completion is inventory-derived: true when every requirement is covered.</summary>
        public bool CanComplete(ActiveContract contract)
        {
            for (int i = 0; i < contract.Data.Required.Length; i++)
                if (!_inventory.HasItem(contract.Data.Required[i].ItemId, contract.Data.Required[i].Amount))
                    return false;
            return true;
        }

        public bool TryCompleteContract(int index)
        {
            if (index < 0 || index >= ActiveContracts.Count) return false;
            var contract = ActiveContracts[index];
            if (!CanComplete(contract)) return false;

            // All-or-nothing: CanComplete guaranteed coverage, so consumes cannot fail mid-way
            foreach (var req in contract.Data.Required)
                _inventory.TryConsume(req.ItemId, req.Amount);

            _wallet.Add(contract.Data.CoinReward);
            if (!string.IsNullOrEmpty(contract.Data.UnlockPlantId))
                _plants.Unlock(contract.Data.UnlockPlantId);
            if (!string.IsNullOrEmpty(contract.Data.UnlockDyeId))
                _dyes.Grant(contract.Data.UnlockDyeId);
            if (!string.IsNullOrEmpty(contract.Data.UnlockBuildingId))
                _buildings.Grant(contract.Data.UnlockBuildingId);

            OnContractCompleted.OnNext(contract);
            ActiveContracts.RemoveAt(index);
            return true;
        }

        public void AbandonContract(int index)
        {
            if (index < 0 || index >= ActiveContracts.Count) return;
            ActiveContracts.RemoveAt(index);
        }

        public void Dispose()
        {
            OnContractCompleted.Dispose();
        }
    }
}
