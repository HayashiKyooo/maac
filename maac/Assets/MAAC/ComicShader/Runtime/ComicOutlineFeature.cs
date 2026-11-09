using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace MAAC.ComicShader
{
    /// <summary>
    /// Screen-space comic outline (Render Graph only - URP 17.6 has no Compatibility Mode).
    /// Detects silhouettes (depth) and internal creases (normals) on the whole opaque image and
    /// inks them by alpha-blending onto the camera color. Complements the per-material inverted
    /// hull outline of the Comic shader:
    ///  - Inverted hull : per-material width/color, constant pixel width, cheap, but no interior creases
    ///                    and gaps at hard edges unless smoothed normals are baked.
    ///  - This feature  : interior creases + gap-free silhouettes for EVERY opaque object, one full-screen
    ///                    pass; needs the depth texture (and _CameraNormalsTexture when Use Normals is on,
    ///                    which is free when SSAO already runs a DepthNormals prepass).
    /// </summary>
    [DisallowMultipleRendererFeature("Comic Outline")]
    [Tooltip("Screen-space depth/normal edge detection for the comic look.")]
    public class ComicOutlineFeature : ScriptableRendererFeature
    {
        public enum InjectionPoint
        {
            AfterRenderingOpaques = RenderPassEvent.AfterRenderingOpaques,
            AfterRenderingSkybox = RenderPassEvent.AfterRenderingSkybox,
            BeforeRenderingTransparents = RenderPassEvent.BeforeRenderingTransparents,
            BeforeRenderingPostProcessing = RenderPassEvent.BeforeRenderingPostProcessing,
        }

        [System.Serializable]
        public class Settings
        {
            public InjectionPoint injectionPoint = InjectionPoint.AfterRenderingSkybox;
            [Tooltip("Use _CameraNormalsTexture to detect creases inside silhouettes. Requires a DepthNormals prepass (free if SSAO uses Depth Normals).")]
            public bool useNormals = true;
            public Color edgeColor = Color.black;
            [Tooltip("Sample offset in pixels at 1080p (scaled with resolution).")]
            [Range(0.5f, 6f)] public float thickness = 1.25f;
            [Tooltip("Relative depth discontinuity needed for a silhouette line.")]
            [Range(0.001f, 1f)] public float depthThreshold = 0.06f;
            [Tooltip("1 - cos(angle) between neighbouring normals needed for a crease line.")]
            [Range(0.01f, 1.5f)] public float normalThreshold = 0.3f;
            [Range(0.5f, 16f)] public float edgeSharpness = 4f;
            [Tooltip("Lines fade out between these eye distances (helps against noisy far detail).")]
            public float fadeStart = 30f;
            public float fadeEnd = 90f;
            [Tooltip("Also render in Scene View cameras.")]
            public bool showInSceneView = true;
        }

        public Settings settings = new Settings();

        [SerializeField, HideInInspector] Shader m_Shader;

        Material m_Material;
        ComicOutlinePass m_Pass;

        const string k_ShaderName = "Hidden/Comic/ScreenSpaceOutline";

        public override void Create()
        {
            if (m_Shader == null)
                m_Shader = Shader.Find(k_ShaderName);
            m_Pass = new ComicOutlinePass();
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            CameraType camType = renderingData.cameraData.cameraType;
            if (camType == CameraType.Preview || camType == CameraType.Reflection)
                return;
            if (camType == CameraType.SceneView && !settings.showInSceneView)
                return;

            if (m_Material == null)
            {
                if (m_Shader == null)
                    m_Shader = Shader.Find(k_ShaderName);
                if (m_Shader == null)
                    return;
                m_Material = CoreUtils.CreateEngineMaterial(m_Shader);
            }

            m_Pass.Setup(m_Material, settings);
            m_Pass.renderPassEvent = (RenderPassEvent)settings.injectionPoint;
            m_Pass.ConfigureInput(settings.useNormals
                ? ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Normal
                : ScriptableRenderPassInput.Depth);
            renderer.EnqueuePass(m_Pass);
        }

        protected override void Dispose(bool disposing)
        {
            CoreUtils.Destroy(m_Material);
            m_Material = null;
        }
    }
}
