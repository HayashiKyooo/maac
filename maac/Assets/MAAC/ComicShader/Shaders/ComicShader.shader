// =====================================================================================
// Universal Render Pipeline / Comic
// Black & white Japanese manga style shader for URP 17.6 (Unity 6000.6).
// Passes: ForwardLit (UniversalForwardOnly), Outline (SRPDefaultUnlit, inverted hull),
//         ShadowCaster, DepthOnly, DepthNormalsOnly
// =====================================================================================
Shader "Universal Render Pipeline/Comic"
{
    Properties
    {
        // ---------------- Surface ----------------
        [MainTexture] _BaseMap("Base Map", 2D) = "white" {}
        [MainColor]   _BaseColor("Base Color", Color) = (1, 1, 1, 1)
        [Toggle(_ALPHATEST_ON)] _AlphaClip("Alpha Clipping", Float) = 0.0
        _Cutoff("Alpha Cutoff", Range(0.0, 1.0)) = 0.5
        [Enum(UnityEngine.Rendering.CullMode)] _Cull("Cull", Float) = 2.0
        [ToggleOff(_RECEIVE_SHADOWS_OFF)] _ReceiveShadows("Receive Shadows", Float) = 1.0

        // ---------------- Tone (基本設定) ----------------
        _Grayscale("Grayscale", Range(0, 1)) = 1.0
        _Contrast("Contrast", Range(0.1, 8)) = 1.0
        _Brightness("Brightness", Range(-1, 1)) = 0.0
        [IntRange] _ToneSteps("Tone Steps", Range(2, 8)) = 4
        _ShadowThreshold("Shadow Threshold", Range(0, 1.5)) = 0.6
        _ToneSmoothness("Tone Edge Smoothness", Range(0, 4)) = 1.0
        _AmbientStrength("Ambient Strength", Range(0, 2)) = 1.0
        _AdditionalLightStrength("Additional Light Strength", Range(0, 2)) = 1.0
        _OcclusionStrength("SSAO Strength", Range(0, 1)) = 0.0
        _InkColor("Ink Color", Color) = (0, 0, 0, 1)
        _PaperColor("Paper Color", Color) = (1, 1, 1, 1)

        // ---------------- Pattern space (Halftone + Hatching) ----------------
        [KeywordEnum(Screen, World, Object, UV)] _HalftoneSpace("Halftone Coordinate Mode", Float) = 0
        _PatternWorldScale("World/Object Scale (units per meter)", Float) = 100
        _PatternUVScale("UV Scale (units per UV)", Float) = 128
        [ToggleUI] _HalftoneAnchorToObject("Screen: Anchor To Object", Float) = 0

        // ---------------- Halftone ----------------
        [Toggle(_HALFTONE_ON)] _HalftoneEnable("Enable Halftone", Float) = 1
        _HalftoneScale("Halftone Scale (cell size)", Range(2, 64)) = 10
        _HalftoneDotSize("Halftone Dot Size", Range(0.25, 2)) = 1
        _HalftoneStrength("Halftone Strength", Range(0, 1)) = 1
        _HalftoneAngle("Halftone Angle", Range(0, 90)) = 45
        _HalftoneContrast("Halftone Contrast", Range(0, 1)) = 0.8
        _HalftoneColor("Halftone Color", Color) = (0, 0, 0, 1)

        // ---------------- Hatching ----------------
        [Toggle(_HATCHING_ON)] _HatchingEnable("Enable Hatching", Float) = 0
        _HatchingStrength("Hatching Strength", Range(0, 1)) = 1
        _HatchingDensity("Hatching Density (lines / 100 units)", Range(1, 50)) = 10
        _HatchingWidth("Hatching Width", Range(0.02, 1)) = 0.25
        _HatchingAngle("Hatching Angle", Range(0, 180)) = 45
        _HatchingCrossAngle("Cross Hatching Angle Offset", Range(0, 180)) = 90
        _HatchingThreshold1("Hatching Threshold", Range(0, 1)) = 0.5
        _HatchingThreshold2("Cross Hatching Threshold", Range(0, 1)) = 0.25

        // ---------------- Noise / Paper ----------------
        [Toggle(_NOISE_ON)] _NoiseEnable("Enable Noise", Float) = 0
        _NoiseStrength("Noise Strength", Range(0, 1)) = 0.5
        _NoiseScale("Noise Scale (grain size)", Range(0.25, 8)) = 1
        _PaperGrain("Paper Grain", Range(0, 1)) = 0.5
        _InkUnevenness("Ink Unevenness", Range(0, 1)) = 0.5
        _InkMottleScale("Ink Mottle Size", Range(4, 400)) = 80

        // ---------------- Outline (inverted hull) ----------------
        [ToggleUI] _OutlineEnable("Enable Outline", Float) = 1
        _OutlineWidth("Outline Width (px @1080p)", Range(0, 16)) = 2.5
        _OutlineColor("Outline Color", Color) = (0, 0, 0, 1)
        _OutlineDistanceScale("Distance-based Width", Range(0, 1)) = 0.5
        _OutlineReferenceDistance("Outline Reference Distance", Range(0.1, 100)) = 8
        _OutlineMinScale("Outline Min Width Scale", Range(0, 1)) = 0.3
        _OutlineZOffset("Outline Z Offset", Range(0, 1)) = 0.0
        [Enum(Mesh Normal, 0, Smoothed Normal UV3, 1)] _OutlineNormalSource("Outline Normal Source", Float) = 0

        // ---------------- Hidden / engine ----------------
        [HideInInspector] _QueueOffset("Queue offset", Float) = 0.0
        [HideInInspector][NoScaleOffset] unity_Lightmaps("unity_Lightmaps", 2DArray) = "" {}
        [HideInInspector][NoScaleOffset] unity_LightmapsInd("unity_LightmapsInd", 2DArray) = "" {}
        [HideInInspector][NoScaleOffset] unity_ShadowMasks("unity_ShadowMasks", 2DArray) = "" {}
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
            "IgnoreProjector" = "True"
        }
        LOD 300

        // ---------------------------------------------------------------------------
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForwardOnly" }

            Cull [_Cull]
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex ComicPassVertex
            #pragma fragment ComicPassFragment

            // Material keywords
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma shader_feature_local _RECEIVE_SHADOWS_OFF
            #pragma shader_feature_local_fragment _HALFTONE_ON
            #pragma shader_feature_local_fragment _HALFTONESPACE_SCREEN _HALFTONESPACE_WORLD _HALFTONESPACE_OBJECT _HALFTONESPACE_UV
            #pragma shader_feature_local_fragment _HATCHING_ON
            #pragma shader_feature_local_fragment _NOISE_ON

            // Universal Pipeline keywords
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ EVALUATE_SH_MIXED EVALUATE_SH_VERTEX
            #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
            #pragma multi_compile _ SHADOWS_SHADOWMASK
            #pragma multi_compile _ _LIGHT_LAYERS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ProbeVolumeVariants.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"

            // Unity defined keywords
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile _ USE_LEGACY_LIGHTMAPS
            #pragma multi_compile _ LOD_FADE_CROSSFADE

            // GPU Instancing / DOTS
            #pragma multi_compile_instancing
            #pragma instancing_options renderinglayer
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"

            #include "Includes/ComicForwardPass.hlsl"
            ENDHLSL
        }

        // ---------------------------------------------------------------------------
        // Inverted hull outline. Enabled/disabled per material via SetShaderPassEnabled("SRPDefaultUnlit").
        Pass
        {
            Name "Outline"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            Cull Front
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex ComicOutlineVertex
            #pragma fragment ComicOutlineFragment

            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"

            #include "Includes/Outline.hlsl"
            ENDHLSL
        }

        // ---------------------------------------------------------------------------
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment

            #pragma shader_feature_local _ALPHATEST_ON
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Includes/ComicInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
            ENDHLSL
        }

        // ---------------------------------------------------------------------------
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment

            #pragma shader_feature_local _ALPHATEST_ON
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"

            #include "Includes/ComicInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthOnlyPass.hlsl"
            ENDHLSL
        }

        // ---------------------------------------------------------------------------
        // Used for _CameraNormalsTexture (SSAO, Comic screen-space outline feature).
        // "DepthNormalsOnly" is the tag used by forward-only materials (see URP ComplexLit).
        Pass
        {
            Name "DepthNormalsOnly"
            Tags { "LightMode" = "DepthNormalsOnly" }

            ZWrite On
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DepthNormalsVertex
            #pragma fragment DepthNormalsFragment

            #pragma shader_feature_local _ALPHATEST_ON
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"

            #include "Includes/ComicInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthNormalsPass.hlsl"
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
    CustomEditor "MAAC.ComicShader.Editor.ComicShaderGUI"
}
