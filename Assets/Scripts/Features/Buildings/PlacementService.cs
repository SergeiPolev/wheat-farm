using System.Collections.Generic;
using ObservableCollections;
using UnityEngine;
using WheatFarm.Core.Data;using WheatFarm.Core;

using WheatFarm.Economy;
using WheatFarm.Farming;

namespace WheatFarm.Buildings
{
    /// <summary>Result of evaluating a placeable's footprint at a candidate position.</summary>
    public struct FootprintEval
    {
        public bool AllOk;

        /// <summary>World-space center of the mask cells' bounding box, y=0.</summary>
        public Vector3 BBoxCenter;
    }

    /// <summary>Runtime data for a placed object (building, decor). Paths are tracked via cell GroundState only.</summary>
    public class PlacedObject
    {
        public PlaceableData Data;

        /// <summary>Anchor cell's chunk.</summary>
        public Vector2Int ChunkCoord;

        /// <summary>Anchor cell within its chunk.</summary>
        public int CellX, CellY;

        /// <summary>Step90 footprint rotation, 0..3.</summary>
        public int RotationSteps;

        /// <summary>Visual rotation in degrees. Free5 also uses this for rasterization.</summary>
        public float RotationY;

        /// <summary>Upgrade level — separate from footprint/rotation concerns.</summary>
        public int Level = 1;

        public GameObject Instance;

        public List<(Vector2Int chunkCoord, int cellX, int cellY)> OccupiedCells = new();
    }

    public interface IPlacementService
    {
        ObservableList<PlacedObject> PlacedObjects { get; }

        PlacedObject Place(PlaceableData data, Vector3 worldPos, int rotationSteps, float rotationY);
        bool CanPlace(PlaceableData data, Vector3 worldPos, int rotationSteps, float rotationY);

        /// <summary>
        /// Evaluates every footprint cell (and any blocking padding cells) for the given placement.
        /// Fills <paramref name="result"/> (cleared first) with world positions and per-cell validity.
        /// </summary>
        FootprintEval EvaluateFootprint(PlaceableData data, Vector3 worldPos, int rotationSteps, float rotationY,
            List<(Vector3 worldPos, bool ok)> result);

        bool TryGetAt(Vector3 worldPos, out PlacedObject obj);
        bool Remove(PlacedObject obj);
        PlacedObject RestorePlace(PlaceableData data, Vector2Int chunkCoord, int cellX, int cellY, int rotationSteps, float rotationY, int level);
    }

    public class PlacementService : IPlacementService
    {
        private const float RefundRatio = 0.5f;

        private readonly IChunkSystem _chunkSystem;
        private readonly IWalletService _wallet;
        private readonly IFeedbackService _feedback;

        private readonly Dictionary<PlaceableData, FootprintMask> _footprintCache = new();

        // Scratch buffers reused across calls to avoid per-call allocations.
        private readonly List<Vector2Int> _scratchOffsets = new();
        private readonly List<Vector2Int> _scratchPadding = new();
        private readonly List<(Vector3 worldPos, bool ok)> _scratchEval = new();

        public ObservableList<PlacedObject> PlacedObjects { get; } = new();

        public PlacementService(IChunkSystem chunkSystem, IWalletService wallet, IFeedbackService feedback = null)
        {
            _chunkSystem = chunkSystem;
            _wallet = wallet;
            _feedback = feedback;
        }

        // --- Footprint resolution ---

        private FootprintMask GetMask(PlaceableData data)
        {
            if (!_footprintCache.TryGetValue(data, out var mask))
            {
                mask = FootprintMask.Create(data.FootprintRows, data.GridSize);
                _footprintCache[data] = mask;
            }
            return mask;
        }

        /// <summary>Cell offsets (relative to the anchor cell) for this placeable's footprint at the given rotation.</summary>
        private IReadOnlyList<Vector2Int> GetOffsets(PlaceableData data, int rotationSteps, float rotationY)
        {
            var mask = GetMask(data);
            return data.Rotation switch
            {
                RotationMode.Step90 => mask.Cells(rotationSteps),
                RotationMode.Free5 => mask.RasterizeRotated(rotationY),
                _ => mask.Cells(0),
            };
        }

        /// <summary>Resolves an offset cell (relative to the anchor) to its absolute chunk/cell coordinates.</summary>
        private (Vector2Int chunkCoord, int cellX, int cellY) ResolveOffset(Vector3 anchorWorld, Vector2Int offset)
        {
            var offsetWorld = anchorWorld + new Vector3(offset.x, 0, offset.y) * _chunkSystem.CellWorldSize;
            return _chunkSystem.WorldToCell(offsetWorld);
        }

