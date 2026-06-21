using UnityEngine;

namespace WheatFarm.Core.Data
{
    /// <summary>
    /// GPU instance data per sub-cell for DrawMeshInstancedIndirect.
    /// 180 bytes — must match GetStructedBuffer.hlsl exactly.
    /// </summary>
    public struct MeshProperties
    {
        public Matrix4x4 m;
        public Matrix4x4 gr;
        public Vector4 color;
        public Vector4 uv;
        public Vector4 cropState;
        public uint neighborTypes;   // 4 nibbles: N(0..3) E(4..7) S(8..11) W(12..15) = neighbor GroundState

        public static int Size()
        {
            return
                sizeof(float) * 4 * 4 + // matrix
                sizeof(float) * 4 * 4 + // groundMatrix
                sizeof(float) * 4 +     // color
                sizeof(float) * 4 +     // cropState
                sizeof(float) * 4 +     // uv
                sizeof(uint);            // neighborTypes
        }
    }
}
