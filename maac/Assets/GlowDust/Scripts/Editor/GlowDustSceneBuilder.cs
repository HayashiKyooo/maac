using System.Collections.Generic;
using System.Linq;
using StarterAssets;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace GlowDust.EditorTools
{
    /// <summary>
    /// Builds the GlowDust test maze scene from an ASCII map.  Menu: GlowDust/Build Test Scene
    /// Legend: # wall, . floor, S start, G goal, n narrow corridor, o pillar, x rubble,
    ///         1-9 raised floor (digit * 0.4m), _ pit (abyss), b beam over a pit, m moving-platform track (pit),
    ///         c cracked floor (crumbles), p powder pickup
    /// </summary>
    public static class GlowDustSceneBuilder
    {
        const string Root = "Assets/GlowDust";
        const string ScenePath = Root + "/Scenes/GlowDustMaze.unity";
        const string PlayerPrefabPath = "Assets/Starter Assets/Runtime/FirstPersonController/Prefabs/PlayerCapsule.prefab";
        const float Cell = 3f;
        const float WallHeight = 6f;
        const float StepHeight = 0.4f;
        const float PitDepth = 6f;

        // row 0 = south (z = 0). Written north -> south for readability.
        static readonly string[] MapNorthToSouth =
        {
            "####################",
            "#p........._###6666#",
            "#....x.....b2466G66#",
            "#......o..._###6666#",
            "#..o.......#######6#",
            "#...___c.x.#######5#",
            "#..####c##########4#",
            "#..#..o....#######3#",
            "#_.#._.__._#######2#",
            "#..n..._...#######1#",
            "#cc#_.o.._.#######.#",
            "#cc#..__...#______m#",
            "#..#p.o....#______m#",
            "#..#####.###______m#",
            "#...####.###._____.#",
            "#SS...b.._...mmmmm.#",
            "#SS.####p###._____p#",
            "####################",
        };

        static string[] map;
        static int rows, cols;
        static int stageLayer;

        [MenuItem("GlowDust/Build Test Scene")]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            map = MapNorthToSouth.Reverse().ToArray();
            rows = map.Length; cols = map[0].Length;
            EnsureFolders();
            stageLayer = EnsureLayer("Stage");
            int playerLayer = EnsureLayer("Player");

            // ---------- materials
            var matWall    = StoneMat("M_Wall",    new Color(0.62f, 0.60f, 0.68f), new Vector2(1.1f, 0.55f), 0.06f);
            var matFloor   = StoneMat("M_Floor",   new Color(0.50f, 0.50f, 0.56f), new Vector2(1.5f, 1.5f), 0.05f);
            var matPillar  = StoneMat("M_Pillar",  new Color(0.72f, 0.68f, 0.74f), new Vector2(0.7f, 0.9f), 0.05f);
            var matCrate   = StoneMat("M_Rubble",  new Color(0.70f, 0.58f, 0.52f), new Vector2(0.5f, 0.35f), 0.04f);
            var matCeiling = StoneMat("M_Ceiling", new Color(0.40f, 0.40f, 0.46f), new Vector2(2.0f, 1.0f), 0.05f);
            var matDust  = GlowAdditiveMat("M_Dust", 3.5f, 2f, 1f);
            var matSpark = GlowAdditiveMat("M_Spark", 3f, 1.5f, 1f);
            var matMote  = GlowAdditiveMat("M_WindMote", 2.5f, 2f, 1f);
            var matTrail = GlowAdditiveMat("M_Trail", 3f, 1.5f, 0f);
            var matBall  = GlowUnlitMat("M_PowderBall", new Color(1.6f, 0.9f, 3.2f), 0.2f);
            var matGoal  = GlowUnlitMat("M_GoalCrystal", new Color(3.5f, 2.4f, 0.7f), 0.35f);
            var matCracked  = StoneMat("M_CrackedFloor", new Color(0.78f, 0.52f, 0.48f), new Vector2(0.45f, 0.45f), 0.09f);
            var matPlatform = StoneMat("M_Platform", new Color(0.55f, 0.7f, 0.68f), new Vector2(0.6f, 0.6f), 0.07f);
            var matPickup   = GlowUnlitMat("M_PowderPickup", new Color(0.9f, 0.55f, 1.8f), 0.5f);
            var matFog   = LoadOrCreate("M_PowderFog", "GlowDust/PowderFog");
            var profile  = BuildVolumeProfile();

            // ---------- scene
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = Color.black;
            RenderSettings.fog = false;
            RenderSettings.reflectionIntensity = 0f;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;

            // ---------- stage
            var stage = new GameObject("Stage").transform;
            BuildStage(stage, matWall, matFloor, matPillar, matCrate, matCeiling, matCracked, matPlatform, matPickup);

            // ---------- systems
            var systems = new GameObject("GlowDustSystems");
            var volume = systems.AddComponent<PowderVolume>();
            volume.origin = new Vector3(-0.6f, -4.2f, -0.6f); // extends down into the pits
            volume.size = new Vector3(cols * Cell + 1.2f, WallHeight + 4.8f, rows * Cell + 1.2f);
            volume.cellSize = 0.6f;
            volume.obstacleMask = 1 << stageLayer;
            var wind = systems.AddComponent<WindField>();
            wind.globalDirection = new Vector3(0.3f, 0f, 1f);
            wind.globalStrength = 0.3f;
            var powder = systems.AddComponent<PowderSystem>();
            powder.volume = volume;
            powder.wind = wind;
            powder.dustMaterial = matDust;
            powder.sparkMaterial = matSpark;
            powder.moteMaterial = matMote;

            BuildWindVolumes(systems.transform);

            // volumetric glow smoke (ray-marches the same density volume)
            var fog = GameObject.CreatePrimitive(PrimitiveType.Cube);
            fog.name = "PowderFog";
            Object.DestroyImmediate(fog.GetComponent<Collider>());
            fog.transform.SetParent(systems.transform, false);
            fog.transform.position = volume.origin + volume.size * 0.5f;
            fog.transform.localScale = volume.size;
            fog.GetComponent<MeshRenderer>().sharedMaterial = matFog;
            fog.AddComponent<PowderFogVolume>().volume = volume;

            // ---------- player (Starter Assets FirstPersonController, reused)
            Vector3 start = Vector3.zero; int sCount = 0;
            for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
                if (map[r][c] == 'S') { start += CellCenter(c, r); sCount++; }
            start = sCount > 0 ? start / sCount : CellCenter(1, 1);
            var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            GameObject player;
            if (playerPrefab != null)
                player = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab, scene);
            else
            {
                Debug.LogError("[GlowDust] Starter Assets PlayerCapsule prefab not found.");
                return;
            }
            player.name = "Player";
            player.transform.SetPositionAndRotation(start + Vector3.up * 0.05f, Quaternion.Euler(0, 90, 0));
            var fpc = player.GetComponent<FirstPersonController>();
            // The player must NOT be on the Stage layer, otherwise the ground check hits its own collider.
            foreach (var tr in player.GetComponentsInChildren<Transform>(true)) tr.gameObject.layer = playerLayer;
            fpc.GroundLayers = 1 << stageLayer;
            fpc.MoveSpeed = 4f;
            fpc.SprintSpeed = 6.5f;
            fpc.JumpHeight = 1.25f;
            fpc.RotationSpeed = 2.2f; // mouse look sensitivity (Starter Assets default: 1)
            var cc = player.GetComponent<CharacterController>();
            cc.stepOffset = 0.45f;
            cc.radius = 0.4f;
            fpc.GroundedRadius = 0.4f;

            Transform camRoot = fpc.CinemachineCameraTarget != null ? fpc.CinemachineCameraTarget.transform : player.transform;
            var camGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            camGo.tag = "MainCamera";
            camGo.transform.SetParent(camRoot, false);
            var cam = camGo.GetComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 150f;
            cam.fieldOfView = 70f;
            var camData = cam.GetUniversalAdditionalCameraData();
            camData.renderPostProcessing = true;
            camData.requiresDepthOption = CameraOverrideOption.On; // smoke stops at walls using the depth texture
            camData.antialiasing = AntialiasingMode.FastApproximateAntialiasing;

            volume.viewer = camGo.transform;
            powder.viewer = camGo.transform;

            // projectile template + pool
            var pool = new GameObject("PowderBallPool").transform;
            var ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ball.name = "PowderBall";
            ball.transform.SetParent(pool, false);
            ball.transform.localScale = Vector3.one * 0.22f;
            ball.GetComponent<MeshRenderer>().sharedMaterial = matBall;
            ball.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            var rb = ball.AddComponent<Rigidbody>();
            rb.mass = 0.4f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            var proj = ball.AddComponent<PowderProjectile>();
            var trailGo = new GameObject("Trail", typeof(TrailRenderer));
            trailGo.transform.SetParent(ball.transform, false);
            var trail = trailGo.GetComponent<TrailRenderer>();
            trail.sharedMaterial = matTrail;
            trail.time = 0.35f;
            trail.widthMultiplier = 0.16f;
            trail.minVertexDistance = 0.05f;
            trail.shadowCastingMode = ShadowCastingMode.Off;
            var grad = new Gradient();
            grad.SetKeys(new[] { new GradientColorKey(new Color(0.8f, 0.6f, 1f), 0f), new GradientColorKey(new Color(0.3f, 0.85f, 1f), 1f) },
                         new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            trail.colorGradient = grad;
            ball.SetActive(false);

            var thrower = player.AddComponent<PowderThrower>();
            thrower.viewCamera = cam;
            thrower.projectileTemplate = proj;
            thrower.startCount = 10;
            thrower.maxCount = 20;

            // ---------- game / HUD
            var gameGo = new GameObject("Game");
            var game = gameGo.AddComponent<GlowDustGame>();
            var hud = gameGo.AddComponent<GlowDustHUD>();
            game.player = player.transform;
            game.thrower = thrower;
            game.hud = hud;
            game.killY = -3f;

            // gimmicks need the player
            foreach (var d in stage.GetComponentsInChildren<DynamicStageElement>(true)) d.player = cc;
            foreach (var pu in stage.GetComponentsInChildren<PowderPickup>(true)) { pu.thrower = thrower; pu.hud = hud; }

            // ---------- goal
            Vector3 goalPos = Vector3.zero;
            for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
                if (map[r][c] == 'G') goalPos = CellCenter(c, r) + Vector3.up * CellHeight('G');
            var goalGo = new GameObject("Goal");
            goalGo.transform.position = goalPos;
            var pedestal = Box("Pedestal", goalGo.transform, goalPos + Vector3.up * 0.35f, new Vector3(1.2f, 0.7f, 1.2f), matPillar);
            pedestal.transform.SetParent(goalGo.transform, true);
            var crystal = GameObject.CreatePrimitive(PrimitiveType.Cube);
            crystal.name = "Crystal";
            Object.DestroyImmediate(crystal.GetComponent<Collider>());
            crystal.transform.SetParent(goalGo.transform, false);
            crystal.transform.localPosition = new Vector3(0, 1.45f, 0);
            crystal.transform.localRotation = Quaternion.Euler(45f, 0f, 45f);
            crystal.transform.localScale = new Vector3(0.55f, 0.55f, 0.55f);
            crystal.GetComponent<MeshRenderer>().sharedMaterial = matGoal;
            var goal = goalGo.AddComponent<GoalZone>();
            goal.game = game;
            goal.player = player.transform;
            goal.crystal = crystal.transform;
            goal.moteMaterial = matMote;
            goal.zoneSize = new Vector3(3f, 3f, 3f);

            // ---------- post processing
            var ppGo = new GameObject("PostProcessVolume");
            var vol = ppGo.AddComponent<Volume>();
            vol.isGlobal = true;
            vol.sharedProfile = profile;

            EditorSceneManager.SaveScene(scene, ScenePath);
            AddToBuildSettings(ScenePath);
            Debug.Log("[GlowDust] Test scene built: " + ScenePath);
        }

        // ------------------------------------------------------------------ stage

        static Vector3 CellCenter(float c, float r) => new Vector3(c * Cell + Cell * 0.5f, 0f, r * Cell + Cell * 0.5f);

        static float CellHeight(char ch)
        {
            if (ch >= '1' && ch <= '9') return (ch - '0') * StepHeight;
            if (ch == 'G') return 6 * StepHeight;
            return 0f;
        }

        static char At(int c, int r) => (r < 0 || r >= rows || c < 0 || c >= cols) ? '#' : map[r][c];
        static bool IsWall(char ch) => ch == '#';
        static bool IsPit(char ch) => ch == '_' || ch == 'm' || ch == 'b';

        static void BuildStage(Transform stage, Material wall, Material floor, Material pillar, Material crate, Material ceiling,
                               Material cracked, Material platformMat, Material pickupMat)
        {
            float w = cols * Cell, d = rows * Cell;
            var floors = new GameObject("Floors").transform; floors.SetParent(stage);
            var walls = new GameObject("Walls").transform; walls.SetParent(stage);
            var props = new GameObject("Props").transform; props.SetParent(stage);
            var gimmicks = new GameObject("Gimmicks").transform; gimmicks.SetParent(stage);

            Box("Ceiling", stage, new Vector3(w * 0.5f, WallHeight + 0.25f, d * 0.5f), new Vector3(w, 0.5f, d), ceiling);

            float wallBottom = -PitDepth, wallH = WallHeight + PitDepth;
            for (int r = 0; r < rows; r++)
            {
                int c = 0;
                while (c < cols)
                {
                    // walls (merged runs, they also form the sides of the pits)
                    if (IsWall(map[r][c]))
                    {
                        int c0 = c;
                        while (c < cols && IsWall(map[r][c])) c++;
                        Box($"Wall_r{r}_c{c0}", walls, new Vector3((c0 + c) * Cell * 0.5f, wallBottom + wallH * 0.5f, r * Cell + Cell * 0.5f),
                            new Vector3((c - c0) * Cell, wallH, Cell), wall);
                        continue;
                    }
                    // plain floor (merged runs)
                    if (IsPlainFloor(map[r][c]))
                    {
                        int c0 = c;
                        while (c < cols && IsPlainFloor(map[r][c])) c++;
                        Box($"Floor_r{r}_c{c0}", floors, new Vector3((c0 + c) * Cell * 0.5f, -0.25f, r * Cell + Cell * 0.5f),
                            new Vector3((c - c0) * Cell, 0.5f, Cell), floor);
                        continue;
                    }
                    c++;
                }
            }

            var rng = new System.Random(1234);
            for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
            {
                char ch = map[r][c];
                Vector3 cc = CellCenter(c, r);
                float h = CellHeight(ch);
                if (h > 0f)
                    Box($"Raised_r{r}_c{c}", floors, cc + Vector3.up * ((h - 0.5f) * 0.5f), new Vector3(Cell, h + 0.5f, Cell), floor);

                if (IsPit(ch)) BuildPitLiners(c, r, walls, wall);

                switch (ch)
                {
                    case 'n':
                    {
                        bool alongZ = IsWall(At(c - 1, r)) && IsWall(At(c + 1, r));
                        float t = 0.8f;
                        if (alongZ)
                        {
                            Box($"Narrow_r{r}_c{c}_A", walls, cc + new Vector3(-Cell * 0.5f + t * 0.5f, WallHeight * 0.5f, 0), new Vector3(t, WallHeight, Cell), wall);
                            Box($"Narrow_r{r}_c{c}_B", walls, cc + new Vector3(Cell * 0.5f - t * 0.5f, WallHeight * 0.5f, 0), new Vector3(t, WallHeight, Cell), wall);
                        }
                        else
                        {
                            Box($"Narrow_r{r}_c{c}_A", walls, cc + new Vector3(0, WallHeight * 0.5f, -Cell * 0.5f + t * 0.5f), new Vector3(Cell, WallHeight, t), wall);
                            Box($"Narrow_r{r}_c{c}_B", walls, cc + new Vector3(0, WallHeight * 0.5f, Cell * 0.5f - t * 0.5f), new Vector3(Cell, WallHeight, t), wall);
                        }
                        break;
                    }
                    case 'o':
                    {
                        var p = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                        Setup(p, $"Pillar_r{r}_c{c}", props, cc + Vector3.up * (WallHeight * 0.5f + h), new Vector3(1.3f, WallHeight * 0.5f, 1.3f), pillar);
                        Box($"PillarBase_r{r}_c{c}", props, cc + Vector3.up * (0.2f + h), new Vector3(1.9f, 0.4f, 1.9f), pillar);
                        Box($"PillarCap_r{r}_c{c}", props, cc + Vector3.up * (WallHeight - 0.25f), new Vector3(1.8f, 0.5f, 1.8f), pillar);
                        break;
                    }
                    case 'x':
                    {
                        float yaw = (float)rng.NextDouble() * 35f;
                        var b = Box($"Block_r{r}_c{c}", props, cc + Vector3.up * (0.55f + h), new Vector3(1.5f, 1.1f, 1.5f), crate);
                        b.transform.rotation = Quaternion.Euler(0, yaw, 0);
                        var b2 = Box($"Block_r{r}_c{c}_Top", props, cc + new Vector3(0.2f, 1.45f + h, -0.1f), new Vector3(0.9f, 0.7f, 0.9f), crate);
                        b2.transform.rotation = Quaternion.Euler(0, yaw + 20f, 0);
                        var b3 = Box($"Block_r{r}_c{c}_Side", props, cc + new Vector3(-1.0f, 0.3f + h, 0.8f), new Vector3(0.6f, 0.6f, 0.6f), crate);
                        b3.transform.rotation = Quaternion.Euler(0, yaw - 15f, 0);
                        break;
                    }
                    case 'b':
                    {
                        // narrow beam across the pit, oriented toward the walkable neighbours
                        bool alongX = !IsWall(At(c - 1, r)) && !IsPit(At(c - 1, r)) || !IsWall(At(c + 1, r)) && !IsPit(At(c + 1, r));
                        Vector3 size = alongX ? new Vector3(Cell, 0.5f, 0.9f) : new Vector3(0.9f, 0.5f, Cell);
                        Box($"Beam_r{r}_c{c}", floors, cc + Vector3.down * 0.25f, size, floor);
                        break;
                    }
                    case 'c':
                    {
                        var tile = Box($"Cracked_r{r}_c{c}", gimmicks, cc + Vector3.down * 0.25f, new Vector3(Cell - 0.04f, 0.5f, Cell - 0.04f), cracked);
                        MakeDynamic(tile);
                        tile.AddComponent<CrumbleTile>();
                        BuildPitLiners(c, r, walls, wall, true);
                        break;
                    }
                    case 'p':
                    {
                        var pu = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        Object.DestroyImmediate(pu.GetComponent<Collider>());
                        pu.name = $"PowderPickup_r{r}_c{c}";
                        pu.transform.SetParent(gimmicks, false);
                        pu.transform.position = cc + Vector3.up * (0.8f + h);
                        pu.transform.rotation = Quaternion.Euler(45f, 0f, 45f);
                        pu.transform.localScale = Vector3.one * 0.3f;
                        var mr = pu.GetComponent<MeshRenderer>();
                        mr.sharedMaterial = pickupMat;
                        mr.shadowCastingMode = ShadowCastingMode.Off;
                        pu.AddComponent<PowderPickup>().amount = 3;
                        break;
                    }
                }
            }

            // moving platforms: each straight run of 'm' cells is one platform track
            var used = new bool[rows, cols];
            int index = 0;
            for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
            {
                if (map[r][c] != 'm' || used[r, c]) continue;
                int c1 = c, r1 = r;
                while (At(c1 + 1, r) == 'm') c1++;
                if (c1 == c) while (At(c, r1 + 1) == 'm') r1++;
                for (int rr = r; rr <= r1; rr++) for (int cc2 = c; cc2 <= c1; cc2++) used[rr, cc2] = true;
                Vector3 a = CellCenter(c, r) + Vector3.down * 0.25f;
                Vector3 b = CellCenter(c1, r1) + Vector3.down * 0.25f;
                var plat = Box($"MovingPlatform_{index}", gimmicks, a, new Vector3(2.5f, 0.5f, 2.5f), platformMat);
                MakeDynamic(plat);
                var mp = plat.AddComponent<MovingPlatform>();
                mp.pointA = a; mp.pointB = b;
                mp.speed = 1.8f; mp.waitTime = 1.0f;
                mp.startPhase = (index * 0.37f) % 1f;
                index++;
            }
        }

        static bool IsPlainFloor(char ch) => !IsWall(ch) && !IsPit(ch) && ch != 'c' && CellHeight(ch) <= 0f;

        static void MakeDynamic(GameObject go) => GameObjectUtility.SetStaticEditorFlags(go, 0);

        /// <summary>Vertical liners under the floor edges so the pits look like deep shafts.</summary>
        static void BuildPitLiners(int c, int r, Transform parent, Material mat, bool underCrumble = false)
        {
            Vector3 cc = CellCenter(c, r);
            float top = -0.5f, bottom = -PitDepth, hgt = top - bottom, t = 0.3f;
            void Liner(int dc, int dr)
            {
                char n = At(c + dc, r + dr);
                if (IsWall(n)) return;
                if (!underCrumble && (IsPit(n) || n == 'c')) return;
                if (underCrumble && (IsPit(n) || n == 'c')) return;
                Vector3 off = new Vector3(dc * (Cell * 0.5f - t * 0.5f), bottom + hgt * 0.5f, dr * (Cell * 0.5f - t * 0.5f));
                Vector3 size = dc != 0 ? new Vector3(t, hgt, Cell) : new Vector3(Cell, hgt, t);
                Box($"PitLiner_r{r}_c{c}_{dc}{dr}", parent, cc + off, size, mat);
            }
            Liner(1, 0); Liner(-1, 0); Liner(0, 1); Liner(0, -1);
        }

        static GameObject Box(string name, Transform parent, Vector3 center, Vector3 size, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Setup(go, name, parent, center, size, mat);
            return go;
        }

        static void Setup(GameObject go, string name, Transform parent, Vector3 center, Vector3 scale, Material mat)
        {
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.position = center;
            go.transform.localScale = scale;
            go.layer = stageLayer;
            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);
        }

        static void BuildWindVolumes(Transform parent)
        {
            var root = new GameObject("WindVolumes").transform;
            root.SetParent(parent, false);
            // (name, col range, row range, direction, strength, turbulence)
            Wind(root, "Wind_SouthCorridor_East", 3, 11, 2, 2, new Vector3(1, 0, 0), 2.0f, 0.35f);
            Wind(root, "Wind_Chasm_Updraft", 12, 18, 1, 6, new Vector3(0, 0.45f, 1), 1.0f, 0.55f);
            Wind(root, "Wind_StairShaft_Up", 18, 18, 7, 13, new Vector3(0, 0.25f, 1), 1.5f, 0.3f);
            Wind(root, "Wind_GoalRoom_Swirl", 15, 18, 14, 16, new Vector3(-1, 0, 0.3f), 0.5f, 0.6f);
            Wind(root, "Wind_WestCorridor_North", 1, 2, 3, 11, new Vector3(0, 0, 1), 1.0f, 0.3f);
            Wind(root, "Wind_PillarHall_West", 4, 10, 5, 10, new Vector3(-1, 0, 0.2f), 0.8f, 0.45f);
            Wind(root, "Wind_NorthRoom_East", 1, 10, 12, 16, new Vector3(1, 0, 0), 1.2f, 0.4f);
            Wind(root, "Wind_Bridge_East", 11, 14, 14, 16, new Vector3(1, 0.15f, 0), 1.0f, 0.35f);
        }

        static void Wind(Transform parent, string name, int c0, int c1, int r0, int r1, Vector3 dir, float strength, float turb)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            float x0 = c0 * Cell, x1 = (c1 + 1) * Cell, z0 = r0 * Cell, z1 = (r1 + 1) * Cell;
            go.transform.position = new Vector3((x0 + x1) * 0.5f, (WallHeight - PitDepth) * 0.5f, (z0 + z1) * 0.5f);
            var wv = go.AddComponent<WindVolume>();
            wv.size = new Vector3(x1 - x0 + 1f, WallHeight + PitDepth + 1f, z1 - z0 + 1f);
            wv.direction = dir;
            wv.strength = strength;
            wv.turbulence = turb;
            wv.edgeSoftness = 1.5f;
        }

        // ------------------------------------------------------------------ assets

        static void EnsureFolders()
        {
            foreach (var f in new[] { "Materials", "Scenes", "Settings" })
                if (!AssetDatabase.IsValidFolder(Root + "/" + f)) AssetDatabase.CreateFolder(Root, f);
        }

        static int EnsureLayer(string name)
        {
            int l = LayerMask.NameToLayer(name);
            if (l >= 0) return l;
            var tm = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers = tm.FindProperty("layers");
            for (int i = 8; i < 32; i++)
            {
                var sp = layers.GetArrayElementAtIndex(i);
                if (string.IsNullOrEmpty(sp.stringValue))
                {
                    sp.stringValue = name;
                    tm.ApplyModifiedPropertiesWithoutUndo();
                    return i;
                }
            }
            Debug.LogWarning("[GlowDust] No free layer slot, using Default.");
            return 0;
        }

        static Material LoadOrCreate(string name, string shaderName)
        {
            string path = $"{Root}/Materials/{name}.mat";
            var shader = Shader.Find(shaderName);
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(mat, path);
            }
            else mat.shader = shader;
            return mat;
        }

        static Material StoneMat(string name, Color baseColor, Vector2 block, float mortar)
        {
            var m = LoadOrCreate(name, "GlowDust/RevealStone");
            m.SetColor("_BaseColor", baseColor);
            m.SetVector("_BlockSize", new Vector4(block.x, block.y, 0, 0));
            m.SetFloat("_MortarWidth", mortar);
            m.SetFloat("_Exposure", 1.5f);
            m.SetFloat("_DensityGain", 0.5f);
            EditorUtility.SetDirty(m);
            return m;
        }

        static Material GlowAdditiveMat(string name, float intensity, float softness, float radialX)
        {
            var m = LoadOrCreate(name, "GlowDust/GlowAdditive");
            m.SetFloat("_Intensity", intensity);
            m.SetFloat("_Softness", softness);
            m.SetFloat("_RadialX", radialX);
            EditorUtility.SetDirty(m);
            return m;
        }

        static Material GlowUnlitMat(string name, Color c, float pulse)
        {
            var m = LoadOrCreate(name, "GlowDust/GlowUnlit");
            m.SetColor("_Color", c);
            m.SetFloat("_Pulse", pulse);
            EditorUtility.SetDirty(m);
            return m;
        }

        static VolumeProfile BuildVolumeProfile()
        {
            string path = Root + "/Settings/GlowDustPostProcess.asset";
            var existing = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (existing != null) AssetDatabase.DeleteAsset(path);
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, path);

            var bloom = profile.Add<Bloom>(true);
            bloom.threshold.Override(0.75f);
            bloom.intensity.Override(1.4f);
            bloom.scatter.Override(0.72f);
            var tone = profile.Add<Tonemapping>(true);
            tone.mode.Override(TonemappingMode.ACES);
            var vig = profile.Add<Vignette>(true);
            vig.intensity.Override(0.32f);
            vig.smoothness.Override(0.5f);
            var ca = profile.Add<ColorAdjustments>(true);
            ca.postExposure.Override(0.3f);
            ca.contrast.Override(12f);

            foreach (var comp in profile.components)
            {
                comp.name = comp.GetType().Name;
                comp.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
                AssetDatabase.AddObjectToAsset(comp, profile);
            }
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            return profile;
        }

        static void AddToBuildSettings(string path)
        {
            var list = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (list.Any(s => s.path == path)) return;
            list.Add(new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = list.ToArray();
        }
    }
}
