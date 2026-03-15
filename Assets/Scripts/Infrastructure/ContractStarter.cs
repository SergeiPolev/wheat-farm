using VContainer.Unity;
using WheatFarm.Core.Data;
using WheatFarm.Economy;

namespace WheatFarm.Infrastructure
{
    /// <summary>
    /// Auto-accepts the first contract on game start and
    /// bridges harvest events to contract progress.
    /// </summary>
    public class ContractStarter : IStartable
    {
        private readonly IContractService _contracts;
        private readonly ContractDatabase _contractDb;

        public ContractStarter(IContractService contracts, ContractDatabase contractDb = null)
        {
            _contracts = contracts;
            _contractDb = contractDb;
        }

        public void Start()
        {
            if (_contractDb == null || _contractDb.Contracts == null) return;

            // Auto-accept first contract if none active
            if (_contracts.ActiveContracts.Count == 0 && _contractDb.Contracts.Length > 0)
            {
                _contracts.AcceptContract(_contractDb.Contracts[0]);
            }
        }
    }
}
