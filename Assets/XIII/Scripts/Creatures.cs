using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace XIII
{
    // Builds the jointed creature rigs baked into CreatureData (see tools/creatures.py).
    public static class Creatures
    {
        static readonly Dictionary<string, Mesh> meshes = new Dictionary<string, Mesh>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { meshes.Clear(); }

        static Mesh GetMesh(string key)
        {
            Mesh m;
            if (meshes.TryGetValue(key, out m) && m != null) return m;
            int[][] data = CreatureData.Meshes[key];
            int[] v = data[0];
            var verts = new Vector3[v.Length / 3];
            for (int i = 0; i < verts.Length; i++)
                verts[i] = new Vector3(v[i * 3], v[i * 3 + 1], v[i * 3 + 2]) * 0.0001f;
            m = new Mesh { name = key };
            m.vertices = verts;
            m.triangles = data[1];
            m.RecalculateNormals();
            m.RecalculateBounds();
            meshes[key] = m;
            return m;
        }

        // Instantiates a rig under `root` and returns its joints by name.
        public static Dictionary<string, Transform> Build(CreatureData.Part[] parts, Transform root, Material[] mats)
        {
            var map = new Dictionary<string, Transform>();
            var made = new Transform[parts.Length];
            for (int i = 0; i < parts.Length; i++)
            {
                CreatureData.Part p = parts[i];
                var go = new GameObject(p.Name);
                Transform t = go.transform;
                t.SetParent(p.Parent < 0 ? root : made[p.Parent], false);
                t.localPosition = p.Pivot;
                if (p.Mesh != null)
                {
                    go.AddComponent<MeshFilter>().sharedMesh = GetMesh(p.Mesh);
                    var r = go.AddComponent<MeshRenderer>();
                    r.sharedMaterial = mats[p.Mat];
                    r.shadowCastingMode = ShadowCastingMode.Off;
                    r.receiveShadows = false;
                }
                made[i] = t;
                map[p.Name] = t;
            }
            return map;
        }

        public static Material[] CatMaterials()
        {
            return new[]
            {
                Mats.Lit(new Color(0.025f, 0.024f, 0.028f), 0.45f), // fur
                Mats.Unlit(new Color(0.85f, 1f, 0.25f)),             // eyes
                Mats.Lit(new Color(0.28f, 0.13f, 0.13f), 0.5f),      // nose
                Mats.Unlit(Color.black),                             // pupils
            };
        }

        public static Material[] GuestMaterials()
        {
            return new[]
            {
                Mats.Lit(new Color(0.2f, 0.035f, 0.045f), 0.25f),    // coat and hat
                Mats.Lit(new Color(0.8f, 0.77f, 0.72f), 0.35f),      // skin
                Mats.Lit(new Color(0.86f, 0.84f, 0.8f), 0.2f),       // gloves and collar
                Mats.Lit(new Color(0.78f, 0.62f, 0.28f), 0.75f),     // brass
                Mats.Unlit(Color.black),                             // eye hollows
                Mats.Unlit(Color.white),                             // pinprick glints
            };
        }
    }
}
