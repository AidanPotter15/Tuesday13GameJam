using UnityEngine;
using UnityEngine.Rendering;

namespace XIII
{
    // Shared palette and primitive-building helpers for the hotel spaces (lobby and corridor).
    public abstract class HotelBuilder : MonoBehaviour
    {
        protected static readonly Color CarpetA = new Color(0.36f, 0.06f, 0.07f);
        protected static readonly Color CarpetB = new Color(0.30f, 0.05f, 0.06f);
        protected static readonly Color CarpetStripe = new Color(0.55f, 0.42f, 0.18f);
        protected static readonly Color WallPaper = new Color(0.66f, 0.58f, 0.41f);
        protected static readonly Color Wood = new Color(0.28f, 0.16f, 0.09f);
        protected static readonly Color Trim = new Color(0.82f, 0.78f, 0.66f);
        protected static readonly Color CeilingCol = new Color(0.72f, 0.70f, 0.62f);
        protected static readonly Color DoorCol = new Color(0.40f, 0.23f, 0.12f);
        protected static readonly Color DoorPanel = new Color(0.33f, 0.19f, 0.10f);
        protected static readonly Color Brass = new Color(0.78f, 0.62f, 0.28f);
        protected static readonly Color Ink = new Color(0.08f, 0.06f, 0.04f);
        protected static readonly Color Black = new Color(0.015f, 0.015f, 0.02f);
        protected static readonly Color PanelOn = new Color(1f, 0.94f, 0.78f);
        protected static readonly Color LightCol = new Color(1f, 0.87f, 0.66f);


        protected GameObject Prim(PrimitiveType type, string name, Transform parent, Vector3 pos, Vector3 scale, Material m, bool collider = false, Quaternion? rot = null)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent != null ? parent : transform, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = rot ?? Quaternion.identity;
            go.transform.localScale = scale;
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = m;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            if (!collider) DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        protected GameObject Box(string name, Vector3 pos, Vector3 scale, Material m, bool collider = false, Transform parent = null, Quaternion? rot = null)
        {
            return Prim(PrimitiveType.Cube, name, parent, pos, scale, m, collider, rot);
        }

        protected Transform Empty(string name, Vector3 pos, Quaternion rot, Transform parent = null)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent != null ? parent : transform, false);
            t.localPosition = pos;
            t.localRotation = rot;
            return t;
        }

        // Text on a surface. intoSurface points away from the reader (into the wall).
        protected TextMesh Label(string text, Vector3 pos, Vector3 intoSurface, float lineHeight, Color color, Transform parent = null)
        {
            var go = new GameObject("Text");
            go.transform.SetParent(parent != null ? parent : transform, false);
            go.transform.localPosition = pos;
            go.transform.rotation = Quaternion.LookRotation(intoSurface);
            var tm = go.AddComponent<TextMesh>();
            tm.font = Mats.Font;
            tm.fontSize = 64;
            // TextMesh world height is roughly fontSize * characterSize / 10.
            tm.characterSize = lineHeight * 10f / tm.fontSize;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = color;
            tm.text = text;
            go.GetComponent<MeshRenderer>().sharedMaterial = Mats.Text;
            return tm;
        }

        protected Light PointLight(Vector3 pos, float range, float intensityBuiltin, float intensityUrp, Color color)
        {
            var go = new GameObject("Light");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = pos;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.range = range;
            l.intensity = Mats.IsURP ? intensityUrp : intensityBuiltin;
            l.color = color;
            l.shadows = LightShadows.None;
            l.renderMode = LightRenderMode.ForcePixel;
            return l;
        }

        // A soft camera-facing halo, to fake bloom around a light. Null if the glow shader is missing.
        protected Renderer Glow(Vector3 pos, float size, Color color, Transform parent = null)
        {
            Material m = Mats.Glow(color);
            if (m == null) return null;
            var go = new GameObject("Glow");
            go.transform.SetParent(parent != null ? parent : transform, false);
            go.transform.localPosition = pos;
            go.transform.localScale = Vector3.one * size;
            go.AddComponent<MeshFilter>().sharedMesh = Mats.GlowQuad;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = m;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            go.AddComponent<Billboard>();
            return r;
        }

        // Dust drifting slowly through a box of air (usually under a light).
        protected ParticleSystemRenderer Dust(Vector3 center, Vector3 size, int max, float rate)
        {
            Material m = Mats.Glow(new Color(1f, 0.92f, 0.75f, 1f));
            if (m == null) return null;
            var go = new GameObject("Dust");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = center;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.loop = true;
            main.prewarm = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(8f, 14f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.005f, 0.03f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.01f, 0.022f);
            main.startColor = new Color(1f, 0.92f, 0.75f, 0.55f);
            main.maxParticles = max;
            main.gravityModifier = -0.0015f;

            var emission = ps.emission;
            emission.rateOverTime = rate;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = size;
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.04f;
            noise.frequency = 0.4f;
            var fade = ps.colorOverLifetime;
            fade.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.25f), new GradientAlphaKey(1f, 0.75f), new GradientAlphaKey(0f, 1f) });
            fade.color = g;

            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = m;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            ps.Play();
            return r;
        }

        // A wall clock. outward points from the wall toward the viewer; the clock's local +Z faces that way.
        protected ClockHands BuildClock(Vector3 pos, Vector3 outward, Material wood)
        {
            Transform root = Empty("Clock", pos, Quaternion.LookRotation(outward));
            Quaternion faceOut = Quaternion.Euler(90f, 0f, 0f);
            Prim(PrimitiveType.Cylinder, "Rim", root, Vector3.zero, new Vector3(0.5f, 0.02f, 0.5f), wood, false, faceOut);
            Prim(PrimitiveType.Cylinder, "Face", root, new Vector3(0f, 0f, 0.02f), new Vector3(0.44f, 0.01f, 0.44f), Mats.Lit(new Color(0.9f, 0.86f, 0.74f)), false, faceOut);
            Material ink = Mats.Lit(Ink);
            for (int i = 0; i < 12; i++)
            {
                Quaternion r = Quaternion.Euler(0f, 0f, i * 30f);
                bool major = i % 3 == 0;
                Box("Mark", r * Vector3.up * 0.18f + new Vector3(0f, 0f, 0.032f), new Vector3(major ? 0.02f : 0.012f, major ? 0.05f : 0.03f, 0.004f), ink, false, root, r);
            }
            var clock = root.gameObject.AddComponent<ClockHands>();
            clock.Hour = Hand(root, "Hour", 0.1f, 0.02f, 0.036f, ink);
            clock.Minute = Hand(root, "Minute", 0.16f, 0.012f, 0.040f, ink);
            clock.Second = Hand(root, "Second", 0.17f, 0.005f, 0.044f, Mats.Lit(new Color(0.6f, 0.05f, 0.05f)));
            return clock;
        }

        protected Transform Hand(Transform root, string name, float length, float width, float z, Material m)
        {
            Transform pivot = Empty(name, new Vector3(0f, 0f, z), Quaternion.identity, root);
            Box(name + "Hand", new Vector3(0f, length * 0.5f - 0.02f, 0f), new Vector3(width, length, 0.004f), m, false, pivot);
            return pivot;
        }
    }
}
