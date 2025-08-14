using UnityEngine;

public enum ToolID
{
    WateringTool = 1,
    CuttingTool = 2,
    PlanterTool = 3,
}

public abstract class Tool
{
    public float Charge;
    public abstract ToolID ToolID { get; }

    public abstract void OnEquip();
    public abstract void OnUnequip();
    public abstract void UseAt(CropFieldData cropRenderer, Vector2Int posInArray);
    public abstract void ChargeUp(float value);
}