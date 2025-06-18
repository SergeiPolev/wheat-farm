using System;
using UnityEngine;

public class InstanceByGroup : MonoBehaviour
{
    public Vector3 FieldSize;
    public Vector3 MeshSize;

    private float _step;

    private Bounds _bounds;

    private void Start()
    {
        _step = FieldSize.x / MeshSize.x;

        _bounds = new Bounds(transform.position, FieldSize);
        GenerateArrays();
    }

    private void GenerateArrays()
    {
        
    }
}