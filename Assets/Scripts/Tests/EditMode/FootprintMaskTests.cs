using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using WheatFarm.Core.Data;

namespace WheatFarm.Tests
{
    public class FootprintMaskTests
    {
        [Test]
        public void Cells_Rotation0_MatchesExpectedOffsets()
        {
            var mask = FootprintMask.Create(new[] { "XX.", "XXX" }, Vector2Int.one);

            CollectionAssert.AreEquivalent(
                new[]
                {
                    new Vector2Int(-1, 0), new Vector2Int(0, 0),
                    new Vector2Int(-1, 1), new Vector2Int(0, 1), new Vector2Int(1, 1),
                },
                mask.Cells(0));
        }

        [Test]
        public void Cells_Rotation1_MatchesExpectedOffsets()
        {
            var mask = FootprintMask.Create(new[] { "XX.", "XXX" }, Vector2Int.one);

            CollectionAssert.AreEquivalent(
                new[]
                {
                    new Vector2Int(0, -1), new Vector2Int(0, 0), new Vector2Int(0, 1),
                    new Vector2Int(1, -1), new Vector2Int(1, 0),
                },
                mask.Cells(1));
        }

        [Test]
        public void Cells_Rotation2_MatchesExpectedOffsets()
        {
            var mask = FootprintMask.Create(new[] { "XX.", "XXX" }, Vector2Int.one);

            CollectionAssert.AreEquivalent(
                new[]
                {
                    new Vector2Int(-1, 0), new Vector2Int(0, 0), new Vector2Int(1, 0),
                    new Vector2Int(0, 1), new Vector2Int(1, 1),
                },
                mask.Cells(2));
        }

        [Test]
        public void Cells_Rotation3_MatchesExpectedOffsets()
        {
            var mask = FootprintMask.Create(new[] { "XX.", "XXX" }, Vector2Int.one);

            CollectionAssert.AreEquivalent(
                new[]
                {
                    new Vector2Int(0, 0), new Vector2Int(0, 1),
                    new Vector2Int(1, -1), new Vector2Int(1, 0), new Vector2Int(1, 1),
                },
                mask.Cells(3));
        }

        [Test]
        public void Cells_Rotation4_EqualsRotation0()
        {
            var mask = FootprintMask.Create(new[] { "XX.", "XXX" }, Vector2Int.one);

            CollectionAssert.AreEquivalent(mask.Cells(0), mask.Cells(4));
        }

        [Test]
        public void Cells_RotationNegative1_EqualsRotation3()
        {
            var mask = FootprintMask.Create(new[] { "XX.", "XXX" }, Vector2Int.one);

            CollectionAssert.AreEquivalent(mask.Cells(3), mask.Cells(-1));
        }

        [Test]
        public void WidthAndHeight_MatchSourceRows()
        {
            var mask = FootprintMask.Create(new[] { "XX.", "XXX" }, Vector2Int.one);

            Assert.AreEqual(3, mask.Width);
            Assert.AreEqual(2, mask.Height);
        }

        [Test]
        public void Create_NullRows_FallsBackToSolidRectangle()
        {
            var mask = FootprintMask.Create(null, new Vector2Int(2, 3));

            Assert.AreEqual(2, mask.Width);
            Assert.AreEqual(3, mask.Height);

            // 2x3 rectangle, center cell = (floor((2-1)/2f), floor((3-1)/2f)) = (0, 1)
            var expected = new List<Vector2Int>();
            for (var y = 0; y < 3; y++)
            for (var x = 0; x < 2; x++)
                expected.Add(new Vector2Int(x, y) - new Vector2Int(0, 1));

            CollectionAssert.AreEquivalent(expected, mask.Cells(0));
        }

