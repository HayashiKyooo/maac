using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;

namespace MAAC.ComicShader.Editor
{
    /// <summary>
    /// Compiles a representative matrix of keyword variants of the Comic shaders for the
    /// current editor graphics API and reports every compiler error / warning.
    /// Menu: Tools/Comic Shader/Validate Shader Variants
    /// </summary>
    public static class ComicShaderValidator
    {
        public const string ComicShaderPath = "Assets/MAAC/ComicShader/Shaders/ComicShader.shader";
        public const string OutlineShaderPath = "Assets/MAAC/ComicShader/Shaders/ComicScreenSpaceOutline.shader";

        static readonly string[][] k_ForwardGlobalSets =
        {
            new string[0],
            new[] { "_MAIN_LIGHT_SHADOWS_CASCADE", "_ADDITIONAL_LIGHTS", "_ADDITIONAL_LIGHT_SHADOWS", "_SHADOWS_SOFT", "_SHADOWS_SOFT_HIGH", "_SCREEN_SPACE_OCCLUSION", "_LIGHT_LAYERS", "_WRITE_RENDERING_LAYERS", "FOG_EXP2" },
            new[] { "_MAIN_LIGHT_SHADOWS_CASCADE", "_ADDITIONAL_LIGHTS", "_CLUSTER_LIGHT_LOOP", "_ADDITIONAL_LIGHT_SHADOWS", "_SHADOWS_SOFT", "_SHADOWS_SOFT_MEDIUM", "_SCREEN_SPACE_OCCLUSION", "FOG_LINEAR" },
            new[] { "_MAIN_LIGHT_SHADOWS", "_ADDITIONAL_LIGHTS_VERTEX", "EVALUATE_SH_VERTEX" },
            new[] { "_MAIN_LIGHT_SHADOWS_SCREEN", "LIGHTMAP_ON", "DIRLIGHTMAP_COMBINED", "SHADOWS_SHADOWMASK", "LIGHTMAP_SHADOW_MIXING", "_CLUSTER_LIGHT_LOOP", "_ADDITIONAL_LIGHTS" },
            new[] { "LIGHTMAP_ON", "USE_LEGACY_LIGHTMAPS" },
            new[] { "PROBE_VOLUMES_L1", "EVALUATE_SH_MIXED" },
            new[] { "PROBE_VOLUMES_L2" },
            new[] { "LOD_FADE_CROSSFADE", "INSTANCING_ON" },
            new[] { "DOTS_INSTANCING_ON" },
        };

        static readonly string[][] k_ForwardLocalSets =
        {
            new string[0],
            new[] { "_HALFTONE_ON", "_HALFTONESPACE_SCREEN", "_HATCHING_ON", "_NOISE_ON" },
            new[] { "_HALFTONE_ON", "_HALFTONESPACE_WORLD", "_HATCHING_ON", "_NOISE_ON", "_ALPHATEST_ON" },
            new[] { "_HALFTONE_ON", "_HALFTONESPACE_OBJECT", "_HATCHING_ON" },
            new[] { "_HALFTONE_ON", "_HALFTONESPACE_UV", "_NOISE_ON", "_RECEIVE_SHADOWS_OFF" },
            new[] { "_HATCHING_ON", "_HALFTONESPACE_WORLD" },
        };

        static readonly string[][] k_AuxSets =
        {
            new string[0],
            new[] { "_ALPHATEST_ON" },
            new[] { "_ALPHATEST_ON", "LOD_FADE_CROSSFADE", "INSTANCING_ON" },
            new[] { "_CASTING_PUNCTUAL_LIGHT_SHADOW" },
            new[] { "_GBUFFER_NORMALS_OCT", "_WRITE_RENDERING_LAYERS" },
            new[] { "DOTS_INSTANCING_ON", "_ALPHATEST_ON" },
            new[] { "FOG_LINEAR" },
        };

