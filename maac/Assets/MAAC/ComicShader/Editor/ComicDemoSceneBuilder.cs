using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace MAAC.ComicShader.Editor
{
    /// <summary>
    /// Builds Assets/MAAC/ComicShader/Demo/ComicShaderDemo.unity WITHOUT touching the currently open scenes
    /// (the scene is created additively, saved and closed again).
    /// Menu: Tools/Comic Shader/Build Demo Scene
    /// </summary>
    public static class ComicDemoSceneBuilder
    {
        public const string DemoFolder = "Assets/MAAC/ComicShader/Demo";
        public const string MaterialFolder = DemoFolder + "/Materials";
        public const string ScenePath = DemoFolder + "/ComicShaderDemo.unity";
        public const string TorusKnotPath = DemoFolder + "/TorusKnot.asset";

        [MenuItem("Tools/Comic Shader/Build Demo Scene")]
        public static void BuildMenu()
        {
            string path = Build();
            Debug.Log($"[Comic] Demo scene written to {path}");
        }

        public static string Build()
        {
            EnsureFolder(DemoFolder);
            EnsureFolder(MaterialFolder);

            Shader shader = Shader.Find(ComicMaterialUtility.ShaderName);
            if (shader == null)
                throw new System.Exception("Comic shader not found");

            // ---------------- materials ----------------
            Material matWhite = CreateMaterial(shader, "Comic_White", Color.white, m => { });
            Material matGray = CreateMaterial(shader, "Comic_LightGray", new Color(0.75f, 0.75f, 0.75f), m => { });
            Material matDark = CreateMaterial(shader, "Comic_DarkCloth", new Color(0.45f, 0.45f, 0.45f), m =>
            {
                // hatching only (no dots): classic pen shading
                m.SetFloat("_HalftoneEnable", 0f);
                m.SetFloat("_HatchingEnable", 1f);
                m.SetFloat("_HatchingThreshold1", 0.85f);
                m.SetFloat("_HatchingThreshold2", 0.45f);
                m.SetFloat("_ToneSteps", 3f);
            });
            Material matWorld = CreateMaterial(shader, "Comic_WorldTone", new Color(0.8f, 0.8f, 0.8f), m =>
            {
                m.SetFloat("_HalftoneSpace", (float)ComicMaterialUtility.PatternSpace.World);
                m.SetFloat("_HatchingEnable", 1f);
            });
            Material matObject = CreateMaterial(shader, "Comic_ObjectTone", new Color(0.9f, 0.9f, 0.9f), m =>
            {
                m.SetFloat("_HalftoneSpace", (float)ComicMaterialUtility.PatternSpace.Object);
                m.SetFloat("_HalftoneAngle", 15f);
            });
            Material matUV = CreateMaterial(shader, "Comic_UVTone", new Color(0.85f, 0.85f, 0.85f), m =>
            {
                m.SetFloat("_HalftoneSpace", (float)ComicMaterialUtility.PatternSpace.UV);
                m.SetFloat("_PatternUVScale", 96f);
            });
            Material matFloor = CreateMaterial(shader, "Comic_Floor", new Color(0.92f, 0.92f, 0.92f), m =>
            {
                m.SetFloat("_NoiseEnable", 1f);
                m.SetFloat("_OutlineEnable", 0f); // a plane has no silhouette worth outlining
            });
            Material matKnot = CreateMaterial(shader, "Comic_Knot", new Color(0.7f, 0.7f, 0.7f), m =>
            {
                m.SetFloat("_HatchingEnable", 1f);
                m.SetFloat("_NoiseEnable", 1f);
            });

            Mesh knot = CreateTorusKnot();

            // ---------------- scene ----------------
            Scene previousActive = SceneManager.GetActiveScene();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            try
            {
                SceneManager.SetActiveScene(scene);

                var camGO = new GameObject("Main Camera");
                camGO.tag = "MainCamera";
                var cam = camGO.AddComponent<Camera>();
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = Color.white;
                cam.fieldOfView = 45f;
                camGO.AddComponent<AudioListener>();
                var camData = camGO.AddComponent<UniversalAdditionalCameraData>();
                camData.renderShadows = true;
                camGO.transform.position = new Vector3(0f, 5.2f, -11.5f);
                camGO.transform.rotation = Quaternion.Euler(22f, 0f, 0f);
                int comicRenderer = ComicRendererSetup.CreateOrUpdate();
                if (comicRenderer >= 0)
                    camData.SetRenderer(comicRenderer);

                var sunGO = new GameObject("Directional Light");
                var sun = sunGO.AddComponent<Light>();
                sun.type = LightType.Directional;
                sun.intensity = 1.6f;
                sun.color = Color.white;
                sun.shadows = LightShadows.Soft;
                sunGO.transform.rotation = Quaternion.Euler(42f, -35f, 0f);
                var anim = sunGO.AddComponent<ComicDemoLightAnimator>();
                anim.enabled = false; // enable to animate light in Play Mode

                var pointGO = new GameObject("Point Light");
                var point = pointGO.AddComponent<Light>();
                point.type = LightType.Point;
                point.range = 6f;
                point.intensity = 3f;
                point.shadows = LightShadows.None; // quantized tones exaggerate point-shadow acne; enable if needed
                pointGO.transform.position = new Vector3(4.5f, 2.0f, -1.5f);

                RenderSettings.ambientMode = AmbientMode.Flat;
                RenderSettings.ambientLight = new Color(0.22f, 0.22f, 0.22f);
                RenderSettings.fog = false;

                var root = new GameObject("Shapes").transform;
                Prim(PrimitiveType.Plane, "Floor", new Vector3(0, 0, 0), Vector3.zero, new Vector3(3f, 1f, 2f), matFloor, root);
                Prim(PrimitiveType.Cube, "Wall", new Vector3(0, 2f, 6f), Vector3.zero, new Vector3(16f, 4f, 0.4f), matGray, root);
                Prim(PrimitiveType.Cube, "Cube", new Vector3(-4.2f, 0.75f, 0.5f), new Vector3(0, 30, 0), Vector3.one * 1.5f, matWhite, root);
                Prim(PrimitiveType.Sphere, "Sphere", new Vector3(-1.6f, 1f, -0.5f), Vector3.zero, Vector3.one * 2f, matWhite, root);
                Prim(PrimitiveType.Capsule, "Capsule", new Vector3(0.9f, 1f, 0.8f), Vector3.zero, Vector3.one, matDark, root);
                Prim(PrimitiveType.Cylinder, "Cylinder", new Vector3(3.2f, 1f, 0.2f), Vector3.zero, new Vector3(1.2f, 1f, 1.2f), matWorld, root);
                Prim(PrimitiveType.Sphere, "Sphere_ObjectSpace", new Vector3(-4.6f, 0.6f, -2.6f), Vector3.zero, Vector3.one * 1.2f, matObject, root);
                Prim(PrimitiveType.Cube, "Cube_UVSpace", new Vector3(5.3f, 0.6f, -2.6f), new Vector3(0, -20, 0), Vector3.one * 1.2f, matUV, root);

                // furniture-like compound: a table
                var table = new GameObject("Table").transform;
                table.SetParent(root);
                table.position = new Vector3(1.6f, 0f, 3.6f);
                Prim(PrimitiveType.Cube, "Top", table.position + new Vector3(0, 1.0f, 0), Vector3.zero, new Vector3(2.4f, 0.12f, 1.2f), matGray, table);
                foreach (var o in new[] { new Vector3(-1.05f, 0.47f, -0.45f), new Vector3(1.05f, 0.47f, -0.45f), new Vector3(-1.05f, 0.47f, 0.45f), new Vector3(1.05f, 0.47f, 0.45f) })
                    Prim(PrimitiveType.Cube, "Leg", table.position + o, Vector3.zero, new Vector3(0.12f, 0.94f, 0.12f), matDark, table);

                // stairs (building-like)
                var stairs = new GameObject("Stairs").transform;
                stairs.SetParent(root);
                for (int i = 0; i < 5; i++)
                    Prim(PrimitiveType.Cube, "Step" + i, new Vector3(-5.5f + 0f, 0.2f + i * 0.4f, 2.5f + i * 0.6f), Vector3.zero, new Vector3(2.2f, 0.4f, 0.6f), matWorld, stairs);

                // complex model: torus knot
                var knotGO = new GameObject("TorusKnot");
                knotGO.transform.SetParent(root);
                knotGO.transform.position = new Vector3(-0.8f, 1.6f, 3.2f);
                knotGO.transform.localScale = Vector3.one * 0.85f;
                knotGO.AddComponent<MeshFilter>().sharedMesh = knot;
                knotGO.AddComponent<MeshRenderer>().sharedMaterial = matKnot;

                EditorSceneManager.SaveScene(scene, ScenePath);
            }
            finally
            {
                if (previousActive.IsValid())
                    SceneManager.SetActiveScene(previousActive);
                EditorSceneManager.CloseScene(scene, true);
            }
            AssetDatabase.SaveAssets();
            return ScenePath;
        }

        static GameObject Prim(PrimitiveType type, string name, Vector3 pos, Vector3 euler, Vector3 scale, Material mat, Transform parent)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, true);
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(euler);
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            return go;
        }

        static Material CreateMaterial(Shader shader, string name, Color color, System.Action<Material> setup)
        {
            string path = $"{MaterialFolder}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            // reset to shader defaults so re-running the builder is deterministic
            var defaults = new Material(shader);
            mat.shader = shader;
            mat.CopyPropertiesFromMaterial(defaults);
            Object.DestroyImmediate(defaults);
            mat.SetColor("_BaseColor", color);
            setup(mat);
            ComicMaterialUtility.SetupMaterial(mat);
            mat.enableInstancing = true;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static Mesh CreateTorusKnot()
        {
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(TorusKnotPath);
            bool isNew = mesh == null;
            if (isNew) mesh = new Mesh();
            mesh.name = "TorusKnot";

            const int seg = 256, ring = 20, p = 2, q = 3;
            const float R = 1.0f, tube = 0.22f;
            var verts = new Vector3[(seg + 1) * (ring + 1)];
            var uvs = new Vector2[verts.Length];
            var tris = new int[seg * ring * 6];

            System.Func<float, Vector3> curve = t =>
            {
                float r = R * (2f + Mathf.Cos(q * t)) * 0.5f;
                return new Vector3(r * Mathf.Cos(p * t), R * Mathf.Sin(q * t) * 0.5f, r * Mathf.Sin(p * t));
            };

            for (int i = 0; i <= seg; i++)
            {
                float t = i / (float)seg * Mathf.PI * 2f;
                Vector3 c = curve(t);
                Vector3 n = curve(t + 0.01f);
                Vector3 T = (n - c).normalized;
                Vector3 B = Vector3.Cross(T, (n + c)).normalized;
                Vector3 N = Vector3.Cross(B, T);
                for (int j = 0; j <= ring; j++)
                {
                    float a = j / (float)ring * Mathf.PI * 2f;
                    int idx = i * (ring + 1) + j;
                    verts[idx] = c + (Mathf.Cos(a) * N + Mathf.Sin(a) * B) * tube;
                    uvs[idx] = new Vector2(i / (float)seg * 8f, j / (float)ring);
                }
            }
            int k = 0;
            for (int i = 0; i < seg; i++)
                for (int j = 0; j < ring; j++)
                {
                    int a = i * (ring + 1) + j, b = a + ring + 1;
                    tris[k++] = a; tris[k++] = a + 1; tris[k++] = b;
                    tris[k++] = b; tris[k++] = a + 1; tris[k++] = b + 1;
                }
            mesh.Clear();
            mesh.vertices = verts;
            mesh.uv = uvs;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();

            // make sure triangles face outwards (flip if the computed frame produced inward normals)
            Vector3 v0 = mesh.vertices[0], n0 = mesh.normals[0];
            Vector3 c0 = curve(0f);
            if (Vector3.Dot(v0 - c0, n0) < 0f)
            {
                for (int i = 0; i < tris.Length; i += 3) { int tmp = tris[i + 1]; tris[i + 1] = tris[i + 2]; tris[i + 2] = tmp; }
                mesh.triangles = tris;
                mesh.RecalculateNormals();
                mesh.RecalculateTangents();
            }
            ComicSmoothNormalBaker.WriteSmoothedNormalsToUV3(mesh);

            if (isNew) AssetDatabase.CreateAsset(mesh, TorusKnotPath);
            else EditorUtility.SetDirty(mesh);
            return mesh;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
