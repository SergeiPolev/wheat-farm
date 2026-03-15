using UnityEngine;

namespace WheatFarm.Core.Data
{
    [CreateAssetMenu(menuName = "WheatFarm/ContractDatabase")]
    public class ContractDatabase : ScriptableObject
    {
        public ContractData[] Contracts;

        public ContractData GetById(string id)
        {
            foreach (var c in Contracts)
                if (c.ContractId == id) return c;
            return null;
        }
    }
}
