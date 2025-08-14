using System;
using Infrastructure;
using Services;
using UnityEngine;

public class ToolHandler : MonoBehaviour
{
    private Tool _currentTool;

    public Tool CurrentTool => _currentTool;

    private void Start()
    {
        ChangeTool(new WateringTool());
    }

    public void ChangeTool(Tool tool)
    {
        if (_currentTool != null)
        {
            _currentTool.OnEquip();
        }
        
        _currentTool = tool;
        _currentTool.OnEquip();
    }
}

public class WateringTool : Tool
{
    public override ToolID ToolID => ToolID.WateringTool;

    private FieldToolsService _fieldToolsService;
    
    public override void OnEquip()
    {
        _fieldToolsService = AllServices.Container.Single<FieldToolsService>();
        
        ChargeUp(1f);
    }

    public override void OnUnequip()
    {
        
    }

    public override void UseAt(CropFieldData cropRenderer, Vector2Int posInArray)
    {
        if (Charge > 0)
        {
            _fieldToolsService.WaterCrops(cropRenderer, posInArray);
        }
    }

    public override void ChargeUp(float value)
    {
        Charge = value;
    }
}

public class CuttingTool : Tool
{
    public override ToolID ToolID => ToolID.CuttingTool;

    private FieldToolsService _fieldToolsService;
    
    public override void OnEquip()
    {
        _fieldToolsService = AllServices.Container.Single<FieldToolsService>();
        
        ChargeUp(1f);
    }

    public override void OnUnequip()
    {
        
    }

    public override void UseAt(CropFieldData cropRenderer, Vector2Int posInArray)
    {
        if (Charge > 0)
        {
            _fieldToolsService.CutCrops(cropRenderer, posInArray);
        }
    }

    public override void ChargeUp(float value)
    {
        Charge = value;
    }
}

public class PlanterTool : Tool
{
    public override ToolID ToolID => ToolID.PlanterTool;

    private FieldToolsService _fieldToolsService;
    
    public override void OnEquip()
    {
        _fieldToolsService = AllServices.Container.Single<FieldToolsService>();
        
        ChargeUp(1f);
    }

    public override void OnUnequip()
    {
        
    }

    public override void UseAt(CropFieldData cropRenderer, Vector2Int posInArray)
    {
        if (Charge > 0)
        {
            _fieldToolsService.PlantCrops(cropRenderer, posInArray);
        }
    }

    public override void ChargeUp(float value)
    {
        Charge = value;
    }
}