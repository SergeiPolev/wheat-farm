using UnityEngine;
using WheatFarm.Core.Data;

namespace WheatFarm.Farming
{
    /// <summary>
    /// Data for a single chunk in the farm grid.
    /// Each chunk contains a grid of sub-cells that can hold plants.
    /// </summary>
    public class ChunkData
    {
        public readonly Vector2Int ChunkCoord;
        public readonly int Resolution;
        public bool Unlocked;
        public bool Dirty;

        /// <summary>Gameplay state per sub-cell.</summary>
        public readonly SubCellState[] Cells;

        /// <summary>GPU instance data per sub-cell (mirrors Cells for rendering).</summary>
        public readonly MeshProperties[] MeshProps;

        public int CellCount => Resolution * Resolution;

        public ChunkData(Vector2Int coord, int resolution)
        {
            ChunkCoord = coord;
            Resolution = resolution;
            Unlocked = false;
            Dirty = false;
            Cells = new SubCellState[resolution * resolution];
            MeshProps = new MeshProperties[resolution * resolution];

            for (int i = 0; i < Cells.Length; i++)
                Cells[i] = SubCellState.Empty;
        }

        public int CellIndex(int x, int y) => y * Resolution + x;

        public bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < Resolution && y < Resolution;
    }
}
