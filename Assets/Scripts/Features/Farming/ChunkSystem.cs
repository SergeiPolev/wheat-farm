using System;
using System.Collections.Generic;
using R3;
using UnityEngine;
using WheatFarm.Core.Data;

namespace WheatFarm.Farming
{
    /// <summary>
    /// Camera-facing rotation constants for flat crop/tree meshes.
    /// Isometric camera looks from ~(35°, 45°); meshes need ~165° base rotation
    /// with ±25° variance to appear front-facing without obvious uniformity.
    /// </summary>
    public static class CropRotation
    {
        public const float BaseAngle = 165f;
        public const float Variance = 25f;
    }

    public interface IChunkSystem
    {
        ReadOnlyReactiveProperty<int> UnlockedChunkCount { get; }
        float ChunkWorldSize { get; }
        int SubCellResolution { get; }
        float CellWorldSize { get; }

        ChunkData GetChunk(Vector2Int coord);
        ChunkData GetOrCreateChunk(Vector2Int coord);
        bool TryUnlockChunk(Vector2Int coord);
        IReadOnlyList<ChunkData> GetAllUnlockedChunks();

        Vector2Int WorldToChunkCoord(Vector3 worldPos);
        (Vector2Int chunkCoord, int cellX, int cellY) WorldToCell(Vector3 worldPos);
        Vector3 CellToWorld(Vector2Int chunkCoord, int cellX, int cellY);

        /// <summary>
        /// Bounds center for a chunk (used by DrawMeshInstancedIndirect).
        /// MeshProperties positions must be RELATIVE to this for the shader to work correctly.
        /// </summary>
        Vector3 ChunkBoundsCenter(Vector2Int chunkCoord);

        List<(Vector2Int chunkCoord, int cellX, int cellY)> GetCellsInRadius(Vector3 worldPos, float radius);

        /// <summary>
        /// Recompute ground neighbor flags (uv.w) for a cell and its 8 neighbors.
        /// Handles cross-chunk lookups so the shader doesn't need to read across buffers.
        /// Call after any ground state change (plant, water, harvest, uproot, etc.).
        /// </summary>
        void UpdateGroundNeighborFlags(Vector2Int chunkCoord, int cellX, int cellY);
    }

    public class ChunkSystem : IChunkSystem, IDisposable
    {
        private readonly Dictionary<Vector2Int, ChunkData> _chunks = new();
        private readonly ReactiveProperty<int> _unlockedCount = new(0);
        private readonly List<ChunkData> _unlockedChunksCache = new();
        private readonly List<(Vector2Int chunkCoord, int cellX, int cellY)> _cellsInRadiusCache = new();

        private static readonly int[] DxArr = { 0, 1, 0, -1, 1, 1, -1, -1 };
        private static readonly int[] DyArr = { 1, 0, -1, 0, 1, -1, -1, 1 };

        public ReadOnlyReactiveProperty<int> UnlockedChunkCount => _unlockedCount;
        public float ChunkWorldSize { get; }
        public int SubCellResolution { get; }
        public float CellWorldSize => ChunkWorldSize / SubCellResolution;

        public ChunkSystem(float chunkWorldSize = 4f, int subCellResolution = 8)
        {
            ChunkWorldSize = chunkWorldSize;
            SubCellResolution = subCellResolution;
        }

        public void Dispose()
        {
            _unlockedCount.Dispose();
        }

        public ChunkData GetChunk(Vector2Int coord)
        {
            return _chunks.GetValueOrDefault(coord);
        }

        public ChunkData GetOrCreateChunk(Vector2Int coord)
        {
            if (!_chunks.TryGetValue(coord, out var chunk))
            {
                chunk = new ChunkData(coord, SubCellResolution);
                _chunks[coord] = chunk;
                InitializeChunkMeshProps(chunk);
            }
            return chunk;
        }

        public bool TryUnlockChunk(Vector2Int coord)
        {
            var chunk = GetOrCreateChunk(coord);
            if (chunk.Unlocked) return false;

            chunk.Unlocked = true;
            chunk.Dirty = true;
            _unlockedChunksCache.Add(chunk);
            _unlockedCount.Value++;
            return true;
        }

        public IReadOnlyList<ChunkData> GetAllUnlockedChunks()
        {
            return _unlockedChunksCache;
        }

        public Vector2Int WorldToChunkCoord(Vector3 worldPos)
        {
            return new Vector2Int(
                Mathf.FloorToInt(worldPos.x / ChunkWorldSize),
                Mathf.FloorToInt(worldPos.z / ChunkWorldSize));
        }

        public (Vector2Int chunkCoord, int cellX, int cellY) WorldToCell(Vector3 worldPos)
        {
            var chunkCoord = WorldToChunkCoord(worldPos);
            float localX = worldPos.x - chunkCoord.x * ChunkWorldSize;
            float localZ = worldPos.z - chunkCoord.y * ChunkWorldSize;
            int cellX = Mathf.Clamp(Mathf.FloorToInt(localX / CellWorldSize), 0, SubCellResolution - 1);
            int cellY = Mathf.Clamp(Mathf.FloorToInt(localZ / CellWorldSize), 0, SubCellResolution - 1);
            return (chunkCoord, cellX, cellY);
        }

