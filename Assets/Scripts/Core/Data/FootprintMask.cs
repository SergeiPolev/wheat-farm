using System.Collections.Generic;
using UnityEngine;

namespace WheatFarm.Core.Data
{
    /// <summary>
    /// Pure-C# occupancy mask for "freeform footprint" placeables.
    /// Cells are stored as offsets relative to the mask's center cell, with
    /// all 4 clockwise (in mask-string space) 90-degree rotations precomputed.
    /// </summary>
    public class FootprintMask
    {
        private readonly IReadOnlyList<Vector2Int>[] _rotations;
        private readonly int[] _rotationWidths;
        private readonly int[] _rotationHeights;

        public int Width { get; }
        public int Height { get; }

        private FootprintMask(List<Vector2Int> baseCells, int width, int height)
        {
            Width = width;
            Height = height;

            _rotations = new IReadOnlyList<Vector2Int>[4];
            _rotationWidths = new int[4];
            _rotationHeights = new int[4];

            var cells = baseCells;
            var w = width;
            var h = height;

            for (var step = 0; step < 4; step++)
            {
                _rotationWidths[step] = w;
                _rotationHeights[step] = h;
                _rotations[step] = ToOffsets(cells, w, h);

                // Rotate for the next step: (x, y) -> (H-1-y, x), dims swap W<->H.
                var rotated = new List<Vector2Int>(cells.Count);
                foreach (var cell in cells)
                    rotated.Add(new Vector2Int(h - 1 - cell.y, cell.x));

                cells = rotated;
                var newW = h;
                var newH = w;
                w = newW;
                h = newH;
            }
        }

        private static List<Vector2Int> ToOffsets(List<Vector2Int> cells, int width, int height)
        {
            var centerCell = new Vector2Int(
                Mathf.FloorToInt((width - 1) / 2f),
                Mathf.FloorToInt((height - 1) / 2f));

            var offsets = new List<Vector2Int>(cells.Count);
            foreach (var cell in cells)
                offsets.Add(cell - centerCell);

            return offsets;
        }

        /// <summary>
        /// Returns the cell offsets (relative to the mask's center cell) for the given
        /// number of clockwise 90-degree rotation steps. Wraps modulo 4 (negative allowed).
        /// </summary>
        public IReadOnlyList<Vector2Int> Cells(int rotSteps)
        {
            var step = ((rotSteps % 4) + 4) % 4;
            return _rotations[step];
        }

        /// <summary>
        /// Returns all cells within Chebyshev distance &lt;= padding of any input cell,
        /// excluding the input cells themselves (ring only).
        /// </summary>
        public static List<Vector2Int> Dilate(IReadOnlyList<Vector2Int> cells, int padding)
        {
            var input = new HashSet<Vector2Int>();
            foreach (var c in cells)
                input.Add(c);

            var result = new HashSet<Vector2Int>();
            foreach (var c in cells)
            {
                for (var dy = -padding; dy <= padding; dy++)
                for (var dx = -padding; dx <= padding; dx++)
                {
                    if (dx == 0 && dy == 0) continue;
                    var candidate = new Vector2Int(c.x + dx, c.y + dy);
                    if (!input.Contains(candidate))
                        result.Add(candidate);
                }
            }

            return new List<Vector2Int>(result);
        }

