using System;
using UnityEngine;
using WheatFarm.Core.Data;

namespace WheatFarm.Farming
{
    /// <summary>
    /// Per-chunk GPU instanced indirect renderer.
    /// Each unlocked chunk gets its own ChunkCropRenderer with dedicated ComputeBuffers.
    /// Issues TWO draw calls per frame:
    ///   1. Ground tiles (flat quads using gr matrix, always visible)
    ///   2. Crops (plant meshes using m matrix, visible when cropState.y > 0)
    /// Both passes share the same _PerInstanceData ComputeBuffer.
    /// </summary>
    public class ChunkCropRenderer : IDisposable
    {
        private static readonly int PerInstanceData = Shader.PropertyToID("_PerInstanceData");

        private readonly ChunkData _chunk;
        private readonly Bounds _bounds;

        // Shared buffer (both passes read from this)
        private ComputeBuffer _meshPropsBuffer;

        // Crop pass
        private ComputeBuffer _cropArgsBuffer;
        private Material _cropMaterial;
        private Mesh _cropMesh;

        // Ground tile pass (optional — null if not configured)
        private ComputeBuffer _groundArgsBuffer;
        private Material _groundMaterial;
        private Mesh _groundMesh;

        public ChunkData Chunk => _chunk;

        public ChunkCropRenderer(
            ChunkData chunk,
            Mesh cropMesh, Material cropSharedMaterial,
            Mesh groundMesh, Material groundSharedMaterial,
            float chunkWorldSize)
        {
            _chunk = chunk;
            _cropMesh = cropMesh;
            _cropMaterial = new Material(cropSharedMaterial);

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

            InitializeBuffers();
        }

        private void InitializeBuffers()
        {
            int count = _chunk.CellCount;

            // Shared mesh properties buffer (StructuredBuffer in shader)
            _meshPropsBuffer = new ComputeBuffer(count, MeshProperties.Size());
            _meshPropsBuffer.SetData(_chunk.MeshProps);

            // Crop indirect args
            _cropArgsBuffer = CreateArgsBuffer(_cropMesh, count);
            _cropMaterial.SetBuffer(PerInstanceData, _meshPropsBuffer);

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
            _cropMaterial.SetBuffer(PerInstanceData, _meshPropsBuffer);

            if (_groundMaterial != null)
                _groundMaterial.SetBuffer(PerInstanceData, _meshPropsBuffer);

            _chunk.Dirty = false;
        }

        /// <summary>
        /// Issue DrawMeshInstancedIndirect calls for this chunk.
        /// Ground tiles are drawn first (render queue 2001), then crops on top (2002).
        /// </summary>
        public void Draw()
        {
            // Ground pass first (always visible, uses gr matrix via vertInstancingGroundSetup)
            if (_groundMesh != null && _groundMaterial != null)
                Graphics.DrawMeshInstancedIndirect(_groundMesh, 0, _groundMaterial, _bounds, _groundArgsBuffer);

            // Crop pass second (visible when cropState.y > 0, uses m matrix via vertInstancingSetup)
            if (_cropMesh != null && _cropMaterial != null)
                Graphics.DrawMeshInstancedIndirect(_cropMesh, 0, _cropMaterial, _bounds, _cropArgsBuffer);
        }

        public void Dispose()
        {
            _meshPropsBuffer?.Release();
            _meshPropsBuffer = null;

            _cropArgsBuffer?.Release();
            _cropArgsBuffer = null;

            _groundArgsBuffer?.Release();
            _groundArgsBuffer = null;

            if (_cropMaterial != null)
                UnityEngine.Object.Destroy(_cropMaterial);
            _cropMaterial = null;

            if (_groundMaterial != null)
                UnityEngine.Object.Destroy(_groundMaterial);
            _groundMaterial = null;
        }
    }
}