        public Vector3 CellToWorld(Vector2Int chunkCoord, int cellX, int cellY)
        {
            float x = chunkCoord.x * ChunkWorldSize + (cellX + 0.5f) * CellWorldSize;
            float z = chunkCoord.y * ChunkWorldSize + (cellY + 0.5f) * CellWorldSize;
            return new Vector3(x, 0f, z);
        }

        public Vector3 ChunkBoundsCenter(Vector2Int chunkCoord)
        {
            return new Vector3(
                (chunkCoord.x + 0.5f) * ChunkWorldSize,
                0f,
                (chunkCoord.y + 0.5f) * ChunkWorldSize);
        }

        public List<(Vector2Int chunkCoord, int cellX, int cellY)> GetCellsInRadius(Vector3 worldPos, float radius)
        {
            _cellsInRadiusCache.Clear();
            float sqrRadius = radius * radius;

            // Find chunk range that the radius covers
            var minCoord = WorldToChunkCoord(worldPos - new Vector3(radius, 0, radius));
            var maxCoord = WorldToChunkCoord(worldPos + new Vector3(radius, 0, radius));

            for (int cx = minCoord.x; cx <= maxCoord.x; cx++)
            {
                for (int cy = minCoord.y; cy <= maxCoord.y; cy++)
                {
                    var coord = new Vector2Int(cx, cy);
                    var chunk = GetChunk(coord);
                    if (chunk == null || !chunk.Unlocked) continue;

                    for (int x = 0; x < SubCellResolution; x++)
                    {
                        for (int y = 0; y < SubCellResolution; y++)
                        {
                            var cellWorld = CellToWorld(coord, x, y);
                            float dx = cellWorld.x - worldPos.x;
                            float dz = cellWorld.z - worldPos.z;
                            if (dx * dx + dz * dz <= sqrRadius)
                            {
                                _cellsInRadiusCache.Add((coord, x, y));
                            }
                        }
                    }
                }
            }

            return _cellsInRadiusCache;
        }

        /// <summary>Radius (in cells) for grass proximity blending around farmland.</summary>
        private const int ProximityRadius = 2;

        public void UpdateGroundNeighborFlags(Vector2Int chunkCoord, int cellX, int cellY)
        {
            // Update a (ProximityRadius+1) ring around the changed cell:
            // - Farmed cells get 8-bit neighbor flags
            // - Grass cells get proximity-to-farmland (0..1) for transition blending
            int range = ProximityRadius + 1; // +1 so edge-flag neighbors of farmland are also updated
            for (int dy = -range; dy <= range; dy++)
            {
                for (int dx = -range; dx <= range; dx++)
                {
                    ResolveCell(chunkCoord, cellX + dx, cellY + dy,
                        out var nChunkCoord, out int nx, out int ny);

                    var nChunk = GetChunk(nChunkCoord);
                    if (nChunk == null || !nChunk.Unlocked) continue;

                    int idx = nChunk.CellIndex(nx, ny);
                    ref var props = ref nChunk.MeshProps[idx];

                    if (nChunk.Cells[idx].GroundState != GroundState.Grass)
                    {
                        // Farmed cell: store 8-bit neighbor flags
                        props.uv.w = ComputeNeighborFlags(nChunkCoord, nx, ny);
                    }
                    else
                    {
                        // Grass cell: store proximity + direction to nearest farmland
                        ComputeFarmlandProximity(nChunkCoord, nx, ny,
                            out float prox, out float nearDx, out float nearDy);
                        props.uv.w = prox;
                        // Store offset to nearest farmland in color.xy
                        // (color is unused for grass cells — no plant to tint)
                        props.color = new Vector4(nearDx, nearDy, 0, 0);
                    }
                    nChunk.Dirty = true;
                }
            }
        }

        /// <summary>
        /// Resolve cell coordinates that may be outside chunk bounds into
        /// the correct chunk + local cell coords (cross-chunk wrapping).
        /// </summary>
        private void ResolveCell(Vector2Int chunkCoord, int cellX, int cellY,
            out Vector2Int outChunk, out int outX, out int outY)
        {
            outChunk = chunkCoord;
            outX = cellX;
            outY = cellY;

            if (outX < 0) { outChunk.x--; outX += SubCellResolution; }
            else if (outX >= SubCellResolution) { outChunk.x++; outX -= SubCellResolution; }
            if (outY < 0) { outChunk.y--; outY += SubCellResolution; }
            else if (outY >= SubCellResolution) { outChunk.y++; outY -= SubCellResolution; }
        }

