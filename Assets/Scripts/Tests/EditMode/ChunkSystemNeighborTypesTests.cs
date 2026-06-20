using NUnit.Framework;
using UnityEngine;
using WheatFarm.Core.Data;
using WheatFarm.Farming;

namespace WheatFarm.Tests
{
    /// <summary>
    /// Verifies neighborTypes nibble packing produced by UpdateGroundNeighborFlags.
    /// nibble0=N, nibble1=E, nibble2=S, nibble3=W = neighbor GroundState (only for path cells).
    /// The nibble order is a contract shared with the GroundInstanced shader, which unpacks
    /// the same channel to blend at boundaries between different path types.
    /// </summary>
    public class ChunkSystemNeighborTypesTests
    {
        private ChunkSystem _chunkSystem;

        [SetUp]
        public void SetUp()
        {
            _chunkSystem = new ChunkSystem(4f, 8);
            _chunkSystem.TryUnlockChunk(new Vector2Int(0, 0));
            _chunkSystem.TryUnlockChunk(new Vector2Int(1, 0));
        }

        [TearDown]
        public void TearDown() => _chunkSystem.Dispose();

        private void SetState(Vector2Int chunkCoord, int x, int y, GroundState state)
        {
            var chunk = _chunkSystem.GetChunk(chunkCoord);
            chunk.Cells[chunk.CellIndex(x, y)].GroundState = state;
        }

        private uint NeighborTypes(Vector2Int chunkCoord, int x, int y)
        {
            var chunk = _chunkSystem.GetChunk(chunkCoord);
            return chunk.MeshProps[chunk.CellIndex(x, y)].neighborTypes;
        }

        private static uint Nibble(uint packed, int dir) => (packed >> (dir * 4)) & 0xF;

        [Test]
        public void PathCell_EncodesNeighborStatePerDirection_InNESWOrder()
        {
            var c = Vector2Int.zero;
            SetState(c, 4, 4, GroundState.PathStone);
            SetState(c, 4, 5, GroundState.PathWood);   // N (+Y)
            SetState(c, 5, 4, GroundState.PathBrick);  // E (+X)
            SetState(c, 4, 3, GroundState.PathStone);  // S (-Y)
            SetState(c, 3, 4, GroundState.PathBrick);  // W (-X)

            _chunkSystem.UpdateGroundNeighborFlags(c, 4, 4);

            uint nt = NeighborTypes(c, 4, 4);
            Assert.AreEqual((uint)GroundState.PathWood, Nibble(nt, 0), "N nibble");
            Assert.AreEqual((uint)GroundState.PathBrick, Nibble(nt, 1), "E nibble");
            Assert.AreEqual((uint)GroundState.PathStone, Nibble(nt, 2), "S nibble");
            Assert.AreEqual((uint)GroundState.PathBrick, Nibble(nt, 3), "W nibble");
        }

        [Test]
        public void PathCell_GrassNeighbors_NibblesAllZero()
        {
            var c = Vector2Int.zero;
            SetState(c, 4, 4, GroundState.PathStone); // neighbors remain Grass (0)

            _chunkSystem.UpdateGroundNeighborFlags(c, 4, 4);

            Assert.AreEqual(0u, NeighborTypes(c, 4, 4));
        }

        [Test]
        public void NonPathFarmedCell_NeighborTypesZero_EvenWithPathNeighbor()
        {
            // Blend only applies to path cells; a Tilled cell must not encode neighbor types.
            var c = Vector2Int.zero;
            SetState(c, 4, 4, GroundState.Tilled);
            SetState(c, 5, 4, GroundState.PathWood); // E neighbor is a path

            _chunkSystem.UpdateGroundNeighborFlags(c, 4, 4);

            Assert.AreEqual(0u, NeighborTypes(c, 4, 4));
        }

        [Test]
        public void PathCell_AcrossChunkBorder_EncodesNeighborInAdjacentChunk()
        {
            // PathStone at the east edge of chunk (0,0); east neighbor lives in chunk (1,0).
            var c0 = new Vector2Int(0, 0);
            var c1 = new Vector2Int(1, 0);
            SetState(c0, 7, 2, GroundState.PathStone);
            SetState(c1, 0, 2, GroundState.PathWood);

            _chunkSystem.UpdateGroundNeighborFlags(c0, 7, 2);

            Assert.AreEqual((uint)GroundState.PathWood, Nibble(NeighborTypes(c0, 7, 2), 1), "E nibble across border");
        }

        [Test]
        public void PathBecomesGrass_NeighborTypesReset()
        {
            var c = Vector2Int.zero;
            SetState(c, 4, 4, GroundState.PathStone);
            SetState(c, 5, 4, GroundState.PathWood);
            _chunkSystem.UpdateGroundNeighborFlags(c, 4, 4);
            Assume.That(NeighborTypes(c, 4, 4), Is.Not.EqualTo(0u), "precondition: nibbles set while a path");

            // Bulldoze the path back to grass and recompute.
            SetState(c, 4, 4, GroundState.Grass);
            _chunkSystem.UpdateGroundNeighborFlags(c, 4, 4);

            Assert.AreEqual(0u, NeighborTypes(c, 4, 4), "stale nibbles must clear when cell is no longer a path");
        }
    }
}
