using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace WheatFarm.Rendering
{
    /// <summary>
    /// Draws preview-layer objects occluded by scene geometry as a highlighted
    /// silhouette with an animated dashed outline. PC renderer only; the ghost
    /// shader's own ZTest Greater pass is the Mobile fallback.
    /// </summary>
    public class GhostOutlineRenderFeature : ScriptableRendererFeature
    {
        [System.Serializable]
        public class OutlineSettings
        {
            public LayerMask previewLayer;
            [Range(1f, 8f)] public float outlineThicknessPx = 2.5f;
            [Min(2f)] public float dashDensityPx = 14f;
            public float dashSpeed = 0.8f;
            [Range(0f, 1f)] public float fillStrength = 0.2f;
        }

        public OutlineSettings settings = new();
        [SerializeField] private Shader maskShader;      // Hidden/WheatFarm/GhostOcclusionMask
        [SerializeField] private Shader compositeShader; // Hidden/WheatFarm/GhostOutlineComposite

        private static readonly int PreviewHighlightColorId = Shader.PropertyToID("_PreviewHighlightColor");

        private Material _maskMaterial;
        private Material _compositeMaterial;
        private GhostOutlinePass _pass;

        public override void Create()
        {
            if (maskShader == null) maskShader = Shader.Find("Hidden/WheatFarm/GhostOcclusionMask");
            if (compositeShader == null) compositeShader = Shader.Find("Hidden/WheatFarm/GhostOutlineComposite");
            if (maskShader == null || compositeShader == null) return;

            // Sane default so brush-only previews (which never set the ghost's validity
            // color) still get a visible green outline.
            Shader.SetGlobalColor(PreviewHighlightColorId, new Color(0.2f, 0.9f, 0.2f, 1f));

            _maskMaterial = CoreUtils.CreateEngineMaterial(maskShader);
            _compositeMaterial = CoreUtils.CreateEngineMaterial(compositeShader);
            _pass = new GhostOutlinePass(_maskMaterial, _compositeMaterial, settings)
            {
                renderPassEvent = RenderPassEvent.AfterRenderingTransparents
            };
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (_pass == null) return;
            if (renderingData.cameraData.cameraType != CameraType.Game) return;
            renderer.EnqueuePass(_pass);
        }

        protected override void Dispose(bool disposing)
        {
            CoreUtils.Destroy(_maskMaterial);
            CoreUtils.Destroy(_compositeMaterial);
        }

        private class GhostOutlinePass : ScriptableRenderPass
        {
            private static readonly ShaderTagId[] ShaderTags =
            {
                new("UniversalForward"), new("SRPDefaultUnlit"), new("UniversalForwardOnly")
            };

            private static readonly int OutlineThicknessId = Shader.PropertyToID("_OutlineThickness");
            private static readonly int DashDensityId = Shader.PropertyToID("_DashDensity");
            private static readonly int DashSpeedId = Shader.PropertyToID("_DashSpeed");
            private static readonly int FillStrengthId = Shader.PropertyToID("_FillStrength");

            private readonly Material _maskMaterial;
            private readonly Material _compositeMaterial;
            private readonly OutlineSettings _settings;

            public GhostOutlinePass(Material mask, Material composite, OutlineSettings settings)
            {
                _maskMaterial = mask;
                _compositeMaterial = composite;
                _settings = settings;
            }

            private class MaskPassData { public RendererListHandle RendererList; }

            private class CompositePassData
            {
                public TextureHandle Mask;
                public Material Material;
                public float OutlineThickness;
                public float DashDensity;
                public float DashSpeed;
                public float FillStrength;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                var resourceData = frameData.Get<UniversalResourceData>();
                var renderingData = frameData.Get<UniversalRenderingData>();
                var cameraData = frameData.Get<UniversalCameraData>();
                var lightData = frameData.Get<UniversalLightData>();

                // --- Pass A: occlusion mask ---
                var desc = cameraData.cameraTargetDescriptor;
                desc.depthBufferBits = 0;
                desc.msaaSamples = 1;
                desc.graphicsFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.R8_UNorm;
                // clear: true ensures the mask starts black each frame.
                TextureHandle mask = UniversalRenderer.CreateRenderGraphTexture(
                    renderGraph, desc, "_GhostOcclusionMask", true);

                using (var builder = renderGraph.AddRasterRenderPass<MaskPassData>(
                           "Ghost Occlusion Mask", out var passData))
                {
                    var drawSettings = RenderingUtils.CreateDrawingSettings(
                        new System.Collections.Generic.List<ShaderTagId>(ShaderTags),
                        renderingData, cameraData, lightData, SortingCriteria.CommonOpaque);
                    drawSettings.overrideMaterial = _maskMaterial;
                    drawSettings.overrideMaterialPassIndex = 0;

                    var filterSettings = new FilteringSettings(RenderQueueRange.all, _settings.previewLayer);
                    var rendererListParams = new RendererListParams(
                        renderingData.cullResults, drawSettings, filterSettings);
                    passData.RendererList = renderGraph.CreateRendererList(rendererListParams);

                    builder.UseRendererList(passData.RendererList);
                    builder.SetRenderAttachment(mask, 0);
                    builder.SetRenderAttachmentDepth(resourceData.activeDepthTexture, AccessFlags.Read);
                    builder.SetRenderFunc((MaskPassData data, RasterGraphContext ctx) =>
                    {
                        ctx.cmd.DrawRendererList(data.RendererList);
                    });
                }

                // --- Pass B: composite outline over camera color ---
                using (var builder = renderGraph.AddRasterRenderPass<CompositePassData>(
                           "Ghost Outline Composite", out var passData))
                {
                    passData.Mask = mask;
                    passData.Material = _compositeMaterial;
                    passData.OutlineThickness = _settings.outlineThicknessPx;
                    passData.DashDensity = _settings.dashDensityPx;
                    passData.DashSpeed = _settings.dashSpeed;
                    passData.FillStrength = _settings.fillStrength;

                    builder.UseTexture(mask);
                    builder.SetRenderAttachment(resourceData.activeColorTexture, 0);
                    builder.SetRenderFunc((CompositePassData data, RasterGraphContext ctx) =>
                    {
                        data.Material.SetFloat(OutlineThicknessId, data.OutlineThickness);
                        data.Material.SetFloat(DashDensityId, data.DashDensity);
                        data.Material.SetFloat(DashSpeedId, data.DashSpeed);
                        data.Material.SetFloat(FillStrengthId, data.FillStrength);
                        Blitter.BlitTexture(ctx.cmd, data.Mask, new Vector4(1, 1, 0, 0), data.Material, 0);
                    });
                }
            }
        }
    }
}
