using UnityEngine;

internal struct MeshProperties 
{
    public Matrix4x4 m;
    public Vector4 color;
    public Vector4 uv;

    public static int Size() {
        return
            sizeof(float) * 4 * 4 + // matrix;
            sizeof(float) * 4 +      // color;
            sizeof(float) * 4;      // uv;
    }
}

public class CropRenderer : MonoBehaviour
{
    public Vector2Int FieldSize;
    public Vector2Int RotationRandom;
    public Vector3 Margin;
    public Vector3 PositionRandom;
    public Vector3 Size;
    public Vector2 RandomSizeMult;
    public float range = 100f;
    public Mesh mesh;
    public Material material;
    

    private ComputeBuffer meshPropertiesBuffer;

    private ComputeBuffer argsBuffer;

    private Bounds bounds;

    private Matrix4x4[] matrices;

    private MaterialPropertyBlock materialPropertyBlocks;
    private int _size => FieldSize.x * FieldSize.y;

    private void Setup() 
    {

        // Boundary surrounding the meshes we will be drawing.  Used for occlusion.
        bounds = new Bounds(transform.position, Vector3.one * (range + 1));

        InitializeBuffers();
    }

    private void InitializeBuffers() {
        // Argument buffer used by DrawMeshInstancedIndirect.
        uint[] args = new uint[5] { 0, 0, 0, 0, 0 };
        // Arguments for drawing mesh.
        // 0 == number of triangle indices, 1 == population, others are only relevant if drawing submeshes.
        args[0] = (uint)mesh.GetIndexCount(0);
        args[1] = (uint)_size;
        args[2] = (uint)mesh.GetIndexStart(0);
        args[3] = (uint)mesh.GetBaseVertex(0);
        argsBuffer = new ComputeBuffer(1, args.Length * sizeof(uint), ComputeBufferType.IndirectArguments);
        argsBuffer.SetData(args);
            
        float stepU = 1f / FieldSize.x;
        float stepV = 1f / FieldSize.y;
        
        // Initialize buffer with the given population.
        MeshProperties[] properties = new MeshProperties[_size];
        Vector3 fieldPos = transform.position;

        for (int x = 0; x < FieldSize.x; x++)
        {
            for (int y = 0; y < FieldSize.y; y++)
            {
                MeshProperties props = new MeshProperties();

                Matrix4x4 matrix4X4 = Matrix4x4.zero;

                Vector3 position = new Vector3(
                    x * Margin.x + Random.Range(-PositionRandom.x, PositionRandom.x), 
                    0,
                    y * Margin.z + Random.Range(-PositionRandom.z, PositionRandom.z));

                matrix4X4.SetTRS(
                    fieldPos + position,
                    Quaternion.Euler(Vector3.up * RotationRandom.GetRandom()),
                    Size * RandomSizeMult.GetRandom());

                props.m = matrix4X4;
                
                float u = x * stepU;
                float v = y * stepV;

                props.uv = new Vector4(u, v, stepU, stepV);
                
                properties[x * FieldSize.x + y] = props;
            }
        }

        meshPropertiesBuffer = new ComputeBuffer(_size, MeshProperties.Size());
        meshPropertiesBuffer.SetData(properties);
        material.SetBuffer("_PerInstanceData", meshPropertiesBuffer);
    }

    private void Start() {
        Setup();
    }

    private void Update() {
        Graphics.DrawMeshInstancedIndirect(mesh, 0, material, bounds, argsBuffer);
    }

    private void OnDisable() {
        // Release gracefully.
        meshPropertiesBuffer?.Release();
        meshPropertiesBuffer = null;

        argsBuffer?.Release();
        argsBuffer = null;
    }
}

// DrawMeshInstanced
    /*void Start()
    {
        matrices = new Matrix4x4[FieldSize.x * FieldSize.y];
        var uvArray = new Vector4[FieldSize.x * FieldSize.y];
        materialPropertyBlocks = new MaterialPropertyBlock();
                
        Vector3 fieldPos = transform.position;
        
        float stepU = 1f / FieldSize.x;
        float stepV = 1f / FieldSize.y;
        
        for (int x = 0; x < FieldSize.x; x++)
        {
            for (int y = 0; y < FieldSize.y; y++)
            {
                Matrix4x4 matrix4X4 = Matrix4x4.zero;
                
                Vector3 position = new Vector3(
                    x * Margin.x + Random.Range(-PositionRandom.x, PositionRandom.x), 
                    0,
                    y * Margin.z + Random.Range(-PositionRandom.z, PositionRandom.z));

                matrix4X4.SetTRS(
                    fieldPos + position,
                    Quaternion.Euler(Vector3.up * RotationRandom.GetRandom()),
                    Size * RandomSizeMult.GetRandom());
                
                matrices[x * FieldSize.x + y] = matrix4X4;
                
                float u = x * stepU;
                float v = y * stepV;

                uvArray[x * FieldSize.x + y] = new Vector4(u, v, stepU, stepV);
            }
        }
        
        materialPropertyBlocks.SetTexture("_ColorTexture", Texture2D.whiteTexture);
        materialPropertyBlocks.SetTexture("_CropTexture", Texture2D.whiteTexture);
        materialPropertyBlocks.SetVectorArray("_UVFrozen", uvArray);
    }

    void Update()
    {
        Graphics.DrawMeshInstanced(mesh, 0, material, matrices,matrices.Length, materialPropertyBlocks);
    }*/
    