using System;
using UnityEngine;
using WheatFarm.Core.Data;

namespace WheatFarm.Farming
{
    /// <summary>
    /// Per-chunk GPU instanced indirect renderer.
    /// Each unlocked chunk gets its own ChunkCropRenderer with dedicated ComputeBuffers.
    /// Uses same shader pipeline as legacy CropRenderer (GetStructedBuffer.hlsl, _PerInstanceData).
    /// </summary>
    public class ChunkCropRenderer : IDisposable
    {
        private static readonly int PerInstanceData = Shader.PropertyToID("_PerInstanceData");

        private readonly ChunkData _chunk;
        private readonly Bounds _bounds;

        private ComputeBuffer _meshPropsBuffer;
        private ComputeBuffer _argsBuffer;
        private Material _material;
        private Mesh _mesh;

        public ChunkData Chunk => _chunk;

        public ChunkCropRenderer(ChunkData chunk, Mesh mesh, Material sharedMaterial, float chunkWorldSize)
        {
            _chunk = chunk;
            _mesh = mesh;

            // Create unique material instance for this chunk's buffer binding
            _material = new Material(sharedMaterial);

            // Bounds centered on the chunk's world position
            var center = new Vector3(
                (chunk.ChunkCoord.x + 0.5f) * chunkWorldSize,
                0f,
                (chunk.ChunkCoord.y + 0.5f) * chunkWorldSize);
            _bounds = new Bounds(center, new Vector3(chunkWorldSize, 2f, chunkWorldSize));

            InitializeBuffers();
        }

        private void InitializeBuffers()
        {
            int count = _chunk.CellCount;

            // Mesh properties buffer (StructuredBuffer in shader)
            _meshPropsBuffer = new ComputeBuffer(count, MeshProperties.Size());
            _meshPropsBuffer.SetData(_chunk.MeshProps);

            // Indirect args buffer: [indexCount, instanceCount, indexStart, baseVertex, 0]
            var args = new uint[5];
            args[0] = _mesh.GetIndexCount(0);
            args[1] = (uint)count;
            args[2] = _mesh.GetIndexStart(0);
            args[3] = _mesh.GetBaseVertex(0);
            args[4] = 0;

            _argsBuffer = new ComputeBuffer(1, 5 * sizeof(uint), ComputeBufferType.IndirectArguments);
            _argsBuffer.SetData(args);

            _material.SetBuffer(PerInstanceData, _meshPropsBuffer);
        }

        /// <summary>
        /// Re-upload MeshProperties to GPU if chunk data changed.
        /// Call before Draw() each frame.
        /// </summary>
        public void SyncIfDirty()
        {
            if (!_chunk.Dirty) return;
            _meshPropsBuffer.SetData(_chunk.MeshProps);
            _material.SetBuffer(PerInstanceData, _meshPropsBuffer);
            _chunk.Dirty = false;
        }

        /// <summary>
        /// Issue the DrawMeshInstancedIndirect call for this chunk.
        /// </summary>
        public void Draw()
        {
            if (_mesh == null || _material == null) return;
            Graphics.DrawMeshInstancedIndirect(_mesh, 0, _material, _bounds, _argsBuffer);
        }

        public void Dispose()
        {
            _meshPropsBuffer?.Release();
            _meshPropsBuffer = null;

            _argsBuffer?.Release();
            _argsBuffer = null;

            if (_material != null)
                UnityEngine.Object.Destroy(_material);
            _material = null;
        }
    }
}
