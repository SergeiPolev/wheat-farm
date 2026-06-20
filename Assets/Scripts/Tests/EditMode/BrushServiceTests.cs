using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using WheatFarm.Farming;

namespace WheatFarm.Tests
{
    /// <summary>Records applied cells; CanApply = Bulldozable (the predicate that
    /// exposes the old Occupied-skip bug, since path cells are Occupied).</summary>
    internal class RecordingBulldozeAction : IBrushAction
    {
        public readonly List<(Vector2Int chunk, int x, int y)> Applied = new();

        public bool CanApply(ChunkData chunk, int cellX, int cellY) =>
            BrushPredicates.Bulldozable(chunk.Cells[chunk.CellIndex(cellX, cellY)]);

        public void Apply(ChunkData chunk, int cellX, int cellY) =>
            Applied.Add((chunk.ChunkCoord, cellX, cellY));
    }

    public class BrushServiceTests
    {
        private ChunkSystem _chunks;
        private BrushService _brush;

        [SetUp]
        public void SetUp()
        {
            _chunks = new ChunkSystem(chunkWorldSize: 4f, subCellResolution: 8);
            _chunks.TryUnlockChunk(Vector2Int.zero);
            _brush = new BrushService(_chunks);
            _brush.CurrentSize.Value = BrushSize.Medium;
        }

        [TearDown]
        public void TearDown()
        {
            _brush.Dispose();
            _chunks.Dispose();
        }

        private void SetCell(int x, int y, GroundState ground, bool occupied, string plantId = null)
        {
            var chunk = _chunks.GetChunk(Vector2Int.zero);
            int idx = chunk.CellIndex(x, y);
            chunk.Cells[idx].GroundState = ground;
            chunk.Cells[idx].Occupied = occupied;
            chunk.Cells[idx].PlantId = plantId;
        }

        [Test]
        public void Apply_ReachesOccupiedPathCells()
        {
            // Regression: path cells set Occupied=true; old code skipped them entirely.
            SetCell(4, 4, GroundState.PathStone, occupied: true);
            var action = new RecordingBulldozeAction();
            var center = _chunks.CellToWorld(Vector2Int.zero, 4, 4);

            _brush.ApplyAtWorldPos(center, action);

            Assert.Contains((Vector2Int.zero, 4, 4), action.Applied);
        }

        [Test]
        public void Apply_SkipsCellsWhereCanApplyFalse()
        {
            // Empty grass: Bulldozable == false everywhere
            var action = new RecordingBulldozeAction();
            _brush.ApplyAtWorldPos(_chunks.CellToWorld(Vector2Int.zero, 4, 4), action);
            Assert.IsEmpty(action.Applied);
        }

        [Test]
        public void PreviewAndApply_SameCellSet()
        {
            // Mixed field: paths (occupied), crops, occupied-by-building, empty
            SetCell(3, 4, GroundState.PathWood, occupied: true);
            SetCell(4, 4, GroundState.Grass, occupied: false, plantId: "wheat");
            SetCell(5, 4, GroundState.Grass, occupied: true);   // building — not bulldozable by brush
            var action = new RecordingBulldozeAction();
            var center = _chunks.CellToWorld(Vector2Int.zero, 4, 4);

            var previewSet = _brush.GetAffectableCells(center)
                .Where(c => action.CanApply(c.chunk, c.cellX, c.cellY))
                .Select(c => (c.chunk.ChunkCoord, c.cellX, c.cellY))
                .ToHashSet();

            _brush.ApplyAtWorldPos(center, action);

            CollectionAssert.AreEquivalent(previewSet, action.Applied);
            Assert.IsTrue(previewSet.Contains((Vector2Int.zero, 3, 4)), "path cell in preview");
            Assert.IsTrue(previewSet.Contains((Vector2Int.zero, 4, 4)), "crop cell in preview");
            Assert.IsFalse(previewSet.Contains((Vector2Int.zero, 5, 4)), "building cell excluded");
        }

        [Test]
        public void GetAffectableCells_ExcludesLockedChunks()
        {
            // Brush at the chunk edge reaches into the locked neighbor chunk
            var edge = _chunks.CellToWorld(Vector2Int.zero, 7, 4);
            foreach (var (chunk, _, _) in _brush.GetAffectableCells(edge))
                Assert.AreEqual(Vector2Int.zero, chunk.ChunkCoord);
        }
    }
}