        // --- Evaluation ---

        public FootprintEval EvaluateFootprint(PlaceableData data, Vector3 worldPos, int rotationSteps, float rotationY,
            List<(Vector3 worldPos, bool ok)> result)
        {
            result.Clear();

            var eval = new FootprintEval { AllOk = true, BBoxCenter = Vector3.zero };
            if (data == null)
            {
                eval.AllOk = false;
                return eval;
            }

            var (anchorChunk, ax, ay) = _chunkSystem.WorldToCell(worldPos);
            var anchorWorld = _chunkSystem.CellToWorld(anchorChunk, ax, ay);

            var offsets = GetOffsets(data, rotationSteps, rotationY);

            float minX = float.MaxValue, maxX = float.MinValue;
            float minZ = float.MaxValue, maxZ = float.MinValue;

            foreach (var off in offsets)
            {
                var (chunkCoord, cx, cy) = ResolveOffset(anchorWorld, off);
                var cellWorld = _chunkSystem.CellToWorld(chunkCoord, cx, cy);

                bool ok;
                var chunk = _chunkSystem.GetChunk(chunkCoord);
                if (chunk == null || !chunk.Unlocked)
                {
                    ok = false;
                }
                else
                {
                    ref var cell = ref chunk.Cells[chunk.CellIndex(cx, cy)];
                    ok = !cell.Occupied && !cell.HasPlant;
                }

                if (!ok) eval.AllOk = false;

                result.Add((cellWorld, ok));

                minX = Mathf.Min(minX, cellWorld.x);
                maxX = Mathf.Max(maxX, cellWorld.x);
                minZ = Mathf.Min(minZ, cellWorld.z);
                maxZ = Mathf.Max(maxZ, cellWorld.z);
            }

            eval.BBoxCenter = new Vector3((minX + maxX) * 0.5f, 0f, (minZ + maxZ) * 0.5f);

            // Padding ring: blocks placement only if an occupied cell in an existing unlocked chunk.
            if (data.PaddingCells > 0)
            {
                _scratchPadding.Clear();
                _scratchPadding.AddRange(FootprintMask.Dilate(offsets, data.PaddingCells));

                foreach (var off in _scratchPadding)
                {
                    var (chunkCoord, cx, cy) = ResolveOffset(anchorWorld, off);
                    var chunk = _chunkSystem.GetChunk(chunkCoord);
                    if (chunk == null || !chunk.Unlocked) continue;

                    ref var cell = ref chunk.Cells[chunk.CellIndex(cx, cy)];
                    if (cell.Occupied)
                    {
                        eval.AllOk = false;
                        var cellWorld = _chunkSystem.CellToWorld(chunkCoord, cx, cy);
                        result.Add((cellWorld, false));
                    }
                }
            }

            return eval;
        }

        public bool CanPlace(PlaceableData data, Vector3 worldPos, int rotationSteps, float rotationY)
        {
            if (data == null) return false;
            if (data.Category == PlaceableCategory.Path) return false; // paths use brush, not this method

            return EvaluateFootprint(data, worldPos, rotationSteps, rotationY, _scratchEval).AllOk;
        }

        // --- Place / Remove ---

        public PlacedObject Place(PlaceableData data, Vector3 worldPos, int rotationSteps, float rotationY)
        {
            if (data == null) return null;
            if (!CanPlace(data, worldPos, rotationSteps, rotationY)) return null;
            if (!_wallet.TrySpend(data.Cost)) return null;

            var (anchorChunk, ax, ay) = _chunkSystem.WorldToCell(worldPos);
            var anchorWorld = _chunkSystem.CellToWorld(anchorChunk, ax, ay);
            var offsets = GetOffsets(data, rotationSteps, rotationY);

            var placed = new PlacedObject
            {
                Data = data,
                ChunkCoord = anchorChunk,
                CellX = ax,
                CellY = ay,
                RotationSteps = rotationSteps,
                RotationY = rotationY,
                Level = 1,
            };

            float minX = float.MaxValue, maxX = float.MinValue;
            float minZ = float.MaxValue, maxZ = float.MinValue;

            foreach (var off in offsets)
            {
                var (chunkCoord, cx, cy) = ResolveOffset(anchorWorld, off);
                var chunk = _chunkSystem.GetChunk(chunkCoord);
                if (chunk == null) continue;

                chunk.Cells[chunk.CellIndex(cx, cy)].Occupied = true;
                chunk.Dirty = true;
                placed.OccupiedCells.Add((chunkCoord, cx, cy));

                var cellWorld = _chunkSystem.CellToWorld(chunkCoord, cx, cy);
                minX = Mathf.Min(minX, cellWorld.x);
                maxX = Mathf.Max(maxX, cellWorld.x);
                minZ = Mathf.Min(minZ, cellWorld.z);
                maxZ = Mathf.Max(maxZ, cellWorld.z);
            }

            var spawnPos = new Vector3((minX + maxX) * 0.5f, 0f, (minZ + maxZ) * 0.5f);

            if (data.Prefab != null)
            {
                placed.Instance = Object.Instantiate(data.Prefab, spawnPos, Quaternion.Euler(0, rotationY, 0));

                if (data.Interactable)
                {
                    var marker = placed.Instance.AddComponent<BuildingMarker>();
                    marker.PlacedObject = placed;
                }
            }

            PlacedObjects.Add(placed);

            _feedback?.PlayEffect(FarmFxType.Build, spawnPos);

            return placed;
        }

