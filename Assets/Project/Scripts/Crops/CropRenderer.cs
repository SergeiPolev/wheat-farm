using Services;
using UnityEngine;

public class CropRenderer : MonoBehaviour
{
    public CropFieldData FieldData;
    public CropsType CropsType;

    private CropsData _cropData;

    private ComputeBuffer meshPropertiesBuffer;
    private ComputeBuffer argsBuffer;
    private ComputeBuffer argsGroundBuffer;
    
    private MaterialPropertyBlock materialPropertyBlocks;
    private static readonly int PerInstanceData = Shader.PropertyToID("_PerInstanceData");
    public  Material cropMaterial;
    private Material _groundMaterial;
    private Mesh _ground;

    public Bounds GetBounds => FieldData.GetBounds;

    private void Setup()
    {
        _cropData = AllServices.Container.Single<StaticDataService>().GetCropData(CropsType); 
        _ground = AllServices.Container.Single<StaticDataService>().Crops.GroundMesh; 
        _groundMaterial = new Material(AllServices.Container.Single<StaticDataService>().Crops.GroundMat); 
        
        // Materials using the same structered buffer if shared
        cropMaterial = new Material(_cropData.CropMat);
        //cropMaterial = _cropData.CropMat;

        InitializeBuffers();
    }

    private void InitializeBuffers()
    {
        // Argument buffer used by DrawMeshInstancedIndirect.
        uint[] args = new uint[5] { 0, 0, 0, 0, 0 };
        // Arguments for drawing mesh.
        // 0 == number of triangle indices, 1 == population, others are only relevant if drawing submeshes.
        args[0] = _cropData.CropMesh.GetIndexCount(0);
        args[1] = (uint)FieldData.GetFieldSize;
        args[2] = _cropData.CropMesh.GetIndexStart(0);
        args[3] = _cropData.CropMesh.GetBaseVertex(0);
        
        argsBuffer = new ComputeBuffer(1, args.Length * sizeof(uint), ComputeBufferType.IndirectArguments);
        argsBuffer.SetData(args);
        
        
        uint[] groundArgs = new uint[5] { 0, 0, 0, 0, 0 };
        // Arguments for drawing mesh.
        // 0 == number of triangle indices, 1 == population, others are only relevant if drawing submeshes.
        groundArgs[0] = _ground.GetIndexCount(0);
        groundArgs[1] = (uint)FieldData.GetFieldSize;
        groundArgs[2] = _ground.GetIndexStart(0);
        groundArgs[3] = _ground.GetBaseVertex(0);
        
        argsGroundBuffer = new ComputeBuffer(1, groundArgs.Length * sizeof(uint), ComputeBufferType.IndirectArguments);
        argsGroundBuffer.SetData(groundArgs);
        
        meshPropertiesBuffer = new ComputeBuffer(FieldData.GetFieldSize, MeshProperties.Size());
        meshPropertiesBuffer.SetData(FieldData.GetMeshProperties);
        cropMaterial.SetBuffer(PerInstanceData, meshPropertiesBuffer);
        _groundMaterial.SetBuffer(PerInstanceData, meshPropertiesBuffer);
    }

    private void Start()
    {
        Setup();
    }

    private void Update()
    {
        Graphics.DrawMeshInstancedIndirect(_cropData.CropMesh, 0, cropMaterial, GetBounds, argsBuffer);
        //Graphics.DrawMeshInstancedIndirect(_ground, 0, _groundMaterial, bounds, argsGroundBuffer);
    }

    private void OnDisable()
    {
        // Release gracefully.
        meshPropertiesBuffer?.Release();
        meshPropertiesBuffer = null;

        argsBuffer?.Release();
        argsGroundBuffer?.Release();
        argsBuffer = null;
        argsGroundBuffer = null;
    }

    private void OnDrawGizmosSelected()
    {
        if (Application.isPlaying)
        {
            Gizmos.DrawWireCube(GetBounds.center, GetBounds.size);
        }
        else
        {
            Vector3 size = new Vector3(
                FieldData.FieldSize.x * FieldData.Margin.x,
                0,
                FieldData.FieldSize.y * FieldData.Margin.z);

            var center = transform.position;
            // Boundary surrounding the meshes we will be drawing.  Used for occlusion.
            Gizmos.DrawWireCube(center + size * 0.5f, size);
        }
    }

    public void OnChangedData()
    {
        meshPropertiesBuffer.SetData(FieldData.GetMeshProperties);
        cropMaterial.SetBuffer(PerInstanceData, meshPropertiesBuffer);
        _groundMaterial.SetBuffer(PerInstanceData, meshPropertiesBuffer);
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