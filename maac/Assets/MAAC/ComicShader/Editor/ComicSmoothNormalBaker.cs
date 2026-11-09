using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace MAAC.ComicShader.Editor
{
    /// <summary>
    /// Inverted-hull outlines break apart at hard edges (e.g. cube corners) because the hull is
    /// extruded along split vertex normals. This tool averages the normals of all vertices that
    /// share a position and stores the result in UV3 (TEXCOORD3, object space).
    /// Set "Outline Normal Source" = "Smoothed Normal UV3" on the material to use it.
    /// The original mesh asset is never modified: a copy is written to GeneratedMeshes/.
    /// Limitation: the UV3 normal is object space and is not skinned (SkinnedMeshRenderer).
    /// </summary>
    public static class ComicSmoothNormalBaker
    {
        const string OutputFolder = "Assets/MAAC/ComicShader/GeneratedMeshes";

        [MenuItem("Tools/Comic Shader/Bake Smoothed Outline Normals (Selection)")]
        static void BakeSelection()
        {
            int count = 0;
            foreach (var go in Selection.gameObjects)
            {
                foreach (var mf in go.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (mf.sharedMesh == null) continue;
                    Undo.RecordObject(mf, "Bake Smoothed Outline Normals");
                    mf.sharedMesh = CreateBakedCopy(mf.sharedMesh);
                    count++;
                }
            }
            Debug.Log($"[Comic] Baked smoothed outline normals for {count} mesh(es). Set 'Outline Normal Source' to 'Smoothed Normal UV3' on their materials.");
        }

        [MenuItem("Tools/Comic Shader/Bake Smoothed Outline Normals (Selection)", true)]
        static bool BakeSelectionValidate() => Selection.gameObjects.Length > 0;

        public static Mesh CreateBakedCopy(Mesh source)
        {
            if (!AssetDatabase.IsValidFolder(OutputFolder))
                AssetDatabase.CreateFolder("Assets/MAAC/ComicShader", "GeneratedMeshes");

            var copy = Object.Instantiate(source);
            copy.name = source.name + "_ComicOutline";
            WriteSmoothedNormalsToUV3(copy);
            string path = AssetDatabase.GenerateUniqueAssetPath($"{OutputFolder}/{copy.name}.asset");
            AssetDatabase.CreateAsset(copy, path);
            AssetDatabase.SaveAssets();
            return copy;
        }

        public static void WriteSmoothedNormalsToUV3(Mesh mesh)
        {
            Vector3[] verts = mesh.vertices;
            Vector3[] normals = mesh.normals;
            if (normals == null || normals.Length != verts.Length)
            {
                mesh.RecalculateNormals();
                normals = mesh.normals;
            }

            var accum = new Dictionary<Vector3Int, Vector3>(verts.Length);
            const float quant = 10000f;
            Vector3Int Key(Vector3 v) => new Vector3Int(Mathf.RoundToInt(v.x * quant), Mathf.RoundToInt(v.y * quant), Mathf.RoundToInt(v.z * quant));

            for (int i = 0; i < verts.Length; i++)
            {
                var k = Key(verts[i]);
                accum.TryGetValue(k, out var n);
                accum[k] = n + normals[i];
            }

            var smoothed = new List<Vector3>(verts.Length);
            for (int i = 0; i < verts.Length; i++)
            {
                Vector3 n = accum[Key(verts[i])];
                smoothed.Add(n.sqrMagnitude > 1e-8f ? n.normalized : normals[i]);
            }
            mesh.SetUVs(3, smoothed);
        }
    }
}
