using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using WheatFarm.Buildings;
using WheatFarm.Core.Data;
using WheatFarm.Economy;
using WheatFarm.Farming;

namespace WheatFarm.Tests
{
    /// <summary>Minimal fake wallet — always allows spending, records Add() calls.</summary>
    public class FakeWalletService : IWalletService
    {
        private readonly R3.ReactiveProperty<int> _coins = new(1000);
        public R3.ReadOnlyReactiveProperty<int> Coins => _coins;

        public int SpentTotal { get; private set; }
        public int AddedTotal { get; private set; }

        public bool CanAfford(int amount) => true;

        public void Add(int amount)
        {
            AddedTotal += amount;
            _coins.Value += amount;
        }

        public bool TrySpend(int amount)
        {
            SpentTotal += amount;
            _coins.Value -= amount;
            return true;
        }

        public void SetCoins(int amount) => _coins.Value = amount;

        public void Dispose() => _coins.Dispose();
    }

    public class PlacementServiceTests
    {
        private ChunkSystem _chunkSystem;
        private FakeWalletService _wallet;
        private PlacementService _service;

        [SetUp]
        public void SetUp()
        {
            _chunkSystem = new ChunkSystem(4f, 8);
            _wallet = new FakeWalletService();
            _service = new PlacementService(_chunkSystem, _wallet);

            // Unlock a generous area around the origin.
            for (int x = -1; x <= 2; x++)
                for (int y = -1; y <= 2; y++)
                    _chunkSystem.TryUnlockChunk(new Vector2Int(x, y));
        }

        [TearDown]
        public void TearDown()
        {
            _wallet.Dispose();
            _chunkSystem.Dispose();
        }

        private static PlaceableData MakeLMask(int paddingCells = 0, RotationMode rotation = RotationMode.Fixed)
        {
            var data = ScriptableObject.CreateInstance<PlaceableData>();
            data.FootprintRows = new[] { "X.", "XX" };
            data.Category = PlaceableCategory.Building;
            data.Cost = 0;
            data.Prefab = null;
            data.PaddingCells = paddingCells;
            data.Rotation = rotation;
            return data;
        }

        // L-mask Cells(0) offsets relative to center cell (0,0): (0,0), (0,1), (1,1)
        private static readonly Vector2Int[] LOffsets0 =
        {
            new(0, 0), new(0, 1), new(1, 1)
        };

        private static HashSet<(Vector2Int chunkCoord, int cellX, int cellY)> ResolveOffsets(
            ChunkSystem chunkSystem, Vector3 worldPos, IEnumerable<Vector2Int> offsets)
        {
            var (anchorChunk, ax, ay) = chunkSystem.WorldToCell(worldPos);
            var anchorWorld = chunkSystem.CellToWorld(anchorChunk, ax, ay);
            var result = new HashSet<(Vector2Int, int, int)>();
            foreach (var off in offsets)
            {
                var offWorld = anchorWorld + new Vector3(off.x, 0, off.y) * chunkSystem.CellWorldSize;
                result.Add(chunkSystem.WorldToCell(offWorld));
            }
            return result;
        }

        // --- L-mask on clean field ---

        [Test]
        public void LMask_CleanField_CanPlaceTrue()
        {
            var data = MakeLMask();
            var worldPos = _chunkSystem.CellToWorld(Vector2Int.zero, 2, 2);

            Assert.IsTrue(_service.CanPlace(data, worldPos, 0, 0f));
        }

        [Test]
        public void LMask_AfterPlace_ExactlyMaskCellsOccupied()
        {
            var data = MakeLMask();
            var worldPos = _chunkSystem.CellToWorld(Vector2Int.zero, 2, 2);

            var placed = _service.Place(data, worldPos, 0, 0f);
            Assert.IsNotNull(placed);

            var expected = ResolveOffsets(_chunkSystem, worldPos, LOffsets0);

            // All expected cells are occupied
            foreach (var (chunkCoord, cx, cy) in expected)
            {
                var chunk = _chunkSystem.GetChunk(chunkCoord);
                int idx = chunk.CellIndex(cx, cy);
                Assert.IsTrue(chunk.Cells[idx].Occupied, $"Expected cell ({chunkCoord},{cx},{cy}) to be occupied");
            }

            // PlacedObject.OccupiedCells matches the same set
            var actual = new HashSet<(Vector2Int, int, int)>(placed.OccupiedCells);
            CollectionAssert.AreEquivalent(expected, actual);
        }

