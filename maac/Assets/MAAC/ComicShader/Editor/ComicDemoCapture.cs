using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MAAC.ComicShader.Editor
{
    /// <summary>
    /// Verification helper: opens the demo scene as an isolated PREVIEW scene (the user's open
    /// scenes are not modified), renders its camera into a PNG and closes it again.
    /// </summary>
    public static class ComicDemoCapture
    {
        public static string Capture(string scenePath, string outputPng, int width, int height, Action<Scene, Camera> tweak = null)
        {
            Scene preview = EditorSceneManager.OpenPreviewScene(scenePath);
            RenderTexture rt = null;
            Texture2D tex = null;
            try
            {
                Camera cam = null;
                foreach (var root in preview.GetRootGameObjects())
                {
                    cam = root.GetComponentInChildren<Camera>(true);
                    if (cam != null) break;
                }
                if (cam == null) return "no camera in scene";

                cam.scene = preview;
                tweak?.Invoke(preview, cam);

                rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 1 };
                rt.Create();
                var prevTarget = cam.targetTexture;
                cam.targetTexture = rt;
                // Render twice: the first frame may still be compiling shader variants asynchronously.
                ShaderUtil.allowAsyncCompilation = false;
                cam.Render();
                cam.Render();
                ShaderUtil.allowAsyncCompilation = true;
                cam.targetTexture = prevTarget;

                var prevActive = RenderTexture.active;
                RenderTexture.active = rt;
                tex = new Texture2D(width, height, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                tex.Apply();
                RenderTexture.active = prevActive;

                Directory.CreateDirectory(Path.GetDirectoryName(outputPng));
                File.WriteAllBytes(outputPng, tex.EncodeToPNG());
                return outputPng;
            }
            finally
            {
                ShaderUtil.allowAsyncCompilation = true;
                if (rt != null) { rt.Release(); UnityEngine.Object.DestroyImmediate(rt); }
                if (tex != null) UnityEngine.Object.DestroyImmediate(tex);
                EditorSceneManager.ClosePreviewScene(preview);
            }
        }
    }
}
