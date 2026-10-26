using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace Match3
{
    /// <summary>
    /// Full-screen material pass that reads the camera color, like URP's FullScreenPassRendererFeature with
    /// "Fetch Color Buffer", but copies the camera color with a sampled blit. URP 17.6 copies it with the render
    /// graph copy pass (framebuffer fetch through an input attachment), which rendered block noise on a
    /// Mali-G610 under Vulkan. See Doc/dependencies.md, "Problèmes connus".
    /// </summary>
    public class SampledCopyFullScreenFeature : ScriptableRendererFeature
    {
        public Material passMaterial;
        public RenderPassEvent injectionPoint = RenderPassEvent.AfterRenderingPostProcessing;

        private FullScreenPass m_Pass;

        public override void Create()
        {
            m_Pass = new FullScreenPass();
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            var cameraType = renderingData.cameraData.cameraType;
            if (passMaterial == null || cameraType == CameraType.Preview || cameraType == CameraType.Reflection)
            {
                return;
            }

            m_Pass.renderPassEvent = injectionPoint;
            m_Pass.Material = passMaterial;
            m_Pass.requiresIntermediateTexture = true;
            renderer.EnqueuePass(m_Pass);
        }

        private class FullScreenPass : ScriptableRenderPass
        {
            private static readonly int s_BlitTexture = Shader.PropertyToID("_BlitTexture");
            private static readonly int s_BlitScaleBias = Shader.PropertyToID("_BlitScaleBias");

            public Material Material;

            private class CopyData
            {
                public TextureHandle Source;
                public TextureHandle Destination;
            }

            private class DrawData
            {
                public TextureHandle Source;
                public TextureHandle Destination;
                public Material Material;
                public MaterialPropertyBlock Properties;
            }

            private readonly MaterialPropertyBlock m_Properties = new MaterialPropertyBlock();

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                var resources = frameData.Get<UniversalResourceData>();

                var desc = renderGraph.GetTextureDesc(resources.cameraColor);
                desc.name = "_CameraColorSampledCopy";
                desc.clearBuffer = false;
                var copy = renderGraph.CreateTexture(desc);

                using (var builder = renderGraph.AddRasterRenderPass<CopyData>("Copy Color (sampled blit)", out var copyData))
                {
                    copyData.Source = resources.cameraColor;
                    copyData.Destination = copy;
                    builder.UseTexture(copyData.Source, AccessFlags.Read);
                    builder.SetRenderAttachment(copy, 0, AccessFlags.Write);
                    builder.SetRenderFunc(static (CopyData data, RasterGraphContext context) =>
                    {
                        var flip = context.GetTextureUVOrigin(in data.Source) != context.GetTextureUVOrigin(in data.Destination);
                        Blitter.BlitTexture(context.cmd, data.Source, new Vector4(1, flip ? -1 : 1, 0, flip ? 1 : 0), 0, false);
                    });
                }

                using (var builder = renderGraph.AddRasterRenderPass<DrawData>("Full Screen (sampled copy)", out var drawData))
                {
                    drawData.Source = copy;
                    drawData.Destination = resources.activeColorTexture;
                    drawData.Material = Material;
                    drawData.Properties = m_Properties;
                    builder.UseTexture(copy, AccessFlags.Read);
                    builder.SetRenderAttachment(drawData.Destination, 0, AccessFlags.Write);
                    builder.SetRenderFunc(static (DrawData data, RasterGraphContext context) =>
                    {
                        var flip = context.GetTextureUVOrigin(in data.Source) != context.GetTextureUVOrigin(in data.Destination);
                        data.Properties.Clear();
                        data.Properties.SetTexture(s_BlitTexture, data.Source);
                        data.Properties.SetVector(s_BlitScaleBias, flip ? new Vector4(1, -1, 0, 1) : new Vector4(1, 1, 0, 0));
                        context.cmd.DrawProcedural(Matrix4x4.identity, data.Material, 0, MeshTopology.Triangles, 3, 1, data.Properties);
                    });
                }
            }
        }
    }
}
