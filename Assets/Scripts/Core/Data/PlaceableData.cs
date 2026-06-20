using UnityEngine;

namespace WheatFarm.Core.Data
{
    public enum PlaceableCategory { Building, Decor, Path }
    public enum RotationMode { Fixed, Step90, Free5 }
    public enum BuildingRole { Production, Market, Warehouse, Contracts }


    [CreateAssetMenu(menuName = "WheatFarm/PlaceableData")]
    public class PlaceableData : ScriptableObject
    {
        [Header("Identity")]
        public string PlaceableId;
        public string DisplayName;
        public PlaceableCategory Category;
        public Sprite Icon;

        [Header("Placement")]
        public Vector2Int GridSize = Vector2Int.one;
        public bool BlocksPlanting = true;
        public RotationMode Rotation = RotationMode.Fixed;
        [Tooltip("Маска футпринта: 'X' занято, '.' свободно. Пусто = прямоугольник GridSize (в КЛЕТКАХ).")]
        public string[] FootprintRows;
        [Tooltip("Зазор валидности вокруг маски, в клетках. Не помечает клетки занятыми.")]
        public int PaddingCells = 0;

        [Header("Visual")]
        public GameObject Prefab;

        [Header("Path Properties")]
        public int PathSubtype;
        public Color PathColor = Color.white;

        [Header("Economy")]
        public int Cost;
        public bool UnlockedByDefault = true;

        [Header("Interaction")]
        public bool Interactable;
        public BuildingRole Role;

        public RecipeData[] Recipes;
        public int MaxLevel = 1;
    }
}
