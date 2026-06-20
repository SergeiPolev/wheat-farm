using System;
using UnityEngine;
using VContainer.Unity;
using WheatFarm.Buildings;
using WheatFarm.Core.Data;
using WheatFarm.Player;

namespace WheatFarm.UI
{
    /// <summary>
    /// Opens economy panels when the player clicks the matching building:
    /// Warehouse → Inventory, Contracts → Contract board.
    /// (Market is handled by MarketPresenter, Production by BuildingPanelPresenter.)
    /// </summary>
    public class EconomyBuildingPresenter : IInitializable, IDisposable
    {
        private readonly FarmInteractionController _interaction;
        private readonly InventoryView _inventory;
        private readonly ContractBoardView _contracts;

        public EconomyBuildingPresenter(
            FarmInteractionController interaction,
            InventoryView inventory,
            ContractBoardView contracts)
        {
            _interaction = interaction;
            _inventory = inventory;
            _contracts = contracts;
        }

        public void Initialize()
        {
            if (_interaction != null)
                _interaction.OnBuildingClicked += OnBuildingClicked;
        }

        public void Dispose()
        {
            if (_interaction != null)
                _interaction.OnBuildingClicked -= OnBuildingClicked;
        }

        private void OnBuildingClicked(GameObject go)
        {
            if (go == null) return;

            var marker = go.GetComponentInParent<BuildingMarker>();
            if (marker == null || marker.PlacedObject == null) return;

            var data = marker.PlacedObject.Data;
            if (data == null) return;

            switch (data.Role)
            {
                case BuildingRole.Warehouse:
                    if (_inventory != null)
                    {
                        if (_inventory.IsOpen) _inventory.Hide();
                        else _inventory.Show();
                    }
                    break;

                case BuildingRole.Contracts:
                    if (_contracts != null)
                    {
                        if (_contracts.IsOpen) _contracts.Hide();
                        else _contracts.Show();
                    }
                    break;
            }
        }
    }
}