        // --- Pre-occupied cell ---

        [Test]
        public void LMask_OnePreOccupiedCell_CanPlaceFalse_AndEvaluateMarksOnlyThatCellBad()
        {
            var data = MakeLMask();
            var worldPos = _chunkSystem.CellToWorld(Vector2Int.zero, 2, 2);

            // Pre-occupy the far-corner mask cell (offset (1,1))
            var blocked = ResolveOffsets(_chunkSystem, worldPos, new[] { new Vector2Int(1, 1) });
            foreach (var (chunkCoord, cx, cy) in blocked)
            {
                var chunk = _chunkSystem.GetChunk(chunkCoord);
                chunk.Cells[chunk.CellIndex(cx, cy)].Occupied = true;
            }

            Assert.IsFalse(_service.CanPlace(data, worldPos, 0, 0f));

            var result = new List<(Vector3 worldPos, bool ok)>();
            var eval = _service.EvaluateFootprint(data, worldPos, 0, 0f, result);

            Assert.IsFalse(eval.AllOk);

            // Exactly one entry should be ok=false
            int badCount = 0;
            foreach (var (_, ok) in result)
                if (!ok) badCount++;
            Assert.AreEqual(1, badCount);
            Assert.AreEqual(LOffsets0.Length, result.Count);
        }

        // --- Mask crossing chunk border ---

        [Test]
        public void LMask_CrossingChunkBorder_PlaceableAndSpansTwoChunks()
        {
            var data = MakeLMask();
            // Anchor at cell (7,4) of chunk (0,0) — far edge, so offset (1,*) spills into chunk (1,0)
            var worldPos = _chunkSystem.CellToWorld(new Vector2Int(0, 0), 7, 4);

            Assert.IsTrue(_service.CanPlace(data, worldPos, 0, 0f));

            var placed = _service.Place(data, worldPos, 0, 0f);
            Assert.IsNotNull(placed);

            var chunkCoords = new HashSet<Vector2Int>();
            foreach (var (chunkCoord, _, _) in placed.OccupiedCells)
                chunkCoords.Add(chunkCoord);

            Assert.AreEqual(2, chunkCoords.Count, "OccupiedCells should span two chunkCoords");
            Assert.IsTrue(chunkCoords.Contains(new Vector2Int(0, 0)));
            Assert.IsTrue(chunkCoords.Contains(new Vector2Int(1, 0)));
        }

        [Test]
        public void LMask_CrossingChunkBorder_NeighborChunkLocked_CanPlaceFalse()
        {
            var freshChunkSystem = new ChunkSystem(4f, 8);
            var freshWallet = new FakeWalletService();
            var freshService = new PlacementService(freshChunkSystem, freshWallet);

            // Unlock only chunk (0,0) — neighbor (1,0) stays locked
            freshChunkSystem.TryUnlockChunk(new Vector2Int(0, 0));

            var data = MakeLMask();
            var worldPos = freshChunkSystem.CellToWorld(new Vector2Int(0, 0), 7, 4);

            Assert.IsFalse(freshService.CanPlace(data, worldPos, 0, 0f));

            freshWallet.Dispose();
            freshChunkSystem.Dispose();
        }

        // --- Padding ---

