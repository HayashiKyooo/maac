// =====================================================================================
// Hidden/Comic/ScreenSpaceOutline
// Full screen edge detection used by ComicOutlineFeature (Render Graph, URP 17.6).
//  - Depth edges : Laplacian of 1/eyeDepth (perspective) or eyeDepth (ortho). This is
//                  exactly zero on planar surfaces, so floors/walls seen at grazing
//                  angles do not produce false lines. Only the NEAR side of a depth
//                  discontinuity is inked (lines hug the object like hand inking).
//  - Normal edges: 1 - min(dot(n_center, n_neighbour)) -> creases inside a silhouette.
// Output is alpha blended over the camera color (no color copy needed).
// =====================================================================================
Shader "Hidden/Comic/ScreenSpaceOutline"
{
    Properties
    {
        _EdgeColor("Edge Color", Color) = (0, 0, 0, 1)
        _EdgeThickness("Thickness (px @1080p)", Float) = 1.5
        _DepthThreshold("Depth Threshold", Float) = 0.08
        _NormalThreshold("Normal Threshold", Float) = 0.35
        _EdgeSharpness("Edge Sharpness", Float) = 4
        _FadeStart("Distance Fade Start", Float) = 30
        _FadeEnd("Distance Fade End", Float) = 80
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZTest Always
        ZWrite Off
        Cull Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Name "ComicScreenSpaceOutline"

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_local_fragment _ _USE_NORMALS
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #if defined(_USE_NORMALS)
                #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl"
            #endif

            CBUFFER_START(UnityPerMaterial)
                half4 _EdgeColor;
                float _EdgeThickness;
                float _DepthThreshold;
                float _NormalThreshold;
                float _EdgeSharpness;
                float _FadeStart;
                float _FadeEnd;
            CBUFFER_END

            struct Attributes
            {
                uint vertexID : SV_VertexID;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = GetFullScreenTriangleVertexPosition(input.vertexID);
                return output;
            }

            float EyeDepth(float rawDepth)
            {
                if (unity_OrthoParams.w == 0.0)
                    return LinearEyeDepth(rawDepth, _ZBufferParams);
            #if UNITY_REVERSED_Z
                rawDepth = 1.0 - rawDepth;
            #endif
                return lerp(_ProjectionParams.y, _ProjectionParams.z, rawDepth);
            }

            // Quantity that is linear in screen space across planar surfaces.
            float PlanarLinearDepth(float eyeDepth)
            {
                return unity_OrthoParams.w == 0.0 ? rcp(eyeDepth) : eyeDepth;
            }

            uint2 ClampPixel(float2 p)
            {
                float2 maxP = _CameraDepthTexture_TexelSize.zw - 1.0;
                return (uint2)clamp(p, 0.0, maxP);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 pixel = floor(input.positionCS.xy);
                float t = max(1.0, _EdgeThickness * _ScaledScreenParams.y / 1080.0);

                float2 o[4] = { float2(t, 0), float2(-t, 0), float2(0, t), float2(0, -t) };

                float rawC = LoadSceneDepth(ClampPixel(pixel));
                float eyeC = EyeDepth(rawC);
                float farPlane = _ProjectionParams.z * 0.999;

                float lc = PlanarLinearDepth(eyeC);
                float sum = 0.0;
                float eyeN[4];
                [unroll] for (int i = 0; i < 4; i++)
                {
                    eyeN[i] = EyeDepth(LoadSceneDepth(ClampPixel(pixel + o[i])));
                    sum += PlanarLinearDepth(eyeN[i]);
                }

                // Laplacian, normalised. Perspective: positive when the center is NEARER than its surroundings.
                float lap = (unity_OrthoParams.w == 0.0) ? (4.0 * lc - sum) / max(lc, 1e-6)
                                                         : (sum - 4.0 * lc) / max(lc, 1e-6);
                float depthEdge = saturate((lap - _DepthThreshold) * _EdgeSharpness / max(_DepthThreshold, 1e-4));
                // the sky itself is never inked
                depthEdge *= (eyeC < farPlane) ? 1.0 : 0.0;

                float edge = depthEdge;

            #if defined(_USE_NORMALS)
                if (eyeC < farPlane)
                {
                    float3 nC = LoadSceneNormals(ClampPixel(pixel));
                    float minDot = 1.0;
                    [unroll] for (int j = 0; j < 4; j++)
                    {
                        float3 nN = LoadSceneNormals(ClampPixel(pixel + o[j]));
                        // ignore neighbours that are sky or far behind (handled by depth edges)
                        bool valid = eyeN[j] < farPlane && abs(eyeN[j] - eyeC) < eyeC * 0.1;
                        minDot = valid ? min(minDot, dot(nC, nN)) : minDot;
                    }
                    float normalEdge = saturate(((1.0 - minDot) - _NormalThreshold) * _EdgeSharpness / max(_NormalThreshold, 1e-4));
                    edge = max(edge, normalEdge);
                }
            #endif

                float fade = 1.0 - saturate((eyeC - _FadeStart) / max(_FadeEnd - _FadeStart, 1e-3));
                return half4(_EdgeColor.rgb, _EdgeColor.a * edge * fade);
            }
            ENDHLSL
        }
    }
}
