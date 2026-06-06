using System;
using System.Collections.Generic;
using UnityEngine;
using WheatFarm.Core.Data;

namespace WheatFarm.Farming
{
    /// <summary>
    /// Per-chunk GPU instanced indirect renderer.
    /// Each unlocked chunk gets its own ChunkCropRenderer with dedicated ComputeBuffers.
    /// Issues multiple draw calls per frame:
    ///   1. Ground tiles (flat quads using gr matrix, always visible) — 1 call
    ///   2. Crops — one call per CropMeshEntry (plant meshes using m matrix, filtered by cropState.x == _Id)
    /// All passes share the same _PerInstanceData ComputeBuffer.
    /// </summary>
    public class ChunkCropRenderer : IDisposable
    {
        private static readonly int PerInstanceData = Shader.PropertyToID("_PerInstanceData");

        private readonly ChunkData _chunk;
        private readonly Bounds _bounds;

        // Shared buffer (all passes read from this)
        private ComputeBuffer _meshPropsBuffer;

        // Crop passes (one per mesh type)
        private readonly List<CropPass> _cropPasses = new();

        // Ground tile pass (optional — null if not configured)
        private ComputeBuffer _groundArgsBuffer;
        private Material _groundMaterial;
        private Mesh _groundMesh;

        public ChunkData Chunk => _chunk;

        private struct CropPass
        {
            public int MeshId;
            public Mesh Mesh;
            public Material Material;
            public ComputeBuffer ArgsBuffer;
        }

        public ChunkCropRenderer(
            ChunkData chunk,
            CropMeshEntry[] cropEntries,
            Mesh groundMesh, Material groundSharedMaterial,
            float chunkWorldSize)
        {
            _chunk = chunk;

            if (groundMesh != null && groundSharedMaterial != null)
            {
                _groundMesh = groundMesh;
                _groundMaterial = new Material(groundSharedMaterial);
            }

            // Bounds centered on the chunk's world position (used for frustum culling)
            var center = new Vector3(
                (chunk.ChunkCoord.x + 0.5f) * chunkWorldSize,
                0f,
                (chunk.ChunkCoord.y + 0.5f) * chunkWorldSize);
            _bounds = new Bounds(center, new Vector3(chunkWorldSize, 2f, chunkWorldSize));

            InitializeBuffers(cropEntries);
        }

        private void InitializeBuffers(CropMeshEntry[] cropEntries)
        {
            int count = _chunk.CellCount;

            // Shared mesh properties buffer (StructuredBuffer in shader)
            _meshPropsBuffer = new ComputeBuffer(count, MeshProperties.Size());
            _meshPropsBuffer.SetData(_chunk.MeshProps);

            // Crop passes — one per mesh entry
            foreach (var entry in cropEntries)
            {
                if (entry.Mesh == null || entry.Material == null) continue;

                var pass = new CropPass
                {
                    MeshId = entry.MeshId,
                    Mesh = entry.Mesh,
                    Material = new Material(entry.Material),
                    ArgsBuffer = CreateArgsBuffer(entry.Mesh, count)
                };
                pass.Material.SetBuffer(PerInstanceData, _meshPropsBuffer);
                _cropPasses.Add(pass);
            }

            // Ground indirect args (if ground rendering enabled)
            if (_groundMesh != null && _groundMaterial != null)
            {
                _groundArgsBuffer = CreateArgsBuffer(_groundMesh, count);
                _groundMaterial.SetBuffer(PerInstanceData, _meshPropsBuffer);
            }
        }

        private static ComputeBuffer CreateArgsBuffer(Mesh mesh, int instanceCount)
        {
            var args = new uint[5];
            args[0] = mesh.GetIndexCount(0);
            args[1] = (uint)instanceCount;
            args[2] = mesh.GetIndexStart(0);
            args[3] = mesh.GetBaseVertex(0);
            args[4] = 0;

            var buffer = new ComputeBuffer(1, 5 * sizeof(uint), ComputeBufferType.IndirectArguments);
            buffer.SetData(args);
            return buffer;
        }

        /// <summary>
        /// Re-upload MeshProperties to GPU if chunk data changed.
        /// Call before Draw() each frame.
        /// </summary>
        public void SyncIfDirty()
        {
            if (!_chunk.Dirty) return;

            _meshPropsBuffer.SetData(_chunk.MeshProps);
            _chunk.Dirty = false;
        }

        /// <summary>
        /// Issue DrawMeshInstancedIndirect calls for this chunk.
        /// Ground tiles drawn first (render queue 2001), then crop passes on top.
        /// </summary>
        public void Draw()
        {
            // Ground pass first (always visible, uses gr matrix via vertInstancingGroundSetup)
            if (_groundMesh != null && _groundMaterial != null)
                Graphics.DrawMeshInstancedIndirect(_groundMesh, 0, _groundMaterial, _bounds, _groundArgsBuffer);

            // Crop passes (each material's _Id filters which instances are visible via ShaderGraph)
            foreach (var pass in _cropPasses)
                Graphics.DrawMeshInstancedIndirect(pass.Mesh, 0, pass.Material, _bounds, pass.ArgsBuffer);
        }

        public void Dispose()
        {
            _meshPropsBuffer?.Release();
            _meshPropsBuffer = null;

            foreach (var pass in _cropPasses)
            {
                pass.ArgsBuffer?.Release();
                if (pass.Material != null)
                    UnityEngine.Object.Destroy(pass.Material);
            }
            _cropPasses.Clear();

            _groundArgsBuffer?.Release();
            _groundArgsBuffer = null;

            if (_groundMaterial != null)
                UnityEngine.Object.Destroy(_groundMaterial);
            _groundMaterial = null;
        }
    }
}
