using UnityEngine;

public class CropFieldData : MonoBehaviour
{
    public RenderTexture cropMap;
    public int size = 1;

    void Awake()
    {
        cropMap = new RenderTexture(size, size, 0, RenderTextureFormat.ARGB32)
        {
            enableRandomWrite = true
        };
        cropMap.Create();
        
        RenderTexture.active = cropMap;
        Graphics.Blit(Texture2D.whiteTexture, cropMap);
        RenderTexture.active = null;
    }
}