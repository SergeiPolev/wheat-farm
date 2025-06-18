using UnityEngine;
using System.Collections.Generic;
using System.Runtime.InteropServices;

public class GrassChunkRenderer : MonoBehaviour
{
    [System.Serializable]
    public struct PlantData
    {
        public Vector3 worldPos;
    }

    [System.Serializable]
    public class CropEntry
    {
        public int cropID;
        public Mesh mesh;
        public Material material;
    }

    public GrassChunk grassChunk; // ссылка на чанк с текстурой
    public float chunkSize => grassChunk.transform.transform.localScale.x;
    public List<CropEntry> crops;

    private ComputeBuffer argsBuffer;
    private ComputeBuffer plantBuffer;
    private Bounds bounds;

    void Start()
    {
        GeneratePlantData();

        foreach (var crop in crops)
        {
            // создаём копию материала для каждого меша
            crop.material = new Material(crop.material);
            crop.material.SetBuffer("_PlantBuffer", plantBuffer);
            crop.material.SetTexture("_FieldMap", grassChunk.GrassTextures[grassChunk.CurrentTextureIndex]);
            crop.material.SetFloat("_FieldMapSize", chunkSize);
        }

        bounds = new Bounds(transform.position, Vector3.one * chunkSize);
    }

    void GeneratePlantData()
    {
        PlantData[] data = new PlantData[1];
        for (int i = 0; i < 1; i++)
        {
            float x = Random.Range(0, chunkSize);
            float z = Random.Range(0, chunkSize);
            Vector3 pos = transform.position + new Vector3(x, 0, z);
            data[i].worldPos = pos;
        }

        plantBuffer = new ComputeBuffer(1, Marshal.SizeOf(typeof(PlantData)));
        plantBuffer.SetData(data);

        argsBuffer = new ComputeBuffer(1, 5 * sizeof(uint), ComputeBufferType.IndirectArguments);
    }

    void Update()
    {
        var texture = grassChunk.GrassTextures[grassChunk.CurrentTextureIndex];

        foreach (var crop in crops)
        {
            var mat = crop.material;
            mat.SetTexture("_FieldMap", texture);
            mat.SetFloat("_CropID", crop.cropID / 255f); // R канал в 0–1
            mat.SetFloat("_FieldMapSize", chunkSize);

            Mesh mesh = crop.mesh;
            uint[] args = new uint[5] {
                (uint)mesh.GetIndexCount(0), (uint)1, 0, 0, 0
            };
            argsBuffer.SetData(args);

            Graphics.DrawMeshInstancedIndirect(mesh, 0, mat, bounds, argsBuffer);
        }
    }

    void OnDestroy()
    {
        argsBuffer?.Release();
        plantBuffer?.Release();
    }
}
