using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace MAAC.ComicShader.Editor
{
    /// <summary>
    /// Creates "Comic_Renderer" (a copy of the active URP asset's default renderer + ComicOutlineFeature)
    /// and APPENDS it to the active URP asset's renderer list. The default renderer (index 0) and all
    /// existing renderers are left untouched; cameras opt in by selecting the Comic renderer.
    /// Menu: Tools/Comic Shader/Create Comic Renderer
    /// </summary>
    public static class ComicRendererSetup
    {
        public const string SettingsFolder = "Assets/MAAC/ComicShader/Settings";
        public const string RendererPath = SettingsFolder + "/Comic_Renderer.asset";

        public static UniversalRenderPipelineAsset ActiveAsset =>
            (QualitySettings.renderPipeline ?? GraphicsSettings.defaultRenderPipeline) as UniversalRenderPipelineAsset;

        /// <summary>Index of Comic_Renderer in the active URP asset, or -1.</summary>
        public static int FindComicRendererIndex()
        {
            var asset = ActiveAsset;
            if (asset == null) return -1;
            var so = new SerializedObject(asset);
            var list = so.FindProperty("m_RendererDataList");
            for (int i = 0; i < list.arraySize; i++)
            {
                var rd = list.GetArrayElementAtIndex(i).objectReferenceValue;
                if (rd != null && AssetDatabase.GetAssetPath(rd) == RendererPath)
                    return i;
            }
            return -1;
        }

        [MenuItem("Tools/Comic Shader/Create Comic Renderer")]
        public static void CreateMenu()
        {
            int index = CreateOrUpdate();
            Debug.Log($"[Comic] Comic_Renderer is renderer index {index} of {AssetDatabase.GetAssetPath(ActiveAsset)}");
        }

        /// <returns>renderer index of Comic_Renderer in the active URP asset</returns>
        public static int CreateOrUpdate()
        {
            var asset = ActiveAsset;
            if (asset == null)
                throw new System.Exception("Active render pipeline is not URP.");

            if (!AssetDatabase.IsValidFolder(SettingsFolder))
                AssetDatabase.CreateFolder("Assets/MAAC/ComicShader", "Settings");

            // 1) renderer asset (copy of the default renderer so it keeps the project's settings)
            var rendererData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
            if (rendererData == null)
            {
                var assetSO = new SerializedObject(asset);
                var list = assetSO.FindProperty("m_RendererDataList");
                int defaultIndex = assetSO.FindProperty("m_DefaultRendererIndex").intValue;
                var source = list.GetArrayElementAtIndex(defaultIndex).objectReferenceValue as UniversalRendererData;
                if (source == null)
                    throw new System.Exception("Default renderer is not a UniversalRendererData.");
                if (!AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(source), RendererPath))
                    throw new System.Exception("Failed to copy renderer asset.");
                AssetDatabase.ImportAsset(RendererPath);
                rendererData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
                rendererData.name = "Comic_Renderer";
                // SSAO is attenuated by the Comic shader (SSAO Strength = 0 by default), so the copied
                // SSAO feature would only cost GPU time. It is disabled in the COPY only.
                foreach (var f in rendererData.rendererFeatures)
                    if (f is ScreenSpaceAmbientOcclusion) f.SetActive(false);
            }

            // 2) ComicOutlineFeature (added exactly like ScriptableRendererDataEditor.AddComponent does)
            bool hasFeature = false;
            foreach (var f in rendererData.rendererFeatures)
                if (f is ComicOutlineFeature) hasFeature = true;
            if (!hasFeature)
            {
                var feature = ScriptableObject.CreateInstance<ComicOutlineFeature>();
                feature.name = "ComicOutline";
                feature.hideFlags |= HideFlags.HideInHierarchy;
                AssetDatabase.AddObjectToAsset(feature, rendererData);
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out string _, out long localId);

                var so = new SerializedObject(rendererData);
                var features = so.FindProperty("m_RendererFeatures");
                var map = so.FindProperty("m_RendererFeatureMap");
                features.arraySize++;
                features.GetArrayElementAtIndex(features.arraySize - 1).objectReferenceValue = feature;
                map.arraySize++;
                map.GetArrayElementAtIndex(map.arraySize - 1).longValue = localId;
                so.ApplyModifiedProperties();

                var fso = new SerializedObject(feature);
                fso.FindProperty("m_Shader").objectReferenceValue = Shader.Find("Hidden/Comic/ScreenSpaceOutline");
                fso.ApplyModifiedProperties();
                EditorUtility.SetDirty(feature);
            }
            rendererData.SetDirty();
            EditorUtility.SetDirty(rendererData);

            // 3) append to the active URP asset's renderer list (never reorders / replaces entries)
            int index = FindComicRendererIndex();
            if (index < 0)
            {
                var assetSO = new SerializedObject(asset);
                var list = assetSO.FindProperty("m_RendererDataList");
                list.arraySize++;
                list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = rendererData;
                assetSO.ApplyModifiedProperties();
                EditorUtility.SetDirty(asset);
                index = list.arraySize - 1;
            }
            AssetDatabase.SaveAssets();
            return index;
        }
    }
}
