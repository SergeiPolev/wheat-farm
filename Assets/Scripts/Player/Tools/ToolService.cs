using System.Collections.Generic;
using R3;
using UnityEngine;

namespace WheatFarm.Player.Tools
{
    public interface IToolService
    {
        ReadOnlyReactiveProperty<ITool> CurrentTool { get; }
        ReadOnlyReactiveProperty<ToolId> CurrentToolId { get; }
        void EquipTool(ToolId id);
        void UseCurrentTool(Vector3 worldPos);
    }

    public class ToolService : IToolService
    {
        private readonly Dictionary<ToolId, ITool> _tools = new();
        private readonly ReactiveProperty<ITool> _currentTool = new();
        private readonly ReactiveProperty<ToolId> _currentToolId = new();

        public ReadOnlyReactiveProperty<ITool> CurrentTool => _currentTool;
        public ReadOnlyReactiveProperty<ToolId> CurrentToolId => _currentToolId;

        public ToolService(IEnumerable<ITool> tools)
        {
            foreach (var tool in tools)
            {
                _tools[tool.Id] = tool;
            }

            // Default to planter
            if (_tools.ContainsKey(ToolId.Planter))
                EquipTool(ToolId.Planter);
        }

        public void EquipTool(ToolId id)
        {
            if (!_tools.TryGetValue(id, out var tool))
            {
                Debug.LogWarning($"[ToolService] Tool {id} not registered");
                return;
            }

            _currentTool.Value?.OnUnequip();
            _currentTool.Value = tool;
            _currentToolId.Value = id;
            tool.OnEquip();
        }

        public void UseCurrentTool(Vector3 worldPos)
        {
            _currentTool.Value?.UseAtPosition(worldPos);
        }
    }
}