        [Test]
        public void Padding1_AdjacentBuilding_BlocksWithoutGap_AllowsWithGap()
        {
            var data = MakeLMask(paddingCells: 1);

            // Place building A anchored at cell (2,2) of chunk(0,0)
            var posA = _chunkSystem.CellToWorld(Vector2Int.zero, 2, 2);
            var placedA = _service.Place(data, posA, 0, 0f);
            Assert.IsNotNull(placedA);

            // Building B directly adjacent (touching A's occupied cells within ring 1) → should be blocked
            // A occupies cells (2,2),(2,3),(3,3) (offsets (0,0),(0,1),(1,1) from anchor (2,2))
            // Place B anchored at (4,2) — its mask cells (4,2),(4,3),(5,3); padding ring of B
            // at (3,2),(3,3) etc touches A's occupied cell (3,3) -> blocked
            var posB = _chunkSystem.CellToWorld(Vector2Int.zero, 4, 2);
            Assert.IsFalse(_service.CanPlace(data, posB, 0, 0f));

            // With one free cell gap: anchor B further away at (5,2)
            var posBFar = _chunkSystem.CellToWorld(Vector2Int.zero, 5, 2);
            Assert.IsTrue(_service.CanPlace(data, posBFar, 0, 0f));

            var placedB = _service.Place(data, posBFar, 0, 0f);
            Assert.IsNotNull(placedB);

            // After placing B, padding cells are NOT occupied
            // B's mask cells occupy (5,2),(5,3),(6,3); padding ring cells e.g. (4,2) must remain free
            var paddingCell = _chunkSystem.WorldToCell(_chunkSystem.CellToWorld(Vector2Int.zero, 4, 2));
            var paddingChunk = _chunkSystem.GetChunk(paddingCell.chunkCoord);
            Assert.IsFalse(paddingChunk.Cells[paddingChunk.CellIndex(paddingCell.cellX, paddingCell.cellY)].Occupied);
        }

        [Test]
        public void Padding1_RingIntoLockedOrMissingChunk_Ignored()
        {
            var freshChunkSystem = new ChunkSystem(4f, 8);
            var freshWallet = new FakeWalletService();
            var freshService = new PlacementService(freshChunkSystem, freshWallet);

            // Unlock only chunk (0,0); neighbors stay locked/missing
            freshChunkSystem.TryUnlockChunk(new Vector2Int(0, 0));

            var data = MakeLMask(paddingCells: 1);

            // Anchor near a corner so the padding ring spills into locked/missing chunks
            var worldPos = freshChunkSystem.CellToWorld(new Vector2Int(0, 0), 0, 0);

            Assert.IsTrue(freshService.CanPlace(data, worldPos, 0, 0f),
                "Padding spilling into locked/missing chunks must be ignored");

            freshWallet.Dispose();
            freshChunkSystem.Dispose();
        }

        [Test]
        public void Padding1_Step90Rotation1_PaddingFollowsRotatedMask()
        {
            var data = MakeLMask(paddingCells: 1, rotation: RotationMode.Step90);

            // Place building A unrotated, anchored at (2,2)
            var posA = _chunkSystem.CellToWorld(Vector2Int.zero, 2, 2);
            var placedA = _service.Place(data, posA, 0, 0f);
            Assert.IsNotNull(placedA);

            // Place B rotated by 1 step (90deg). Rotated L-mask Cells(1) offsets: (1,0),(0,0),(0,1)
            // Anchor B such that its rotated padding ring touches A's occupied cells.
            // A occupies (2,2),(2,3),(3,3). Anchor B at (4,2) with rotation 1:
            // B mask cells (offsets (1,0),(0,0),(0,1) from anchor (4,2)) = (5,2),(4,2),(4,3)
            // padding ring includes (3,2),(3,3) which touch A's (3,3) -> blocked
            var posB = _chunkSystem.CellToWorld(Vector2Int.zero, 4, 2);
            Assert.IsFalse(_service.CanPlace(data, posB, 1, 0f));
        }

        // --- Parity ---

