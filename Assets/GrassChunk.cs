using UnityEngine;

public class GrassChunk : MonoBehaviour
{
    public MeshRenderer MeshRenderer;
    public BoxCollider BoxCollider;
    [NaughtyAttributes.ReadOnly]
    public RenderTexture[] GrassTextures = new RenderTexture[2];
    [NaughtyAttributes.ReadOnly] public int CurrentTextureIndex;
    public Texture CurrentTexture => GrassTextures[CurrentTextureIndex];

    private MaterialPropertyBlock _pb;
    private static readonly int MainTex = Shader.PropertyToID("_MainTex");

    private void Awake()
    {
        for (int i = 0; i < 2; i++)
        {
            GrassTextures[i] = new RenderTexture(31, 31, 0, RenderTextureFormat.ARGBFloat)
            {
                enableRandomWrite = true,
                filterMode = FilterMode.Point
            };
        }

        _pb = new MaterialPropertyBlock();
        _pb.SetTexture(MainTex, GrassTextures[0]);
        MeshRenderer.SetPropertyBlock(_pb);
    }
}