using VContainer.Unity;
using WheatFarm.Core.Data;
using WheatFarm.Economy;

namespace WheatFarm.Infrastructure
{
    /// <summary>
    /// Placeholder for future contract auto-generation or rotation logic.
    /// Previously auto-accepted the first contract; now players choose via UI.
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
            // Contracts are now accepted by the player via ContractBoardView (press C).
            // This class is kept as a hook for future logic (e.g. daily rotation, unlock gating).
        }
    }
}
