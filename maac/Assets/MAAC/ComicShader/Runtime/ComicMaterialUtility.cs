using UnityEngine;

namespace MAAC.ComicShader
{
    /// <summary>
    /// Keeps keywords / pass states of "Universal Render Pipeline/Comic" materials in sync with
    /// their float toggles. The Material Inspector calls this automatically (ShaderGUI.ValidateMaterial);
    /// call it yourself after changing toggles from script at runtime, e.g.
    /// <code>mat.SetFloat(ComicMaterialUtility.HalftoneEnable, 0); ComicMaterialUtility.SetupMaterial(mat);</code>
    /// NOTE: keywords are declared with shader_feature, so a variant that is not used by any material
    /// at build time is stripped. Keep a material (or a ShaderVariantCollection) using every variant
    /// you intend to switch to at runtime.
    /// </summary>
    public static class ComicMaterialUtility
    {
        public const string ShaderName = "Universal Render Pipeline/Comic";
        public const string OutlinePassLightMode = "SRPDefaultUnlit";

        public static readonly int AlphaClip = Shader.PropertyToID("_AlphaClip");
        public static readonly int ReceiveShadows = Shader.PropertyToID("_ReceiveShadows");
        public static readonly int HalftoneEnable = Shader.PropertyToID("_HalftoneEnable");
        public static readonly int HalftoneSpace = Shader.PropertyToID("_HalftoneSpace");
        public static readonly int HatchingEnable = Shader.PropertyToID("_HatchingEnable");
        public static readonly int NoiseEnable = Shader.PropertyToID("_NoiseEnable");
        public static readonly int OutlineEnable = Shader.PropertyToID("_OutlineEnable");
        public static readonly int OutlineWidth = Shader.PropertyToID("_OutlineWidth");
        public static readonly int QueueOffset = Shader.PropertyToID("_QueueOffset");

        static readonly string[] k_SpaceKeywords =
        {
            "_HALFTONESPACE_SCREEN", "_HALFTONESPACE_WORLD", "_HALFTONESPACE_OBJECT", "_HALFTONESPACE_UV"
        };

        public enum PatternSpace { Screen = 0, World = 1, Object = 2, UV = 3 }

        public static bool IsComicMaterial(Material material)
        {
            return material != null && material.shader != null && material.shader.name == ShaderName;
        }

        /// <summary>Synchronizes keywords, the outline pass and the render queue with the material's properties.</summary>
        public static void SetupMaterial(Material material)
        {
            if (!IsComicMaterial(material))
                return;

            SetKeyword(material, "_ALPHATEST_ON", GetToggle(material, AlphaClip));
            SetKeyword(material, "_RECEIVE_SHADOWS_OFF", material.HasProperty(ReceiveShadows) && material.GetFloat(ReceiveShadows) < 0.5f);
            SetKeyword(material, "_HALFTONE_ON", GetToggle(material, HalftoneEnable));
            SetKeyword(material, "_HATCHING_ON", GetToggle(material, HatchingEnable));
            SetKeyword(material, "_NOISE_ON", GetToggle(material, NoiseEnable));

            int space = material.HasProperty(HalftoneSpace) ? Mathf.Clamp(Mathf.RoundToInt(material.GetFloat(HalftoneSpace)), 0, 3) : 0;
            for (int i = 0; i < k_SpaceKeywords.Length; i++)
                SetKeyword(material, k_SpaceKeywords[i], i == space);

            // The inverted-hull outline is a separate pass. Disabling the pass removes the extra draw call entirely.
            bool outline = GetToggle(material, OutlineEnable) && material.HasProperty(OutlineWidth) && material.GetFloat(OutlineWidth) > 0f;
            material.SetShaderPassEnabled(OutlinePassLightMode, outline);

            int queueOffset = material.HasProperty(QueueOffset) ? (int)material.GetFloat(QueueOffset) : 0;
            int baseQueue = GetToggle(material, AlphaClip) ? (int)UnityEngine.Rendering.RenderQueue.AlphaTest : (int)UnityEngine.Rendering.RenderQueue.Geometry;
            material.renderQueue = baseQueue + queueOffset;
            material.SetOverrideTag("RenderType", GetToggle(material, AlphaClip) ? "TransparentCutout" : "Opaque");
        }

        public static void SetPatternSpace(Material material, PatternSpace space)
        {
            if (!IsComicMaterial(material)) return;
            material.SetFloat(HalftoneSpace, (float)space);
            SetupMaterial(material);
        }

        static bool GetToggle(Material m, int id) => m.HasProperty(id) && m.GetFloat(id) > 0.5f;

        static void SetKeyword(Material m, string keyword, bool on)
        {
            if (on) m.EnableKeyword(keyword);
            else m.DisableKeyword(keyword);
        }
    }
}