        [Test]
        public void Create_RaggedRows_LogsErrorAndFallsBack()
        {
            LogAssert.Expect(LogType.Error, new Regex("FootprintMask.*"));

            var mask = FootprintMask.Create(new[] { "XX", "X" }, new Vector2Int(1, 1));

            Assert.AreEqual(1, mask.Width);
            Assert.AreEqual(1, mask.Height);
            CollectionAssert.AreEquivalent(new[] { Vector2Int.zero }, mask.Cells(0));
        }

        [Test]
        public void Create_AllDotsRows_LogsErrorAndFallsBack()
        {
            LogAssert.Expect(LogType.Error, new Regex("FootprintMask.*"));

            var mask = FootprintMask.Create(new[] { "..", ".." }, new Vector2Int(1, 1));

            Assert.AreEqual(1, mask.Width);
            Assert.AreEqual(1, mask.Height);
            CollectionAssert.AreEquivalent(new[] { Vector2Int.zero }, mask.Cells(0));
        }

        [Test]
        public void Dilate_SingleCell_ReturnsEightSurroundingCellsExcludingInput()
        {
            var input = new[] { Vector2Int.zero };

            var result = FootprintMask.Dilate(input, 1);

            var expected = new List<Vector2Int>();
            for (var dy = -1; dy <= 1; dy++)
            for (var dx = -1; dx <= 1; dx++)
            {
                if (dx == 0 && dy == 0) continue;
                expected.Add(new Vector2Int(dx, dy));
            }

            CollectionAssert.AreEquivalent(expected, result);
            CollectionAssert.DoesNotContain(result, Vector2Int.zero);
        }

        [Test]
        public void Dilate_TwoAdjacentCells_ReturnsUnionRingWithoutInputs()
        {
            var input = new[] { new Vector2Int(0, 0), new Vector2Int(1, 0) };

            var result = FootprintMask.Dilate(input, 1);

            CollectionAssert.DoesNotContain(result, new Vector2Int(0, 0));
            CollectionAssert.DoesNotContain(result, new Vector2Int(1, 0));

            // Union of 3x3 neighborhoods around (0,0) and (1,0), excluding the inputs.
            var unionCells = new HashSet<Vector2Int>();
            foreach (var c in input)
            for (var dy = -1; dy <= 1; dy++)
            for (var dx = -1; dx <= 1; dx++)
                unionCells.Add(c + new Vector2Int(dx, dy));

            foreach (var c in input)
                unionCells.Remove(c);

            CollectionAssert.AreEquivalent(unionCells, result);
        }

        [Test]
        public void RasterizeRotated_Angle0_EqualsCells0()
        {
            var mask = FootprintMask.Create(new[] { "XX.", "XXX" }, Vector2Int.one);

            CollectionAssert.AreEquivalent(mask.Cells(0), mask.RasterizeRotated(0f));
        }

        [Test]
        public void RasterizeRotated_Angle90_EqualsCells1()
        {
            var mask = FootprintMask.Create(new[] { "XX.", "XXX" }, Vector2Int.one);

            CollectionAssert.AreEquivalent(mask.Cells(1), mask.RasterizeRotated(90f));
        }

        [Test]
        public void RasterizeRotated_Angle180WithinTolerance_EqualsCells2()
        {
            var mask = FootprintMask.Create(new[] { "XX.", "XXX" }, Vector2Int.one);

            CollectionAssert.AreEquivalent(mask.Cells(2), mask.RasterizeRotated(180.004f));
        }

        [Test]
        public void RasterizeRotated_Angle45_SanityBounds()
        {
            var mask = FootprintMask.Create(new[] { "XXX" }, Vector2Int.one);

            var result = mask.RasterizeRotated(45f);

            Assert.GreaterOrEqual(result.Count, 3);
            Assert.LessOrEqual(result.Count, 9);
            CollectionAssert.Contains(result, Vector2Int.zero);

            foreach (var cell in result)
            {
                Assert.LessOrEqual(Mathf.Abs(cell.x), 2);
                Assert.LessOrEqual(Mathf.Abs(cell.y), 2);
            }
        }
    }
}