        /// <summary>
        /// Compute 8-bit neighbor flags for a farmed cell. Each bit = 1 if that neighbor has ground state > 0.
        /// Bit layout: 0=N(+Y), 1=E(+X), 2=S(-Y), 3=W(-X), 4=NE, 5=SE, 6=SW, 7=NW.
        /// Handles cross-chunk lookups.
        /// </summary>
        private float ComputeNeighborFlags(Vector2Int chunkCoord, int cellX, int cellY)
        {
            int flags = 0;

            for (int bit = 0; bit < 8; bit++)
            {
                ResolveCell(chunkCoord, cellX + DxArr[bit], cellY + DyArr[bit],
                    out var nChunkCoord, out int nx, out int ny);

                var nChunk = GetChunk(nChunkCoord);
                if (nChunk != null && nChunk.Unlocked)
                {
                    int idx = nChunk.CellIndex(nx, ny);
                    if (nChunk.Cells[idx].GroundState != GroundState.Grass)
                        flags |= (1 << bit);
                }
            }

            return (float)flags;
        }

        /// <summary>
        /// For a grass cell, find the nearest farmed cell within ProximityRadius.
        /// Stores proximity (0..1) in uv.w and the cell offset (dx,dy) to nearest
        /// farmland in color.xy (so the shader can compute per-pixel distance).
        /// </summary>
        private void ComputeFarmlandProximity(Vector2Int chunkCoord, int cellX, int cellY,
            out float proximity, out float nearestDx, out float nearestDy)
        {
            float minDistSq = (ProximityRadius + 1) * (ProximityRadius + 1);
            int bestDx = 0, bestDy = 0;
            bool found = false;

            for (int dy = -ProximityRadius; dy <= ProximityRadius; dy++)
            {
                for (int dx = -ProximityRadius; dx <= ProximityRadius; dx++)
                {
                    if (dx == 0 && dy == 0) continue;

                    ResolveCell(chunkCoord, cellX + dx, cellY + dy,
                        out var nChunkCoord, out int nx, out int ny);

                    var nChunk = GetChunk(nChunkCoord);
                    if (nChunk == null || !nChunk.Unlocked) continue;

                    int idx = nChunk.CellIndex(nx, ny);
                    if (nChunk.Cells[idx].GroundState != GroundState.Grass)
                    {
                        float distSq = dx * dx + dy * dy;
                        if (distSq < minDistSq)
                        {
                            minDistSq = distSq;
                            bestDx = dx;
                            bestDy = dy;
                            found = true;
                        }
                    }
                }
            }

            if (!found)
            {
                proximity = 0f;
                nearestDx = 0f;
                nearestDy = 0f;
                return;
            }

            float dist = Mathf.Sqrt(minDistSq);
            // 1.0 at distance 0.5 (touching), 0.0 at ProximityRadius+0.5
            proximity = Mathf.Clamp01(1f - (dist - 0.5f) / (ProximityRadius + 0.5f));
            nearestDx = bestDx;
            nearestDy = bestDy;
        }

        private void InitializeChunkMeshProps(ChunkData chunk)
        {
            float cellSize = CellWorldSize;
            // Positions in MeshProperties must be RELATIVE to bounds center
            // because the shader does: objectToWorld = mul(objectToWorld, data.m)
            // and objectToWorld already contains the bounds-based transform
            Vector3 boundsCenter = ChunkBoundsCenter(chunk.ChunkCoord);

            for (int x = 0; x < chunk.Resolution; x++)
            {
                for (int y = 0; y < chunk.Resolution; y++)
                {
                    int idx = chunk.CellIndex(x, y);
                    var worldPos = CellToWorld(chunk.ChunkCoord, x, y);
                    var relativePos = worldPos - boundsCenter;

                    // Small random offset for natural look
                    var randomOffset = new Vector3(
                        UnityEngine.Random.Range(-cellSize * 0.2f, cellSize * 0.2f),
                        0f,
                        UnityEngine.Random.Range(-cellSize * 0.2f, cellSize * 0.2f));

                    var props = new MeshProperties();
                    props.m = Matrix4x4.TRS(
                        relativePos + randomOffset,
                        Quaternion.Euler(0, CropRotation.BaseAngle + UnityEngine.Random.Range(-CropRotation.Variance, CropRotation.Variance), 0),
                        Vector3.one * UnityEngine.Random.Range(0.8f, 1.2f));
                    // Ground tile: flat quad covering the cell, slightly above ground plane
                    // Quad mesh faces +Z in local space; rotate 90° around X to face +Y (up)
                    // Exact cell size — edge softening is done in UV space within the tile
                    float groundScale = cellSize;
                    props.gr = Matrix4x4.TRS(
                        relativePos + Vector3.up * 0.01f,
                        Quaternion.Euler(90, 0, 0),
                        new Vector3(groundScale, groundScale, groundScale));
                    // color = (0,0,0,0) for grass cells (shader reads color.xy as farmDir)
                    // Plant() sets color to plant tint; UpdateGroundNeighborFlags sets
                    // color.xy to nearest-farmland offset for proximity grass cells
                    props.color = Vector4.zero;
                    props.uv = new Vector4(
                        (float)x / chunk.Resolution,
                        (float)y / chunk.Resolution,
                        1f / chunk.Resolution,
                        0f); // uv.w = 0: no neighbor flags / no proximity
                    props.cropState = Vector4.zero;

                    chunk.MeshProps[idx] = props;
                }
            }
        }
    }
}