        public bool Remove(PlacedObject obj)
        {
            if (obj == null) return false;

            foreach (var (chunkCoord, cx, cy) in obj.OccupiedCells)
            {
                var chunk = _chunkSystem.GetChunk(chunkCoord);
                if (chunk == null) continue;

                chunk.Cells[chunk.CellIndex(cx, cy)].Occupied = false;
                chunk.Dirty = true;
            }

            if (obj.Instance != null)
            {
                _feedback?.PlayEffect(FarmFxType.Remove, obj.Instance.transform.position);
                Object.Destroy(obj.Instance);
            }

            PlacedObjects.Remove(obj);

            // Partial refund
            int refund = Mathf.FloorToInt(obj.Data.Cost * RefundRatio);
            if (refund > 0) _wallet.Add(refund);

            return true;
        }

        // --- TryGetAt ---

        public bool TryGetAt(Vector3 worldPos, out PlacedObject obj)
        {
            var probe = _chunkSystem.WorldToCell(worldPos);

            foreach (var placed in PlacedObjects)
            {
                foreach (var cell in placed.OccupiedCells)
                {
                    if (cell == probe)
                    {
                        obj = placed;
                        return true;
                    }
                }
            }

            obj = null;
            return false;
        }

        // --- Restore (save/load) ---

        /// <summary>
        /// Restore a placed object during save-load without spending coins or validation.
        /// </summary>
        public PlacedObject RestorePlace(PlaceableData data, Vector2Int chunkCoord, int cellX, int cellY,
            int rotationSteps, float rotationY, int level)
        {
            if (data == null) return null;

            var anchorWorld = _chunkSystem.CellToWorld(chunkCoord, cellX, cellY);
            var offsets = GetOffsets(data, rotationSteps, rotationY);

            var placed = new PlacedObject
            {
                Data = data,
                ChunkCoord = chunkCoord,
                CellX = cellX,
                CellY = cellY,
                RotationSteps = rotationSteps,
                RotationY = rotationY,
                Level = level,
            };

            float minX = float.MaxValue, maxX = float.MinValue;
            float minZ = float.MaxValue, maxZ = float.MinValue;

            foreach (var off in offsets)
            {
                var (occChunkCoord, cx, cy) = ResolveOffset(anchorWorld, off);
                var chunk = _chunkSystem.GetChunk(occChunkCoord);
                if (chunk == null) continue;

                chunk.Cells[chunk.CellIndex(cx, cy)].Occupied = true;
                chunk.Dirty = true;
                placed.OccupiedCells.Add((occChunkCoord, cx, cy));

                var cellWorld = _chunkSystem.CellToWorld(occChunkCoord, cx, cy);
                minX = Mathf.Min(minX, cellWorld.x);
                maxX = Mathf.Max(maxX, cellWorld.x);
                minZ = Mathf.Min(minZ, cellWorld.z);
                maxZ = Mathf.Max(maxZ, cellWorld.z);
            }

            if (data.Prefab != null)
            {
                var spawnPos = new Vector3((minX + maxX) * 0.5f, 0f, (minZ + maxZ) * 0.5f);
                placed.Instance = Object.Instantiate(data.Prefab, spawnPos, Quaternion.Euler(0, rotationY, 0));

                if (data.Interactable)
                {
                    var marker = placed.Instance.AddComponent<BuildingMarker>();
                    marker.PlacedObject = placed;
                }
            }

            PlacedObjects.Add(placed);
            return placed;
        }
    }
}
