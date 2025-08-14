using System.Collections.Generic;
using Services;
using UnityEngine;

namespace Infrastructure
{
    public class ChangeToolsService : IService, ITick
    {
        private Dictionary<ToolID, Tool> _tools = new();

        private GlobalBlackboard _globalBlackboard;
        
        public void Initialize()
        {
            _globalBlackboard = AllServices.Container.Single<GlobalBlackboard>();
            _tools.Add(ToolID.WateringTool, new WateringTool());
            _tools.Add(ToolID.CuttingTool, new CuttingTool());
            _tools.Add(ToolID.PlanterTool, new PlanterTool());
        }

        public void ChangeTool(ToolID toolID)
        {
            _globalBlackboard.Player.ToolHandler.ChangeTool(_tools[toolID]);
        }

        public void Tick()
        {
            if (Input.GetKeyDown(KeyCode.Keypad1))
            {
                ChangeTool(ToolID.WateringTool);
            }
            else if (Input.GetKeyDown(KeyCode.Keypad2))
            {
                ChangeTool(ToolID.CuttingTool);
            }
            else if (Input.GetKeyDown(KeyCode.Keypad3))
            {
                ChangeTool(ToolID.PlanterTool);
            }
        }
    }
}