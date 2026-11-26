using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace GlowDust.EditorTools
{
    /// <summary>
    /// Builds an outdoor concept scene (forest, river, castle) rendered with the same glow-powder reveal,
    /// used for proposal images. Menu: GlowDust/Build World Concept Scene
    /// </summary>
    public static class GlowDustConceptBuilder
    {
        const string Root = "Assets/GlowDust";
        const string ScenePath = Root + "/Scenes/GlowDustWorldConcept.unity";

        [MenuItem("GlowDust/Build World Concept Scene")]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            int stage = LayerMask.NameToLayer("Stage");
            if (stage < 0) stage = 0;

            var matGround = Stone("M_Ground", new Color(0.40f, 0.50f, 0.40f), new Vector2(2.5f, 2.5f), 0.004f, 0.85f);
            var matBark   = Stone("M_Bark", new Color(0.55f, 0.42f, 0.34f), new Vector2(0.35f, 1.2f), 0.02f, 0.7f);
            var matLeaf   = Stone("M_Leaves", new Color(0.42f, 0.62f, 0.45f), new Vector2(0.6f, 0.6f), 0.004f, 0.9f);
            var matRock   = Stone("M_Rock", new Color(0.58f, 0.57f, 0.62f), new Vector2(1.2f, 0.8f), 0.006f, 0.8f);
            var matWater  = Stone("M_Water", new Color(0.35f, 0.55f, 0.85f), new Vector2(3f, 0.4f), 0.004f, 0.4f);
            var matWood   = Stone("M_Wood", new Color(0.62f, 0.48f, 0.36f), new Vector2(3f, 0.3f), 0.03f, 0.5f);
            var matWall   = AssetDatabase.LoadAssetAtPath<Material>(Root + "/Materials/M_Wall.mat");
            var matDust   = AssetDatabase.LoadAssetAtPath<Material>(Root + "/Materials/M_Dust.mat");
            var matSpark  = AssetDatabase.LoadAssetAtPath<Material>(Root + "/Materials/M_Spark.mat");
            var matMote   = AssetDatabase.LoadAssetAtPath<Material>(Root + "/Materials/M_WindMote.mat");
            var matFog    = AssetDatabase.LoadAssetAtPath<Material>(Root + "/Materials/M_PowderFog.mat");
            var matHeart  = Glow("M_NightHeart", new Color(1.6f, 0.25f, 2.4f), 0.6f);
            var matStar   = Glow("M_Star", new Color(0.55f, 0.6f, 0.9f), 0f);
            var profile   = AssetDatabase.LoadAssetAtPath<VolumeProfile>(Root + "/Settings/GlowDustPostProcess.asset");

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = Color.black;
            RenderSettings.fog = false;

            var world = new GameObject("World").transform;
            var rng = new System.Random(7);
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);

            // ground (with a river channel along z at x 58..66)
            Box(world, "Ground_West", new Vector3(29f, -0.5f, 60f), new Vector3(58f, 1f, 130f), matGround, stage);
            Box(world, "Ground_East", new Vector3(97f, -0.5f, 60f), new Vector3(62f, 1f, 130f), matGround, stage);
            Box(world, "RiverBed", new Vector3(62f, -2.75f, 60f), new Vector3(8f, 0.5f, 130f), matRock, stage);
            Box(world, "Bank_W", new Vector3(57.7f, -1.5f, 60f), new Vector3(0.6f, 2f, 130f), matRock, stage);
            Box(world, "Bank_E", new Vector3(66.3f, -1.5f, 60f), new Vector3(0.6f, 2f, 130f), matRock, stage);
            var water = Box(world, "Water", new Vector3(62f, -1.5f, 60f), new Vector3(8f, 0.05f, 130f), matWater, stage);
            Object.DestroyImmediate(water.GetComponent<Collider>());

            // bridge
            for (int i = 0; i < 12; i++)
                Box(world, "Plank" + i, new Vector3(62f, 0.1f, 38f + i * 0.42f), new Vector3(10f, 0.15f, 0.36f), matWood, stage);
            Box(world, "BridgeRail_A", new Vector3(62f, 0.9f, 37.9f), new Vector3(10f, 0.12f, 0.12f), matWood, stage);
            Box(world, "BridgeRail_B", new Vector3(62f, 0.9f, 42.7f), new Vector3(10f, 0.12f, 0.12f), matWood, stage);
            for (int i = 0; i < 4; i++)
            {
                Box(world, "Post_A" + i, new Vector3(57.5f + i * 3f, 0.4f, 37.9f), new Vector3(0.2f, 1.1f, 0.2f), matWood, stage);
                Box(world, "Post_B" + i, new Vector3(57.5f + i * 3f, 0.4f, 42.7f), new Vector3(0.2f, 1.1f, 0.2f), matWood, stage);
            }

            // forest (west side)
            var forest = new GameObject("Forest").transform; forest.SetParent(world);
            for (int i = 0; i < 70; i++)
            {
                Vector3 p = new Vector3(R(4f, 54f), 0f, R(6f, 120f));
                if (Mathf.Abs(p.x - 20f) < 2.5f && p.z < 40f) continue; // keep a path
                float h = R(4.5f, 8f);
                var trunk = Prim(PrimitiveType.Cylinder, forest, "Trunk" + i, p + Vector3.up * h * 0.5f, new Vector3(0.6f, h * 0.5f, 0.6f), matBark, stage);
                trunk.transform.rotation = Quaternion.Euler(R(-4f, 4f), R(0, 360), R(-4f, 4f));
                for (int k = 0; k < 3; k++)
                {
                    float s = R(2.6f, 4.2f);
                    Prim(PrimitiveType.Sphere, forest, $"Leaves{i}_{k}", p + new Vector3(R(-1.2f, 1.2f), h + R(-0.6f, 1.4f), R(-1.2f, 1.2f)), new Vector3(s, s * 0.8f, s), matLeaf, stage);
                }
            }
            for (int i = 0; i < 40; i++)
            {
                Vector3 p = new Vector3(R(2f, 56f), 0f, R(2f, 120f));
                float s = R(0.5f, 1.6f);
                var rock = Box(world, "Rock" + i, p + Vector3.up * s * 0.25f, new Vector3(s * 1.3f, s * 0.8f, s), matRock, stage);
                rock.transform.rotation = Quaternion.Euler(R(-15, 15), R(0, 360), R(-15, 15));
            }

            // castle (east side, on a hill)
            var castle = new GameObject("Castle").transform; castle.SetParent(world);
            Vector3 c = new Vector3(100f, 0f, 85f);
            Box(castle, "Hill", c + new Vector3(0, 2f, 0), new Vector3(46f, 4f, 40f), matRock, stage);
            float baseY = 4f;
            Box(castle, "Wall_S", c + new Vector3(0, baseY + 5f, -16f), new Vector3(36f, 10f, 2f), matWall, stage);
            Box(castle, "Wall_N", c + new Vector3(0, baseY + 5f, 16f), new Vector3(36f, 10f, 2f), matWall, stage);
            Box(castle, "Wall_W", c + new Vector3(-18f, baseY + 5f, 0), new Vector3(2f, 10f, 32f), matWall, stage);
            Box(castle, "Wall_E", c + new Vector3(18f, baseY + 5f, 0), new Vector3(2f, 10f, 32f), matWall, stage);
            Box(castle, "Gate", c + new Vector3(0, baseY + 2.5f, -16.6f), new Vector3(5f, 5f, 1.4f), matWood, stage);
            foreach (var o in new[] { new Vector3(-18, 0, -16), new Vector3(18, 0, -16), new Vector3(-18, 0, 16), new Vector3(18, 0, 16) })
            {
                Prim(PrimitiveType.Cylinder, castle, "Tower", c + o + Vector3.up * (baseY + 8f), new Vector3(6f, 8f, 6f), matWall, stage);
                Prim(PrimitiveType.Cylinder, castle, "TowerTop", c + o + Vector3.up * (baseY + 16.5f), new Vector3(7f, 0.5f, 7f), matWall, stage);
            }
            for (int i = -8; i <= 8; i += 2)
                Box(castle, "Merlon", c + new Vector3(i * 2f, baseY + 10.6f, -16f), new Vector3(1.6f, 1.2f, 2f), matWall, stage);
            Box(castle, "Keep", c + new Vector3(0, baseY + 12f, 4f), new Vector3(16f, 24f, 14f), matWall, stage);
            Prim(PrimitiveType.Cylinder, castle, "Spire", c + new Vector3(0, baseY + 30f, 4f), new Vector3(7f, 6f, 7f), matWall, stage);
            Prim(PrimitiveType.Cylinder, castle, "SpireRoof", c + new Vector3(0, baseY + 36.5f, 4f), new Vector3(8.5f, 0.5f, 8.5f), matWall, stage);
            // the dark heart at the top floor: visible through a window
            var heart = GameObject.CreatePrimitive(PrimitiveType.Cube);
            heart.name = "NightHeart";
            Object.DestroyImmediate(heart.GetComponent<Collider>());
            heart.transform.SetParent(castle);
            heart.transform.position = c + new Vector3(0, baseY + 39.5f, 4f);
            heart.transform.rotation = Quaternion.Euler(45f, 20f, 45f);
            heart.transform.localScale = Vector3.one * 2.2f;
            heart.GetComponent<MeshRenderer>().sharedMaterial = matHeart;

            // faint stars
            var sky = new GameObject("Stars").transform; sky.SetParent(world);
            for (int i = 0; i < 260; i++)
            {
                Vector3 d = Random.onUnitSphere; d.y = Mathf.Abs(d.y) * 0.9f + 0.1f;
                var st = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                Object.DestroyImmediate(st.GetComponent<Collider>());
                st.name = "Star";
                st.transform.SetParent(sky);
                st.transform.position = new Vector3(60f, 0f, 60f) + d.normalized * 380f;
                st.transform.localScale = Vector3.one * R(0.6f, 1.6f);
                st.GetComponent<MeshRenderer>().sharedMaterial = matStar;
            }

            // systems (coarser cells: big outdoor area)
            var systems = new GameObject("GlowDustSystems");
            var volume = systems.AddComponent<PowderVolume>();
            volume.origin = new Vector3(-2f, -4f, -2f);
            volume.size = new Vector3(130f, 50f, 126f);
            volume.cellSize = 1f;
            volume.obstacleMask = 1 << stage;
            var wind = systems.AddComponent<WindField>();
            wind.globalDirection = new Vector3(0.4f, 0.05f, 1f);
            wind.globalStrength = 0.5f;
            var powder = systems.AddComponent<PowderSystem>();
            powder.volume = volume; powder.wind = wind;
            powder.dustMaterial = matDust; powder.sparkMaterial = matSpark; powder.moteMaterial = matMote;
            powder.maxDustParticles = 16000;
            powder.impactRadius = 7f;
            powder.flashRadius = 11f;
            var river = new GameObject("Wind_River_Downstream").AddComponent<WindVolume>();
            river.transform.SetParent(systems.transform);
            river.transform.position = new Vector3(62f, 0f, 60f);
            river.size = new Vector3(12f, 8f, 130f);
            river.direction = new Vector3(0f, 0.05f, 1f);
            river.strength = 2.4f; river.turbulence = 0.5f;
            var up = new GameObject("Wind_Castle_Updraft").AddComponent<WindVolume>();
            up.transform.SetParent(systems.transform);
            up.transform.position = c + new Vector3(0, 20f, 0);
            up.size = new Vector3(50f, 40f, 44f);
            up.direction = new Vector3(0f, 1f, 0.3f);
            up.strength = 1.6f; up.turbulence = 0.6f;

            var fog = GameObject.CreatePrimitive(PrimitiveType.Cube);
            fog.name = "PowderFog";
            Object.DestroyImmediate(fog.GetComponent<Collider>());
            fog.transform.SetParent(systems.transform);
            fog.GetComponent<MeshRenderer>().sharedMaterial = matFog;
            fog.AddComponent<PowderFogVolume>().volume = volume;

            // camera
            var camGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            camGo.tag = "MainCamera";
            var cam = camGo.GetComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.01f, 0.01f, 0.025f);
            cam.fieldOfView = 70f; cam.nearClipPlane = 0.05f; cam.farClipPlane = 600f;
            var data = cam.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            data.requiresDepthOption = CameraOverrideOption.On;
            camGo.transform.SetPositionAndRotation(new Vector3(20f, 1.6f, 4f), Quaternion.identity);
            volume.viewer = camGo.transform;
            powder.viewer = camGo.transform;

            var pp = new GameObject("PostProcessVolume").AddComponent<Volume>();
            pp.isGlobal = true; pp.sharedProfile = profile;

            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log("[GlowDust] World concept scene built: " + ScenePath);
        }

        static Material Stone(string name, Color col, Vector2 block, float mortar, float variation)
        {
            string path = $"{Root}/Materials/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(Shader.Find("GlowDust/RevealStone")); AssetDatabase.CreateAsset(m, path); }
            m.SetColor("_BaseColor", col);
            m.SetVector("_BlockSize", new Vector4(block.x, block.y, 0, 0));
            m.SetFloat("_MortarWidth", mortar);
            m.SetFloat("_Variation", variation);
            m.SetFloat("_Exposure", 1.5f);
            m.SetFloat("_DensityGain", 0.5f);
            EditorUtility.SetDirty(m);
            return m;
        }

        static Material Glow(string name, Color col, float pulse)
        {
            string path = $"{Root}/Materials/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(Shader.Find("GlowDust/GlowUnlit")); AssetDatabase.CreateAsset(m, path); }
            m.SetColor("_Color", col);
            m.SetFloat("_Pulse", pulse);
            EditorUtility.SetDirty(m);
            return m;
        }

        static GameObject Box(Transform parent, string name, Vector3 pos, Vector3 size, Material mat, int layer)
            => Prim(PrimitiveType.Cube, parent, name, pos, size, mat, layer);

        static GameObject Prim(PrimitiveType type, Transform parent, string name, Vector3 pos, Vector3 scale, Material mat, int layer)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.localScale = scale;
            go.layer = layer;
            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            return go;
        }
    }
}
