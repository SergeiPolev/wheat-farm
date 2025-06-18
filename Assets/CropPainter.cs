using UnityEngine;
using UnityEngine.UI;

public class CropPainter : MonoBehaviour
{
    public RawImage RawImage;
    [Range(0f, 1f)]
    public float startGrowth = 0.01f;

    private Material paintMaterial;
    private static readonly int BrushCenter = Shader.PropertyToID("_BrushCenter");
    private static readonly int BrushSize = Shader.PropertyToID("_BrushSize");
    private static readonly int BrushColor = Shader.PropertyToID("_BrushColor");
    private static readonly int MainTex = Shader.PropertyToID("_MainTex");
    private int _currentCrop;

    private static float Radius => 1f;

    void Start()
    {
        paintMaterial = new Material(Shader.Find("Hidden/CropBrush"));
    }

    void Update()
    {
        if (Input.GetMouseButton(0))
        {
            if (Physics.Raycast(Camera.main.ScreenPointToRay(Input.mousePosition), out var hit, float.MaxValue, LayerManager.GrassChunkLayerMask))
            {
                var chunks = Physics.OverlapSphere(hit.point, Radius, LayerManager.GrassChunkLayerMask);
                foreach (var collider in chunks)
                {
                    PaintAtWorld(collider.GetComponent<GrassChunk>(), hit, _currentCrop);
                }
            }
        }

        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            _currentCrop = 1;
        }
        else if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            _currentCrop = 10;
        }
    }

    public void PaintAtWorld(GrassChunk grassChunk, RaycastHit hit, int cropType)
    {
        var texture = grassChunk.GrassTextures;
        var currentIndex = grassChunk.CurrentTextureIndex;
        var nextIndex = 1 - grassChunk.CurrentTextureIndex;
        
        float itemSize = grassChunk.BoxCollider.bounds.size.x;
        Vector3 center = hit.collider.bounds.center;
        Vector3 localPos = grassChunk.transform.InverseTransformPoint(hit.point);
        var offset = grassChunk.BoxCollider.bounds.size * 0.5f;
        var posToChunk = localPos + offset;
        posToChunk /= itemSize;
        
        float uvX = posToChunk.x;
        float uvY = posToChunk.z;
        Vector4 color = new Vector4(cropType / 255f, startGrowth, 0, 1);
        
        RawImage.texture = texture[currentIndex];

        Vector2 uvPos = new Vector2(1 - uvX, 1 - uvY);
        Debug.Log(uvPos);
        
        paintMaterial.SetVector(BrushCenter, uvPos);
        paintMaterial.SetFloat(BrushSize, itemSize * Radius / texture[currentIndex].height);
        paintMaterial.SetVector(BrushColor, color);
        paintMaterial.SetTexture(MainTex, texture[currentIndex]);

        RenderTexture.active = texture[currentIndex];
        Graphics.Blit(texture[currentIndex], texture[nextIndex], paintMaterial);
        RenderTexture.active = null;

        grassChunk.CurrentTextureIndex = nextIndex;
    }
}