        static readonly string[][] k_OutlineFeatureSets =
        {
            new string[0],
            new[] { "_USE_NORMALS" },
            new[] { "_USE_NORMALS", "_GBUFFER_NORMALS_OCT" },
        };

        [MenuItem("Tools/Comic Shader/Validate Shader Variants")]
        static void MenuValidate()
        {
            string report = Run();
            Debug.Log(report);
        }

        public static string Run()
        {
            var sb = new StringBuilder();
            int errors = 0, compiled = 0;

            ShaderCompilerPlatform platform = GetEditorPlatform();
            BuildTarget target = EditorUserBuildSettings.activeBuildTarget;
            sb.AppendLine($"[ComicShaderValidator] platform={platform} target={target}");

            errors += ValidateShader(ComicShaderPath, sb, platform, target, ref compiled);
            errors += ValidateShader(OutlineShaderPath, sb, platform, target, ref compiled);

            sb.Insert(0, errors == 0 ? $"[ComicShaderValidator] OK - {compiled} stage compiles, 0 errors\n"
                                     : $"[ComicShaderValidator] FAILED - {errors} errors in {compiled} stage compiles\n");
            return sb.ToString();
        }

        static int ValidateShader(string path, StringBuilder sb, ShaderCompilerPlatform platform, BuildTarget target, ref int compiled)
        {
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(path);
            if (shader == null)
            {
                sb.AppendLine($"  (skipped, not found) {path}");
                return 0;
            }

            int errors = 0;
            foreach (var msg in ShaderUtil.GetShaderMessages(shader))
            {
                sb.AppendLine($"  [import {msg.severity}] {msg.message} ({msg.file}:{msg.line})");
                if (msg.severity == UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error) errors++;
            }

            ShaderData data = ShaderUtil.GetShaderData(shader);
            var sub = data.GetSerializedSubshader(0);
            for (int p = 0; p < sub.PassCount; p++)
            {
                var pass = sub.GetPass(p);
                IEnumerable<string[]> sets = CombinationsFor(shader, pass.Name);
                foreach (var kw in sets)
                {
                    foreach (var stage in new[] { ShaderType.Vertex, ShaderType.Fragment })
                    {
                        var info = pass.CompileVariant(stage, kw, platform, target);
                        compiled++;
                        if (!info.Success)
                            errors++;
                        foreach (var m in info.Messages)
                        {
                            if (m.severity == UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error || !info.Success)
                                sb.AppendLine($"  [{pass.Name}/{stage}] {m.severity}: {m.message} ({m.file}:{m.line}) kw=[{string.Join(" ", kw)}]");
                        }
                    }
                }
            }
            sb.AppendLine($"  {shader.name}: {errors} errors");
            return errors;
        }

        static IEnumerable<string[]> CombinationsFor(Shader shader, string passName)
        {
            if (shader.name.StartsWith("Hidden/"))
            {
                foreach (var s in k_OutlineFeatureSets) yield return s;
                yield break;
            }

            if (passName == "ForwardLit")
            {
                foreach (var g in k_ForwardGlobalSets)
                    foreach (var l in k_ForwardLocalSets)
                    {
                        var list = new List<string>(g);
                        list.AddRange(l);
                        yield return list.ToArray();
                    }
                yield break;
            }

            foreach (var s in k_AuxSets) yield return s;
        }

        static ShaderCompilerPlatform GetEditorPlatform()
        {
            switch (SystemInfo.graphicsDeviceType)
            {
                case UnityEngine.Rendering.GraphicsDeviceType.Metal: return ShaderCompilerPlatform.Metal;
                case UnityEngine.Rendering.GraphicsDeviceType.Vulkan: return ShaderCompilerPlatform.Vulkan;
                case UnityEngine.Rendering.GraphicsDeviceType.OpenGLCore: return ShaderCompilerPlatform.OpenGLCore;
                default: return ShaderCompilerPlatform.D3D;
            }
        }
    }
}
