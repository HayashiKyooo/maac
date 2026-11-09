using UnityEditor;
using UnityEngine;

namespace MAAC.ComicShader.Editor
{
    /// <summary>
    /// Material Inspector for "Universal Render Pipeline/Comic".
    /// Groups the properties per feature, hides parameters of disabled features and keeps keywords /
    /// the outline pass in sync (ComicMaterialUtility.SetupMaterial).
    /// </summary>
    public class ComicShaderGUI : ShaderGUI
    {
        static class Styles
        {
            public static readonly GUIContent surface = new GUIContent("Surface");
            public static readonly GUIContent tone = new GUIContent("基本設定 / Tone");
            public static readonly GUIContent halftone = new GUIContent("ハーフトーン / Halftone");
            public static readonly GUIContent pattern = new GUIContent("パターン座標 / Pattern Space");
            public static readonly GUIContent hatching = new GUIContent("クロスハッチング / Hatching");
            public static readonly GUIContent noise = new GUIContent("ノイズ・紙 / Noise & Paper");
            public static readonly GUIContent outline = new GUIContent("アウトライン / Outline");
            public static readonly GUIContent advanced = new GUIContent("Advanced");
            public static readonly GUIContent baseMap = new GUIContent("Base Map", "Albedo (RGB) used as luminance for the tone, alpha for clipping.");
        }

        MaterialEditor m_Editor;
        MaterialProperty[] m_Props;

        public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
        {
            m_Editor = materialEditor;
            m_Props = properties;

            EditorGUI.BeginChangeCheck();

            if (Section("Surface", Styles.surface, true))
            {
                var baseMap = Find("_BaseMap");
                m_Editor.TexturePropertySingleLine(Styles.baseMap, baseMap, Find("_BaseColor"));
                m_Editor.TextureScaleOffsetProperty(baseMap);
                Prop("_AlphaClip");
                if (IsOn("_AlphaClip")) Prop("_Cutoff", 1);
                Prop("_Cull");
                Prop("_ReceiveShadows");
            }

            if (Section("Tone", Styles.tone, true))
            {
                Prop("_Grayscale");
                Prop("_Contrast");
                Prop("_Brightness");
                Prop("_ToneSteps");
                Prop("_ShadowThreshold");
                Prop("_ToneSmoothness");
                Prop("_AmbientStrength");
                Prop("_AdditionalLightStrength");
                Prop("_OcclusionStrength");
                Prop("_InkColor");
                Prop("_PaperColor");
            }

            if (Section("Halftone", Styles.halftone, true))
            {
                Prop("_HalftoneEnable");
                using (new EditorGUI.DisabledScope(!IsOn("_HalftoneEnable")))
                {
                    Prop("_HalftoneSpace", 0, "Halftone Coordinate Mode");
                    Prop("_HalftoneScale");
                    Prop("_HalftoneDotSize");
                    Prop("_HalftoneStrength");
                    Prop("_HalftoneAngle");
                    Prop("_HalftoneContrast");
                    Prop("_HalftoneColor");
                }
            }

            if (Section("Hatching", Styles.hatching, false))
            {
                Prop("_HatchingEnable");
                using (new EditorGUI.DisabledScope(!IsOn("_HatchingEnable")))
                {
                    Prop("_HatchingStrength");
                    Prop("_HatchingDensity");
                    Prop("_HatchingWidth");
                    Prop("_HatchingAngle");
                    Prop("_HatchingCrossAngle");
                    Prop("_HatchingThreshold1");
                    Prop("_HatchingThreshold2");
                }
            }

            if (Section("Pattern", Styles.pattern, false))
            {
                EditorGUILayout.HelpBox("Halftone とハッチングは同じ座標系を使用します。Screen: 1単位 = 1080p時の1px（解像度非依存）", MessageType.None);
                Prop("_HalftoneSpace", 0, "Coordinate Mode (Halftone / Hatching)");
                int space = Mathf.RoundToInt(Find("_HalftoneSpace").floatValue);
                if (space == 0) Prop("_HalftoneAnchorToObject");
                if (space == 1 || space == 2) Prop("_PatternWorldScale");
                if (space == 3) Prop("_PatternUVScale");
            }

            if (Section("Noise", Styles.noise, false))
            {
                Prop("_NoiseEnable");
                using (new EditorGUI.DisabledScope(!IsOn("_NoiseEnable")))
                {
                    Prop("_NoiseStrength");
                    Prop("_NoiseScale");
                    Prop("_PaperGrain");
                    Prop("_InkUnevenness");
                    Prop("_InkMottleScale");
                }
            }

            if (Section("Outline", Styles.outline, true))
            {
                Prop("_OutlineEnable");
                using (new EditorGUI.DisabledScope(!IsOn("_OutlineEnable")))
                {
                    Prop("_OutlineWidth");
                    Prop("_OutlineColor");
                    Prop("_OutlineDistanceScale", 0, "Distance-based Width");
                    Prop("_OutlineReferenceDistance");
                    Prop("_OutlineMinScale");
                    Prop("_OutlineZOffset");
                    Prop("_OutlineNormalSource");
                }
                EditorGUILayout.HelpBox("外周線: マテリアル単位の Inverted Hull。内部の折れ線は Renderer の 'Comic Outline' Feature（画面空間）で描画されます。角で線が切れる場合は Tools/Comic Shader/Bake Smoothed Outline Normals を実行し Normal Source を UV3 にしてください。", MessageType.None);
            }

            if (Section("Advanced", Styles.advanced, false))
            {
                Prop("_QueueOffset", 0, "Queue Offset");
                m_Editor.RenderQueueField();
                m_Editor.EnableInstancingField();
                m_Editor.DoubleSidedGIField();
            }

            if (EditorGUI.EndChangeCheck())
            {
                foreach (var t in m_Editor.targets)
                    ComicMaterialUtility.SetupMaterial(t as Material);
            }
        }

        public override void ValidateMaterial(Material material)
        {
            ComicMaterialUtility.SetupMaterial(material);
        }

        public override void AssignNewShaderToMaterial(Material material, Shader oldShader, Shader newShader)
        {
            // keep _BaseMap/_BaseColor when switching from URP Lit/SimpleLit/Unlit
            base.AssignNewShaderToMaterial(material, oldShader, newShader);
            ComicMaterialUtility.SetupMaterial(material);
        }

        // ------------------------------------------------------------------ helpers
        MaterialProperty Find(string name) => FindProperty(name, m_Props, false);

        bool IsOn(string name)
        {
            var p = Find(name);
            return p != null && p.floatValue > 0.5f;
        }

        void Prop(string name, int indent = 0, string label = null)
        {
            var p = Find(name);
            if (p == null) return;
            EditorGUI.indentLevel += indent;
            m_Editor.ShaderProperty(p, label ?? p.displayName);
            EditorGUI.indentLevel -= indent;
        }

        static bool Section(string key, GUIContent title, bool defaultOpen)
        {
            string id = "ComicShaderGUI." + key;
            bool open = SessionState.GetBool(id, defaultOpen);
            EditorGUILayout.Space(2);
            bool newOpen = EditorGUILayout.BeginFoldoutHeaderGroup(open, title);
            EditorGUILayout.EndFoldoutHeaderGroup();
            if (newOpen != open) SessionState.SetBool(id, newOpen);
            return newOpen;
        }
    }
}
