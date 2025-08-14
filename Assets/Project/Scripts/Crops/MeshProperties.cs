using UnityEngine;

public struct MeshProperties
{
    public Matrix4x4 m;
    public Matrix4x4 gr;
    public Vector4 color;
    public Vector4 uv;
    public Vector4 cropState;

    public static int Size()
    {
        return
            sizeof(float) * 4 * 4 + // matrix;
            sizeof(float) * 4 * 4 + // groundMatrix;
            sizeof(float) * 4 + // color;
            sizeof(float) * 4 + // cropstate;
            sizeof(float) * 4; // uv;
    }
}