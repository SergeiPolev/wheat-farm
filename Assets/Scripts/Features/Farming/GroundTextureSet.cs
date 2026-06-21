using UnityEngine;

namespace WheatFarm.Farming
{
    /// <summary>
    /// Authoring data for ground textures: an albedo+normal pair per <see cref="GroundState"/>.
    /// GroundTextureArrayBuilder packs the entries into the two Texture2DArrays consumed by
    /// the GroundInstanced shader. Slice index = GroundState ordinal (Grass..PathBrick = 0..6).
    /// </summary>
    [CreateAssetMenu(menuName = "WheatFarm/GroundTextureSet")]
    public class GroundTextureSet : ScriptableObject
    {
        [System.Serializable]
        public struct Entry
        {
            [Tooltip("Self-documenting only — the slice index comes from array order, not this value.")]
            public GroundState State;
            public Texture2D Albedo;
            public Texture2D Normal;
        }

        [Tooltip("One entry per GroundState, in ordinal order (Grass..PathBrick = 0..6).")]
        public Entry[] Entries;

        [Header("Built arrays (assigned by GroundTextureArrayBuilder)")]
        public Texture2DArray AlbedoArray;
        public Texture2DArray NormalArray;
    }
}
