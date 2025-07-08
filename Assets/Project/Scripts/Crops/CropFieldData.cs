using System;
using UnityEngine;
using UnityEngine.Serialization;
using Random = UnityEngine.Random;

public class CropFieldData : MonoBehaviour
{
    public Vector2Int FieldSize;
    public Vector2Int RotationRandom;
    public Vector3 Margin;
    public Vector3 PositionRandom;
    public Vector3 Size;
    public Vector2 RandomSizeMult;
    
    private MeshProperties[] _properties;
    private int _size => FieldSize.x * FieldSize.y;
    public int GetFieldSize => FieldSize.x * FieldSize.y;

    public bool HasChanges = false;
    private Bounds bounds;
    private CropRenderer[] _cropRenderers;

    public MeshProperties[] GetMeshProperties => _properties;
    public Bounds GetBounds => bounds;

    public void OnChanges()
    {
        HasChanges = true;
        foreach (var item in _cropRenderers)
        {
            item.OnChangedData();
        }
    }
    
    void Awake()
    {
        _cropRenderers = GetComponents<CropRenderer>();
        
        float stepU = 1f / FieldSize.x;
        float stepV = 1f / FieldSize.y;
        
        Vector3 size = new Vector3(
            FieldSize.x * Margin.x,
            0,
            FieldSize.y * Margin.z);

        var center = transform.position;
        // Boundary surrounding the meshes we will be drawing.  Used for occlusion.
        bounds = new Bounds(center + size * 0.5f, size);
        
        // Initialize buffer with the given population.
        _properties = new MeshProperties[_size];
        Vector3 offset = new Vector3(-FieldSize.x * Margin.x, 0, -FieldSize.y * Margin.z) * 0.5f;
        for (int x = 0; x < FieldSize.x; x++)
        {
            for (int y = 0; y < FieldSize.y; y++)
            {
                MeshProperties props = new MeshProperties();

                Matrix4x4 matrix4X4 = Matrix4x4.zero;
                Matrix4x4 groundMatrix = Matrix4x4.zero;

                Vector3 position = new Vector3(
                    x * Margin.x,
                    0,
                    y * Margin.z);

                Vector3 randomPos = new Vector3(
                    Random.Range(-PositionRandom.x, PositionRandom.x), 
                    0, 
                    Random.Range(-PositionRandom.z, PositionRandom.z));

                matrix4X4.SetTRS(
                    position + offset + randomPos,
                    Quaternion.Euler(Vector3.up * RotationRandom.GetRandom()),
                    Size * RandomSizeMult.GetRandom());
                
                groundMatrix.SetTRS(
                    position + offset + Vector3.up * 0.05f,
                    Quaternion.identity,
                    Vector3.one * 0.01f);

                props.m = matrix4X4;
                props.gr = groundMatrix;

                float u = x * stepU;
                float v = y * stepV;

                props.uv = new Vector4(u, v, stepU, stepV);
                props.cropState = new Vector4(0, 0, 0, 0);
                props.color = new Vector4(0, 1, 0, 1);
                _properties[x * FieldSize.x + y] = props;
            }
        }
    }
}