        /// <summary>
        /// Conservative rasterization of this mask rotated by an arbitrary angle (degrees,
        /// clockwise). If the angle is a multiple of 90 (within 0.01 degrees), delegates to
        /// <see cref="Cells"/>. Offsets are relative to the rotated mask's center cell.
        /// Allocates a fresh list per call.
        /// </summary>
        public IReadOnlyList<Vector2Int> RasterizeRotated(float angleDeg)
        {
            var normalized = ((angleDeg % 360f) + 360f) % 360f;
            var nearestStep = Mathf.RoundToInt(normalized / 90f) % 4;
            if (Mathf.Abs(normalized - nearestStep * 90f) <= 0.01f)
                return Cells(nearestStep);

            // Reconstruct the rotation-0 occupied cells (0-based, mask-string space).
            var center = new Vector2(Width - 1, Height - 1) * 0.5f;
            var baseCells = _rotations[0];
            var baseCenterCell = new Vector2Int(
                Mathf.FloorToInt((Width - 1) / 2f),
                Mathf.FloorToInt((Height - 1) / 2f));

            var occupied = new HashSet<Vector2Int>();
            foreach (var offset in baseCells)
                occupied.Add(offset + baseCenterCell);

            var radians = -angleDeg * Mathf.Deg2Rad; // clockwise rotation of the grid

            Vector2 RotateCW(Vector2 p)
            {
                var rel = p - center;
                var cos = Mathf.Cos(radians);
                var sin = Mathf.Sin(radians);
                var rotated = new Vector2(
                    rel.x * cos - rel.y * sin,
                    rel.x * sin + rel.y * cos);
                return rotated + center;
            }

            Vector2 InverseRotateCW(Vector2 p)
            {
                var rel = p - center;
                var cos = Mathf.Cos(-radians);
                var sin = Mathf.Sin(-radians);
                var rotated = new Vector2(
                    rel.x * cos - rel.y * sin,
                    rel.x * sin + rel.y * cos);
                return rotated + center;
            }

            // AABB of rotated occupied cells, expanded by 1 candidate cell.
            var minX = float.MaxValue;
            var minY = float.MaxValue;
            var maxX = float.MinValue;
            var maxY = float.MinValue;

            foreach (var cell in occupied)
            {
                for (var cy = 0; cy <= 1; cy++)
                for (var cx = 0; cx <= 1; cx++)
                {
                    var corner = new Vector2(cell.x + cx, cell.y + cy);
                    var rotatedCorner = RotateCW(corner);
                    minX = Mathf.Min(minX, rotatedCorner.x);
                    minY = Mathf.Min(minY, rotatedCorner.y);
                    maxX = Mathf.Max(maxX, rotatedCorner.x);
                    maxY = Mathf.Max(maxY, rotatedCorner.y);
                }
            }

            var minCellX = Mathf.FloorToInt(minX) - 1;
            var minCellY = Mathf.FloorToInt(minY) - 1;
            var maxCellX = Mathf.CeilToInt(maxX) + 1;
            var maxCellY = Mathf.CeilToInt(maxY) + 1;

            const float inset = 0.1f;

            var result = new List<Vector2Int>();

            for (var cy = minCellY; cy < maxCellY; cy++)
            {
                for (var cx = minCellX; cx < maxCellX; cx++)
                {
                    var samples = new[]
                    {
                        new Vector2(cx + 0.5f, cy + 0.5f), // center
                        new Vector2(cx + inset, cy + inset), // top-left
                        new Vector2(cx + 1f - inset, cy + inset), // top-right
                        new Vector2(cx + inset, cy + 1f - inset), // bottom-left
                        new Vector2(cx + 1f - inset, cy + 1f - inset), // bottom-right
                    };

                    var hit = false;
                    foreach (var sample in samples)
                    {
                        var maskSpace = InverseRotateCW(sample);
                        var maskCell = new Vector2Int(Mathf.FloorToInt(maskSpace.x), Mathf.FloorToInt(maskSpace.y));
                        if (occupied.Contains(maskCell))
                        {
                            hit = true;
                            break;
                        }
                    }

                    if (hit)
                        result.Add(new Vector2Int(cx, cy));
                }
            }

            // Re-anchor: cell containing the rotated float center gets offset (0,0).
            var rotatedCenter = RotateCW(center);
            var rotatedCenterCell = new Vector2Int(
                Mathf.FloorToInt(rotatedCenter.x),
                Mathf.FloorToInt(rotatedCenter.y));

            for (var i = 0; i < result.Count; i++)
                result[i] -= rotatedCenterCell;

            return result;
        }

        /// <summary>
        /// Builds a mask from a string representation ('X'/'x' = occupied, '.' = free).
        /// Falls back to a solid rectangle of <paramref name="gridSizeFallback"/> dimensions if
        /// <paramref name="rows"/> is null/empty, ragged, or contains invalid characters.
        /// </summary>
        public static FootprintMask Create(string[] rows, Vector2Int gridSizeFallback)
        {
            if (rows == null || rows.Length == 0)
                return CreateFallback(gridSizeFallback);

            var width = rows[0]?.Length ?? 0;
            var height = rows.Length;

            if (width == 0)
            {
                Debug.LogError("FootprintMask.Create: rows must not be empty; falling back to rectangle.");
                return CreateFallback(gridSizeFallback);
            }

            var cells = new List<Vector2Int>();

            for (var y = 0; y < height; y++)
            {
                var row = rows[y];
                if (row == null || row.Length != width)
                {
                    Debug.LogError("FootprintMask.Create: ragged rows are not supported; falling back to rectangle.");
                    return CreateFallback(gridSizeFallback);
                }

                for (var x = 0; x < width; x++)
                {
                    var c = row[x];
                    switch (c)
                    {
                        case 'X':
                        case 'x':
                            cells.Add(new Vector2Int(x, y));
                            break;
                        case '.':
                            break;
                        default:
                            Debug.LogError($"FootprintMask.Create: invalid character '{c}' at ({x},{y}); falling back to rectangle.");
                            return CreateFallback(gridSizeFallback);
                    }
                }
            }

            return new FootprintMask(cells, width, height);
        }

        private static FootprintMask CreateFallback(Vector2Int gridSizeFallback)
        {
            var width = Mathf.Max(1, gridSizeFallback.x);
            var height = Mathf.Max(1, gridSizeFallback.y);

            var cells = new List<Vector2Int>(width * height);
            for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
                cells.Add(new Vector2Int(x, y));

            return new FootprintMask(cells, width, height);
        }
    }
}
