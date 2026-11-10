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
    /// The ground pass reads the full per-cell buffer. Each crop pass reads its own
    /// COMPACTED buffer holding only the visible instances of that mesh, so the GPU
    /// processes N planted crops instead of (cells x mesh types). Before compaction a
    /// fully unlocked 5x5 farm pushed ~36M crop vertices per frame even when empty.
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

        private class CropPass
        {
            public int MeshId;
            public Mesh Mesh;
            public Material Material;
            public ComputeBuffer ArgsBuffer;
            public ComputeBuffer InstanceBuffer; // compacted, capacity = cell count
            public int VisibleCount;
            public uint[] Args;
        }

        private MeshProperties[] _scratch;

        /// <summary>Total crop instances submitted for this chunk (all passes). For profiling/tests.</summary>
        public int VisibleCropInstances
        {
            get { int n = 0; foreach (var p in _cropPasses) n += p.VisibleCount; return n; }
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

            // Crop passes — one per mesh entry, each with its own compacted instance buffer
            _scratch = new MeshProperties[count];
            foreach (var entry in cropEntries)
            {
                if (entry.Mesh == null || entry.Material == null) continue;

                var pass = new CropPass
                {
                    MeshId = entry.MeshId,
                    Mesh = entry.Mesh,
                    Material = new Material(entry.Material),
                    ArgsBuffer = CreateArgsBuffer(entry.Mesh, 0),
                    InstanceBuffer = new ComputeBuffer(count, MeshProperties.Size()),
                    Args = new uint[]
                    {
                        entry.Mesh.GetIndexCount(0), 0,
                        entry.Mesh.GetIndexStart(0), entry.Mesh.GetBaseVertex(0), 0
                    }
                };
                pass.Material.SetBuffer(PerInstanceData, pass.InstanceBuffer);
                _cropPasses.Add(pass);
            }
            RebuildCropPasses();

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
            RebuildCropPasses();
            _chunk.Dirty = false;
        }

        /// <summary>
        /// Visible = matches the pass mesh, has a non-zero matrix (planted) and growth > 0
        /// (the shader hides cropState.y == 0 instances anyway).
        /// </summary>
        public static bool IsVisibleCrop(in MeshProperties p, int meshId) =>
            (int)(p.cropState.x + 0.5f) == meshId && p.cropState.y > 0f && p.m.m33 != 0f;

        private void RebuildCropPasses()
        {
            var props = _chunk.MeshProps;
            foreach (var pass in _cropPasses)
            {
                int n = 0;
                for (int i = 0; i < props.Length; i++)
                {
                    if (IsVisibleCrop(props[i], pass.MeshId))
                        _scratch[n++] = props[i];
                }

                if (n > 0)
                    pass.InstanceBuffer.SetData(_scratch, 0, 0, n);
                if (n != pass.VisibleCount)
                {
                    pass.VisibleCount = n;
                    pass.Args[1] = (uint)n;
                    pass.ArgsBuffer.SetData(pass.Args);
                }
            }
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
            {
                if (pass.VisibleCount == 0) continue;
                Graphics.DrawMeshInstancedIndirect(pass.Mesh, 0, pass.Material, _bounds, pass.ArgsBuffer);
            }
        }

        public void Dispose()
        {
            _meshPropsBuffer?.Release();
            _meshPropsBuffer = null;

            foreach (var pass in _cropPasses)
            {
                pass.ArgsBuffer?.Release();
                pass.InstanceBuffer?.Release();
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
