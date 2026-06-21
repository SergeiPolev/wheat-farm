using NUnit.Framework;
using UnityEngine;
using WheatFarm.Farming;

namespace WheatFarm.Tests
{
    /// <summary>
    /// Grass cells fade toward soil near *farmland* (Tilled/Watered/Fertilized) via the proximity
    /// value stored in MeshProps.uv.w. Paths (PathStone..PathBrick) are hard surfaces and must NOT
    /// trigger that soil halo — otherwise stone/wood paths get an ugly muddy ring on the grass.
    /// </summary>
    public class ChunkSystemProximityTests
    {
        private ChunkSystem _chunkSystem;
        private static readonly Vector2Int C = Vector2Int.zero;

        [SetUp]
        public void SetUp()
        {
            _chunkSystem = new ChunkSystem(4f, 8);
            _chunkSystem.TryUnlockChunk(C);
        }

        [TearDown]
        public void TearDown() => _chunkSystem.Dispose();

        private void SetState(int x, int y, GroundState state)
        {
            var chunk = _chunkSystem.GetChunk(C);
            chunk.Cells[chunk.CellIndex(x, y)].GroundState = state;
        }

        private float Proximity(int x, int y)
        {
            var chunk = _chunkSystem.GetChunk(C);
            return chunk.MeshProps[chunk.CellIndex(x, y)].uv.w;
        }

        [Test]
        public void GrassNextToFarmland_HasSoilProximity()
        {
            SetState(4, 4, GroundState.Tilled);
            _chunkSystem.UpdateGroundNeighborFlags(C, 4, 4);

            Assert.Greater(Proximity(5, 4), 0f, "grass beside tilled soil should fade toward it");
        }

        [Test]
        public void GrassNextToPath_HasNoSoilProximity()
        {
            SetState(4, 4, GroundState.PathStone);
            _chunkSystem.UpdateGroundNeighborFlags(C, 4, 4);

            Assert.AreEqual(0f, Proximity(5, 4), "grass beside a path must not get a soil halo");
        }
    }
}
