using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace MAAC.ComicShader
{
    /// <summary>
    /// Render Graph raster pass that draws one full-screen triangle with the
    /// Hidden/Comic/ScreenSpaceOutline material, alpha blended onto the active color target.
    /// Reads _CameraDepthTexture / _CameraNormalsTexture (global textures set by URP).
    /// </summary>
    public class ComicOutlinePass : ScriptableRenderPass
    {
        static readonly int s_EdgeColor = Shader.PropertyToID("_EdgeColor");
        static readonly int s_EdgeThickness = Shader.PropertyToID("_EdgeThickness");
        static readonly int s_DepthThreshold = Shader.PropertyToID("_DepthThreshold");
        static readonly int s_NormalThreshold = Shader.PropertyToID("_NormalThreshold");
        static readonly int s_EdgeSharpness = Shader.PropertyToID("_EdgeSharpness");
        static readonly int s_FadeStart = Shader.PropertyToID("_FadeStart");
        static readonly int s_FadeEnd = Shader.PropertyToID("_FadeEnd");
        const string k_UseNormalsKeyword = "_USE_NORMALS";

        Material m_Material;
        bool m_UseNormals;

        class PassData
        {
            internal Material material;
        }

        public ComicOutlinePass()
        {
            profilingSampler = new ProfilingSampler("Comic Screen Space Outline");
        }

        public void Setup(Material material, ComicOutlineFeature.Settings s)
        {
            m_Material = material;
            m_UseNormals = s.useNormals;

            material.SetColor(s_EdgeColor, s.edgeColor);
            material.SetFloat(s_EdgeThickness, s.thickness);
            material.SetFloat(s_DepthThreshold, s.depthThreshold);
            material.SetFloat(s_NormalThreshold, s.normalThreshold);
            material.SetFloat(s_EdgeSharpness, s.edgeSharpness);
            material.SetFloat(s_FadeStart, s.fadeStart);
            material.SetFloat(s_FadeEnd, Mathf.Max(s.fadeEnd, s.fadeStart + 0.01f));
            CoreUtils.SetKeyword(material, k_UseNormalsKeyword, s.useNormals);
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            if (m_Material == null)
                return;

            UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();

            TextureHandle depth = resourceData.cameraDepthTexture;
            if (!depth.IsValid())
                return;
            TextureHandle normals = resourceData.cameraNormalsTexture;
            if (m_UseNormals && !normals.IsValid())
                return;

            TextureHandle target = resourceData.activeColorTexture;
            if (!target.IsValid())
                return;

            using (var builder = renderGraph.AddRasterRenderPass<PassData>(passName, out var passData, profilingSampler))
            {
                passData.material = m_Material;

                builder.UseTexture(depth, AccessFlags.Read);
                if (m_UseNormals)
                    builder.UseTexture(normals, AccessFlags.Read);

                // ReadWrite: we alpha blend onto the existing image.
                builder.SetRenderAttachment(target, 0, AccessFlags.ReadWrite);

                builder.SetRenderFunc(static (PassData data, RasterGraphContext context) =>
                {
                    context.cmd.DrawProcedural(Matrix4x4.identity, data.material, 0, MeshTopology.Triangles, 3, 1);
                });
            }
        }
    }
}
