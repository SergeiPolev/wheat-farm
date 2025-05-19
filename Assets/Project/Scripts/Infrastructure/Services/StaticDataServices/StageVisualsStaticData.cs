using System;
using UnityEngine;

[CreateAssetMenu(fileName = "StageVisualsStaticData", menuName = "ScriptableObjects/StaticData/Stage Visuals")]
public class StageVisualsStaticData : ScriptableObject
{
    public StageVisual[] Visuals;
    public Vector4 ShadowOffset;
    /*public Vector4 OutlineOffset;
    public float OutlineScale = 1.5f;*/
}

[Serializable]
public struct StageVisual
{
    public Sprite SkyVisual;
    public Texture BackgroundVisual;
    public Texture BlocksVisual;
    public Color CameraColor;
}