        [Test]
        public void EvaluateFootprint_AllOk_MatchesCanPlace_OnMixedScenarios()
        {
            var data = MakeLMask(paddingCells: 1);
            var scratch = new List<(Vector3 worldPos, bool ok)>();

            // Scenario 1: clean field
            var pos1 = _chunkSystem.CellToWorld(Vector2Int.zero, 0, 0);
            Assert.AreEqual(_service.CanPlace(data, pos1, 0, 0f), _service.EvaluateFootprint(data, pos1, 0, 0f, scratch).AllOk);

            // Scenario 2: after placing something nearby
            _service.Place(data, pos1, 0, 0f);
            var pos2 = _chunkSystem.CellToWorld(Vector2Int.zero, 2, 0);
            Assert.AreEqual(_service.CanPlace(data, pos2, 0, 0f), _service.EvaluateFootprint(data, pos2, 0, 0f, scratch).AllOk);

            // Scenario 3: locked neighbor chunk
            var freshChunkSystem = new ChunkSystem(4f, 8);
            var freshWallet = new FakeWalletService();
            var freshService = new PlacementService(freshChunkSystem, freshWallet);
            freshChunkSystem.TryUnlockChunk(new Vector2Int(0, 0));
            var pos3 = freshChunkSystem.CellToWorld(new Vector2Int(0, 0), 7, 4);
            Assert.AreEqual(freshService.CanPlace(data, pos3, 0, 0f), freshService.EvaluateFootprint(data, pos3, 0, 0f, scratch).AllOk);
            freshWallet.Dispose();
            freshChunkSystem.Dispose();
        }

        // --- Remove ---

        [Test]
        public void Remove_FreesExactlyOccupiedCells()
        {
            var data = MakeLMask();
            var worldPos = _chunkSystem.CellToWorld(Vector2Int.zero, 2, 2);
            var placed = _service.Place(data, worldPos, 0, 0f);

            var occupiedCells = new List<(Vector2Int chunkCoord, int cellX, int cellY)>(placed.OccupiedCells);

            Assert.IsTrue(_service.Remove(placed));

            foreach (var (chunkCoord, cx, cy) in occupiedCells)
            {
                var chunk = _chunkSystem.GetChunk(chunkCoord);
                Assert.IsFalse(chunk.Cells[chunk.CellIndex(cx, cy)].Occupied);
            }
        }

        // --- TryGetAt ---

        [Test]
        public void TryGetAt_HitsAnyMaskCell_MissesNotchAndOutside()
        {
            var data = MakeLMask();
            var worldPos = _chunkSystem.CellToWorld(Vector2Int.zero, 2, 2);
            var placed = _service.Place(data, worldPos, 0, 0f);

            // Anchor cell (offset (0,0)) hits
            Assert.IsTrue(_service.TryGetAt(_chunkSystem.CellToWorld(Vector2Int.zero, 2, 2), out var hitAnchor));
            Assert.AreSame(placed, hitAnchor);

            // Far corner cell (offset (1,1)) -> (3,3) hits
            Assert.IsTrue(_service.TryGetAt(_chunkSystem.CellToWorld(Vector2Int.zero, 3, 3), out var hitCorner));
            Assert.AreSame(placed, hitCorner);

            // Notch cell (offset (1,0) of the bounding box, not part of the L) -> (3,2) misses
            Assert.IsFalse(_service.TryGetAt(_chunkSystem.CellToWorld(Vector2Int.zero, 3, 2), out _));

            // Outside entirely
            Assert.IsFalse(_service.TryGetAt(_chunkSystem.CellToWorld(Vector2Int.zero, 6, 6), out _));
        }

        // --- Step90 rotation ---

        [Test]
        public void Step90_RotationSteps1_OccupiesRotatedSet()
        {
            var data = MakeLMask(rotation: RotationMode.Step90);
            var worldPos = _chunkSystem.CellToWorld(Vector2Int.zero, 2, 2);

            var placed = _service.Place(data, worldPos, 1, 0f);
            Assert.IsNotNull(placed);

            var mask = FootprintMask.Create(data.FootprintRows, data.GridSize);
            var expected = ResolveOffsets(_chunkSystem, worldPos, mask.Cells(1));

            var actual = new HashSet<(Vector2Int, int, int)>(placed.OccupiedCells);
            CollectionAssert.AreEquivalent(expected, actual);
        }
    }
}
