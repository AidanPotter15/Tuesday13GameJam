using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace XIII
{
    // Builds one hotel corridor out of primitives. The corridor runs along +Z from the
    // door you came through (z = 0) to the next door (z = Length).
    public class Corridor : MonoBehaviour
    {
        public const float Length = 26f, HalfWidth = 1.5f, Height = 3f;
        public const float ForwardTrigger = 25.3f, BackTrigger = 0.75f;
        public static readonly Vector3 Spawn = new Vector3(0f, 0.05f, 2.0f);

        static readonly float[] DoorZ = { 5f, 10f, 15f, 20f };
        static readonly float[] LightZ = { 4f, 10f, 16f, 22f };

        static readonly Color CarpetA = new Color(0.36f, 0.06f, 0.07f);
        static readonly Color CarpetB = new Color(0.30f, 0.05f, 0.06f);
        static readonly Color CarpetStripe = new Color(0.55f, 0.42f, 0.18f);
        static readonly Color WallPaper = new Color(0.66f, 0.58f, 0.41f);
        static readonly Color Wood = new Color(0.28f, 0.16f, 0.09f);
        static readonly Color Trim = new Color(0.82f, 0.78f, 0.66f);
        static readonly Color CeilingCol = new Color(0.72f, 0.70f, 0.62f);
        static readonly Color DoorCol = new Color(0.40f, 0.23f, 0.12f);
        static readonly Color DoorPanel = new Color(0.33f, 0.19f, 0.10f);
        static readonly Color Brass = new Color(0.78f, 0.62f, 0.28f);
        static readonly Color Ink = new Color(0.08f, 0.06f, 0.04f);
        static readonly Color Black = new Color(0.015f, 0.015f, 0.02f);
        static readonly Color PanelOn = new Color(1f, 0.94f, 0.78f);
        static readonly Color LightCol = new Color(1f, 0.87f, 0.66f);

        int floor;
        PlayerController pc;
        Transform cam;

        readonly List<Transform> doorPivots = new List<Transform>();
        readonly List<int> doorSides = new List<int>();
        readonly List<TextMesh> plates = new List<TextMesh>();
        readonly List<Light> lights = new List<Light>();
        readonly List<Renderer> panels = new List<Renderer>();
        TextMesh exitSign, poster, plaqueNumber;
        Transform horseshoe;
        ClockHands clock;
        readonly List<EyeFollow> eyes = new List<EyeFollow>();
        GameObject umbrellaInStand;
        Renderer mirrorGlass;

        public static Corridor Build(int floor, PlayerController pc)
        {
            var go = new GameObject("Corridor (floor " + floor + ")");
            var c = go.AddComponent<Corridor>();
            c.floor = floor;
            c.pc = pc;
            c.cam = pc.Cam.transform;
            c.BuildShell();
            c.BuildDressing();
            return c;
        }

        // ---------- helpers ----------

        GameObject Prim(PrimitiveType type, string name, Transform parent, Vector3 pos, Vector3 scale, Material m, bool collider = false, Quaternion? rot = null)
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

        GameObject Box(string name, Vector3 pos, Vector3 scale, Material m, bool collider = false, Transform parent = null, Quaternion? rot = null)
        {
            return Prim(PrimitiveType.Cube, name, parent, pos, scale, m, collider, rot);
        }

        Transform Empty(string name, Vector3 pos, Quaternion rot, Transform parent = null)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent != null ? parent : transform, false);
            t.localPosition = pos;
            t.localRotation = rot;
            return t;
        }

        // Text on a surface. intoSurface points away from the reader (into the wall).
        TextMesh Label(string text, Vector3 pos, Vector3 intoSurface, float lineHeight, Color color, Transform parent = null)
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

        // ---------- shell ----------

        void BuildShell()
        {
            Material wall = Mats.Lit(WallPaper, 0.1f);
            Material wood = Mats.Lit(Wood, 0.35f);
            Material trim = Mats.Lit(Trim, 0.3f);
            Material ceil = Mats.Lit(CeilingCol, 0.05f);
            Material stripe = Mats.Lit(CarpetStripe, 0.1f);

            // 13 carpet strips, 13 ceiling strips.
            for (int i = 0; i < 13; i++)
            {
                float z = i * 2f + 1f;
                Box("Carpet", new Vector3(0f, -0.05f, z), new Vector3(3.4f, 0.1f, 2f), Mats.Lit(i % 2 == 0 ? CarpetA : CarpetB, 0.05f), true);
                Box("Stripe", new Vector3(-1.15f, 0.001f, z), new Vector3(0.05f, 0.004f, 2f), stripe);
                Box("Stripe", new Vector3(1.15f, 0.001f, z), new Vector3(0.05f, 0.004f, 2f), stripe);
                Box("Ceiling", new Vector3(0f, Height + 0.05f, z), new Vector3(3.4f, 0.1f, 2f), ceil, true);
            }

            // Side walls with room doors.
            int[] sides = { -1, 1 };
            foreach (int s in sides)
            {
                float a = 0f;
                for (int i = 0; i < DoorZ.Length; i++)
                {
                    float zc = DoorZ[i];
                    WallRun(s, a, zc - 0.55f, wall, wood, trim);
                    Box("Lintel", new Vector3(s * 1.6f, 2.6f, zc), new Vector3(0.2f, 0.8f, 1.1f), wall, true);
                    Box("Crown", new Vector3(s * 1.49f, 2.96f, zc), new Vector3(0.03f, 0.08f, 1.1f), trim);
                    int number = floor * 100 + i * 2 + (s < 0 ? 1 : 2);
                    RoomDoor(s, zc, number.ToString(), trim);
                    a = zc + 0.55f;
                }
                WallRun(s, a, Length, wall, wood, trim);
            }

            EndWall(0f, 1, wall, wood, trim);
            EndWall(Length, -1, wall, wood, trim);

            // Ceiling lights.
            bool urp = Mats.IsURP;
            foreach (float z in LightZ)
            {
                Box("LightFrame", new Vector3(0f, 2.99f, z), new Vector3(0.7f, 0.02f, 1.3f), trim);
                var panel = Box("LightPanel", new Vector3(0f, 2.975f, z), new Vector3(0.6f, 0.02f, 1.2f), Mats.Unlit(PanelOn));
                panels.Add(panel.GetComponent<Renderer>());
                var lgo = new GameObject("Light");
                lgo.transform.SetParent(transform, false);
                lgo.transform.localPosition = new Vector3(0f, 2.7f, z);
                var l = lgo.AddComponent<Light>();
                l.type = LightType.Point;
                l.range = 9f;
                l.intensity = urp ? 4.5f : 1.6f;
                l.color = LightCol;
                l.shadows = LightShadows.None;
                l.renderMode = LightRenderMode.ForcePixel;
                lights.Add(l);
            }
        }

        void WallRun(int s, float a, float b, Material wall, Material wood, Material trim)
        {
            float len = b - a, mid = (a + b) * 0.5f;
            if (len <= 0.01f) return;
            Box("Wall", new Vector3(s * 1.6f, 1.5f, mid), new Vector3(0.2f, 3f, len), wall, true);
            Box("Wainscot", new Vector3(s * 1.49f, 0.5f, mid), new Vector3(0.02f, 1f, len), wood);
            Box("Rail", new Vector3(s * 1.48f, 1.02f, mid), new Vector3(0.04f, 0.06f, len), trim);
            Box("Crown", new Vector3(s * 1.49f, 2.96f, mid), new Vector3(0.03f, 0.08f, len), trim);
        }

        void RoomDoor(int s, float zc, string number, Material trim)
        {
            Box("Jamb", new Vector3(s * 1.49f, 1.1f, zc - 0.58f), new Vector3(0.04f, 2.2f, 0.06f), trim);
            Box("Jamb", new Vector3(s * 1.49f, 1.1f, zc + 0.58f), new Vector3(0.04f, 2.2f, 0.06f), trim);
            Box("Head", new Vector3(s * 1.49f, 2.23f, zc), new Vector3(0.04f, 0.06f, 1.22f), trim);

            // Hinged at the zc - 0.5 edge so it can swing open into the room.
            Transform pivot = Empty("DoorPivot", new Vector3(s * 1.55f, 0f, zc - 0.5f), Quaternion.identity);
            Box("Door", new Vector3(0f, 1.1f, 0.5f), new Vector3(0.07f, 2.18f, 1f), Mats.Lit(DoorCol, 0.3f), true, pivot);
            Material panel = Mats.Lit(DoorPanel, 0.3f);
            Box("Panel", new Vector3(-s * 0.04f, 1.05f, 0.5f), new Vector3(0.01f, 0.75f, 0.7f), panel, false, pivot);
            Box("Panel", new Vector3(-s * 0.04f, 0.38f, 0.5f), new Vector3(0.01f, 0.45f, 0.7f), panel, false, pivot);
            Prim(PrimitiveType.Sphere, "Handle", pivot, new Vector3(-s * 0.06f, 1.0f, 0.88f), Vector3.one * 0.07f, Mats.Lit(Brass, 0.7f));
            Box("Plate", new Vector3(-s * 0.04f, 1.62f, 0.5f), new Vector3(0.012f, 0.13f, 0.3f), Mats.Lit(Brass, 0.7f), false, pivot);
            plates.Add(Label(number, new Vector3(-s * 0.047f, 1.62f, 0.5f), new Vector3(s, 0f, 0f), 0.09f, Ink, pivot));
            doorPivots.Add(pivot);
            doorSides.Add(s);
        }

        // An end wall with a door in the middle. n = direction from the wall into the corridor.
        void EndWall(float zFace, int n, Material wall, Material wood, Material trim)
        {
            float zBody = zFace - n * 0.1f;
            Box("EndWall", new Vector3(-1.175f, 1.5f, zBody), new Vector3(1.05f, 3f, 0.2f), wall, true);
            Box("EndWall", new Vector3(1.175f, 1.5f, zBody), new Vector3(1.05f, 3f, 0.2f), wall, true);
            Box("EndLintel", new Vector3(0f, 2.675f, zBody), new Vector3(1.3f, 0.65f, 0.2f), wall, true);
            float zw = zFace + n * 0.01f;
            Box("Wainscot", new Vector3(-1.075f, 0.5f, zw), new Vector3(0.85f, 1f, 0.02f), wood);
            Box("Wainscot", new Vector3(1.075f, 0.5f, zw), new Vector3(0.85f, 1f, 0.02f), wood);
            Box("Rail", new Vector3(-1.075f, 1.02f, zFace + n * 0.02f), new Vector3(0.85f, 0.06f, 0.04f), trim);
            Box("Rail", new Vector3(1.075f, 1.02f, zFace + n * 0.02f), new Vector3(0.85f, 0.06f, 0.04f), trim);
            Box("Frame", new Vector3(-0.68f, 1.2f, zw), new Vector3(0.06f, 2.4f, 0.04f), trim);
            Box("Frame", new Vector3(0.68f, 1.2f, zw), new Vector3(0.06f, 2.4f, 0.04f), trim);
            Box("Frame", new Vector3(0f, 2.38f, zw), new Vector3(1.42f, 0.06f, 0.04f), trim);
            Box("EndDoor", new Vector3(0f, 1.175f, zFace - n * 0.04f), new Vector3(1.3f, 2.35f, 0.08f), Mats.Lit(DoorCol, 0.3f), true);
            Box("PushBar", new Vector3(0f, 1.0f, zFace + n * 0.03f), new Vector3(0.9f, 0.05f, 0.04f), Mats.Lit(new Color(0.6f, 0.6f, 0.62f), 0.8f));
            Box("Window", new Vector3(0f, 1.75f, zFace + n * 0.005f), new Vector3(0.3f, 0.4f, 0.01f), Mats.Lit(Black, 0.9f));
        }

        // ---------- dressing (identical on every normal floor) ----------

        void BuildDressing()
        {
            Material wood = Mats.Lit(Wood, 0.35f);
            Material gold = Mats.Lit(new Color(0.62f, 0.48f, 0.2f), 0.6f);

            // Rules poster, left wall by the entrance.
            Box("PosterFrame", new Vector3(-1.485f, 1.55f, 2.6f), new Vector3(0.02f, 0.9f, 0.7f), wood);
            Box("Poster", new Vector3(-1.47f, 1.55f, 2.6f), new Vector3(0.01f, 0.82f, 0.62f), Mats.Lit(new Color(0.85f, 0.8f, 0.66f), 0.05f));
            poster = Label("GUEST NOTICE\n\nIf you see an omen,\nturn back.\n\nIf nothing is wrong,\nkeep walking.\n\nYour room is on\nthe 13th floor.",
                new Vector3(-1.462f, 1.55f, 2.6f), Vector3.left, 0.042f, Ink);

            // Floor plaque, right wall by the entrance.
            Box("Plaque", new Vector3(1.485f, 1.6f, 2.6f), new Vector3(0.02f, 0.5f, 0.6f), Mats.Lit(Brass, 0.7f));
            Label("FLOOR", new Vector3(1.472f, 1.76f, 2.6f), Vector3.right, 0.07f, Ink);
            plaqueNumber = Label(Omens.Roman(floor), new Vector3(1.472f, 1.54f, 2.6f), Vector3.right, 0.22f, Ink);

            // Console table with dead flowers, left wall.
            Box("TableTop", new Vector3(-1.3f, 0.8f, 7.5f), new Vector3(0.36f, 0.05f, 1f), wood);
            for (int i = 0; i < 4; i++)
                Box("Leg", new Vector3(i < 2 ? -1.44f : -1.16f, 0.39f, i % 2 == 0 ? 7.06f : 7.94f), new Vector3(0.04f, 0.78f, 0.04f), wood);
            Prim(PrimitiveType.Cylinder, "Vase", null, new Vector3(-1.3f, 0.97f, 7.5f), new Vector3(0.12f, 0.12f, 0.12f), Mats.Lit(new Color(0.1f, 0.3f, 0.32f), 0.8f));
            for (int i = 0; i < 3; i++)
                Box("Stem", new Vector3(-1.3f, 1.2f, 7.5f), new Vector3(0.01f, 0.3f, 0.01f), Mats.Lit(new Color(0.2f, 0.17f, 0.08f)), false, null, Quaternion.Euler(i * 12f - 12f, 0f, i * 9f - 9f));

            // Mirror, left wall.
            Box("MirrorFrame", new Vector3(-1.485f, 1.55f, 12.5f), new Vector3(0.03f, 1.25f, 0.85f), gold);
            mirrorGlass = Box("MirrorGlass", new Vector3(-1.468f, 1.55f, 12.5f), new Vector3(0.01f, 1.1f, 0.7f), Mats.Lit(new Color(0.45f, 0.5f, 0.52f), 0.95f)).GetComponent<Renderer>();

            BuildPortrait(gold);
            BuildClock(wood);

            // Umbrella stand, right wall.
            Prim(PrimitiveType.Cylinder, "Stand", null, new Vector3(1.28f, 0.3f, 17.5f), new Vector3(0.22f, 0.3f, 0.22f), Mats.Lit(new Color(0.35f, 0.3f, 0.2f), 0.6f));
            umbrellaInStand = new GameObject("ClosedUmbrella");
            umbrellaInStand.transform.SetParent(transform, false);
            Prim(PrimitiveType.Cylinder, "Shaft", umbrellaInStand.transform, new Vector3(1.28f, 0.75f, 17.5f), new Vector3(0.025f, 0.45f, 0.025f), Mats.Lit(Black));
            Prim(PrimitiveType.Cylinder, "Folded", umbrellaInStand.transform, new Vector3(1.28f, 0.78f, 17.5f), new Vector3(0.07f, 0.28f, 0.07f), Mats.Lit(new Color(0.25f, 0.03f, 0.04f)));
            Box("Handle", new Vector3(1.28f, 1.22f, 17.53f), new Vector3(0.03f, 0.03f, 0.1f), Mats.Lit(Wood, 0.5f), false, umbrellaInStand.transform);

            // Brass horseshoe on a dark plaque above the far door, hung the lucky way: opening up.
            Box("HorseshoePlaque", new Vector3(0f, 2.64f, Length - 0.015f), new Vector3(0.5f, 0.42f, 0.03f), Mats.Lit(new Color(0.12f, 0.07f, 0.04f), 0.4f));
            Prim(PrimitiveType.Sphere, "Nail", null, new Vector3(0f, 2.62f, Length - 0.04f), Vector3.one * 0.03f, Mats.Lit(new Color(0.3f, 0.3f, 0.3f), 0.7f));
            horseshoe = Empty("Horseshoe", new Vector3(0f, 2.68f, Length - 0.05f), Quaternion.identity);
            Material shoe = Mats.Lit(Brass, 0.75f);
            for (int k = 0; k <= 8; k++)
            {
                float a = Mathf.Lerp(-105f, 105f, k / 8f);
                float r = 0.16f, rad = a * Mathf.Deg2Rad;
                Box("Shoe", new Vector3(Mathf.Sin(rad) * r, -Mathf.Cos(rad) * r, 0f), new Vector3(0.085f, 0.05f, 0.03f), shoe, false, horseshoe, Quaternion.Euler(0f, 0f, a));
            }

            // Exit sign.
            Box("ExitSign", new Vector3(-1.0f, 2.55f, Length - 0.04f), new Vector3(0.5f, 0.2f, 0.06f), Mats.Lit(new Color(0.08f, 0.08f, 0.08f)));
            exitSign = Label("EXIT", new Vector3(-1.0f, 2.55f, Length - 0.075f), Vector3.forward, 0.13f, new Color(0.3f, 1f, 0.45f));
        }

        void BuildPortrait(Material gold)
        {
            const float z = 12.5f;
            Box("PortraitFrame", new Vector3(1.485f, 1.6f, z), new Vector3(0.03f, 0.95f, 0.75f), gold);
            Box("Canvas", new Vector3(1.468f, 1.6f, z), new Vector3(0.01f, 0.84f, 0.64f), Mats.Lit(new Color(0.12f, 0.16f, 0.12f)));
            Material skin = Mats.Lit(new Color(0.74f, 0.66f, 0.58f), 0.2f);
            Box("Suit", new Vector3(1.462f, 1.3f, z), new Vector3(0.01f, 0.24f, 0.5f), Mats.Lit(Black));
            Box("Neck", new Vector3(1.461f, 1.45f, z), new Vector3(0.01f, 0.1f, 0.1f), skin);
            Prim(PrimitiveType.Sphere, "Head", null, new Vector3(1.455f, 1.66f, z), new Vector3(0.03f, 0.34f, 0.26f), skin);
            Prim(PrimitiveType.Sphere, "Hair", null, new Vector3(1.452f, 1.77f, z), new Vector3(0.03f, 0.16f, 0.28f), Mats.Lit(new Color(0.06f, 0.04f, 0.03f)));
            Box("Mouth", new Vector3(1.438f, 1.56f, z), new Vector3(0.005f, 0.012f, 0.07f), Mats.Lit(new Color(0.35f, 0.08f, 0.08f)));
            foreach (float dz in new[] { -0.055f, 0.055f })
            {
                Transform pivot = Empty("Eye", new Vector3(1.44f, 1.68f, z + dz), Quaternion.LookRotation(Vector3.left));
                Prim(PrimitiveType.Sphere, "Eyeball", pivot, Vector3.zero, Vector3.one * 0.045f, Mats.Lit(new Color(0.9f, 0.87f, 0.8f), 0.6f));
                Prim(PrimitiveType.Sphere, "Pupil", pivot, new Vector3(0f, 0f, 0.018f), Vector3.one * 0.02f, Mats.Lit(Black, 0.8f));
                var eye = pivot.gameObject.AddComponent<EyeFollow>();
                eye.Target = cam;
                eyes.Add(eye);
            }
        }

        void BuildClock(Material wood)
        {
            // Local +Z of the clock points out of the wall, toward the viewer.
            Transform root = Empty("Clock", new Vector3(-1.47f, 2.1f, 17.5f), Quaternion.LookRotation(Vector3.right));
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
            clock = root.gameObject.AddComponent<ClockHands>();
            clock.Hour = Hand(root, "Hour", 0.1f, 0.02f, 0.036f, ink);
            clock.Minute = Hand(root, "Minute", 0.16f, 0.012f, 0.040f, ink);
            clock.Second = Hand(root, "Second", 0.17f, 0.005f, 0.044f, Mats.Lit(new Color(0.6f, 0.05f, 0.05f)));
        }

        Transform Hand(Transform root, string name, float length, float width, float z, Material m)
        {
            Transform pivot = Empty(name, new Vector3(0f, 0f, z), Quaternion.identity, root);
            Box(name + "Hand", new Vector3(0f, length * 0.5f - 0.02f, 0f), new Vector3(width, length, 0.004f), m, false, pivot);
            return pivot;
        }

        // ---------- omens ----------

        public void ApplyOmen(int omen)
        {
            switch (omen)
            {
                case Omens.Cat: AddCat(); break;
                case Omens.Ladder: AddLadder(); break;
                case Omens.Mirror: BreakMirror(); break;
                case Omens.Umbrella: OpenUmbrella(); break;
                case Omens.Horseshoe:
                    var fall = horseshoe.gameObject.AddComponent<HorseshoeFall>();
                    fall.Target = pc.transform;
                    fall.FloorPos = new Vector3(0.2f, 0.02f, 25.0f);
                    fall.FloorRot = Quaternion.Euler(90f, 25f, 0f);
                    break;
                case Omens.Room13: plates[Random.Range(0, plates.Count)].text = "13"; break;
                case Omens.Clock: clock.Reverse = true; break;
                case Omens.Portrait: foreach (var e in eyes) e.Follow = true; break;
                case Omens.RedLight: RedLight(2); break;
                case Omens.Guest: AddGuest(); break;
                case Omens.Footsteps: gameObject.AddComponent<FootstepsBehind>().Player = pc; break;
                case Omens.OpenDoor: OpenDoor(2); break; // left side, z = 15
                case Omens.ExitSign:
                    exitSign.text = "XIII";
                    exitSign.color = new Color(1f, 0.15f, 0.1f);
                    break;
            }
        }

        // Floor XIII: every omen at once, and no way back.
        public void ApplyFinale()
        {
            for (int o = 0; o < Omens.Count; o++) ApplyOmen(o);
            for (int i = 0; i < lights.Count; i++)
                if (i != 2) lights[i].intensity *= 0.35f;
            poster.text = "THERE IS NO\nTHIRTEENTH FLOOR.\n\n\nThere is no\nway back.\n\n\nWelcome home.";
            plaqueNumber.text = "XIII";
            plaqueNumber.color = new Color(0.45f, 0.02f, 0.02f);
        }

        void AddCat()
        {
            Material fur = Mats.Lit(Black, 0.4f);
            Transform cat = Empty("BlackCat", new Vector3(-1.2f, 0f, 11.5f), Quaternion.LookRotation(Vector3.right));
            Prim(PrimitiveType.Sphere, "Body", cat, new Vector3(0f, 0.2f, -0.04f), new Vector3(0.24f, 0.26f, 0.44f), fur);
            Prim(PrimitiveType.Sphere, "Chest", cat, new Vector3(0f, 0.28f, 0.12f), new Vector3(0.2f, 0.28f, 0.2f), fur);
            Prim(PrimitiveType.Sphere, "Head", cat, new Vector3(0f, 0.45f, 0.2f), new Vector3(0.19f, 0.17f, 0.17f), fur);
            Box("Ear", new Vector3(-0.055f, 0.55f, 0.2f), new Vector3(0.05f, 0.08f, 0.03f), fur, false, cat, Quaternion.Euler(0f, 0f, 20f));
            Box("Ear", new Vector3(0.055f, 0.55f, 0.2f), new Vector3(0.05f, 0.08f, 0.03f), fur, false, cat, Quaternion.Euler(0f, 0f, -20f));
            Material glow = Mats.Unlit(new Color(0.85f, 1f, 0.25f));
            Prim(PrimitiveType.Sphere, "Eye", cat, new Vector3(-0.045f, 0.47f, 0.285f), new Vector3(0.04f, 0.03f, 0.02f), glow);
            Prim(PrimitiveType.Sphere, "Eye", cat, new Vector3(0.045f, 0.47f, 0.285f), new Vector3(0.04f, 0.03f, 0.02f), glow);
            Prim(PrimitiveType.Capsule, "Tail", cat, new Vector3(0f, 0.3f, -0.32f), new Vector3(0.04f, 0.2f, 0.04f), fur, false, Quaternion.Euler(-35f, 0f, 0f));
            for (int i = 0; i < 4; i++)
                Box("Leg", new Vector3(i < 2 ? -0.06f : 0.06f, 0.06f, i % 2 == 0 ? 0.12f : -0.15f), new Vector3(0.04f, 0.12f, 0.04f), fur, false, cat);
            var walker = cat.gameObject.AddComponent<CatWalker>();
            walker.Target = pc.transform;
            walker.From = new Vector3(-1.2f, 0f, 11.5f);
            walker.To = new Vector3(0.9f, 0f, 11.5f);
        }

        void AddLadder()
        {
            // Leans across the hall onto the right wall. You'd have to walk under it.
            Vector3 foot = new Vector3(0.3f, 0f, 8f);
            Vector3 top = new Vector3(1.47f, 2.75f, 8f);
            Vector3 d = top - foot;
            float len = d.magnitude;
            float ang = Mathf.Atan2(d.x, d.y) * Mathf.Rad2Deg;
            Transform ladder = Empty("Ladder", foot, Quaternion.Euler(0f, 0f, -ang));
            Material metal = Mats.Lit(new Color(0.65f, 0.65f, 0.6f), 0.6f);
            Box("Rail", new Vector3(0f, len * 0.5f, -0.22f), new Vector3(0.05f, len, 0.05f), metal, false, ladder);
            Box("Rail", new Vector3(0f, len * 0.5f, 0.22f), new Vector3(0.05f, len, 0.05f), metal, false, ladder);
            for (float y = 0.3f; y < len - 0.1f; y += 0.3f)
                Box("Rung", new Vector3(0f, y, 0f), new Vector3(0.03f, 0.03f, 0.44f), metal, false, ladder);
        }

        void BreakMirror()
        {
            mirrorGlass.sharedMaterial = Mats.Lit(new Color(0.3f, 0.33f, 0.35f), 0.95f);
            Material crack = Mats.Lit(Black);
            Vector3 c = new Vector3(-1.462f, 1.68f, 12.58f);
            var rng = new System.Random(floor * 31 + 7);
            for (int k = 0; k < 9; k++)
            {
                float a = (k / 9f) * Mathf.PI * 2f + (float)rng.NextDouble() * 0.5f;
                float len = 0.12f + (float)rng.NextDouble() * 0.16f;
                Vector3 dir = new Vector3(0f, Mathf.Cos(a), Mathf.Sin(a));
                Box("Crack", c + dir * len * 0.5f, new Vector3(0.004f, len, 0.006f), crack, false, null, Quaternion.FromToRotation(Vector3.up, dir));
            }
            Material shard = Mats.Lit(new Color(0.6f, 0.65f, 0.68f), 0.95f);
            for (int k = 0; k < 7; k++)
            {
                Vector3 p = new Vector3(-1.25f + (float)rng.NextDouble() * 0.4f, 0.005f, 12.1f + (float)rng.NextDouble() * 0.8f);
                Box("Shard", p, new Vector3(0.06f, 0.006f, 0.03f), shard, false, null, Quaternion.Euler(0f, (float)rng.NextDouble() * 180f, 0f));
            }
        }

        void OpenUmbrella()
        {
            umbrellaInStand.SetActive(false);
            Transform u = Empty("OpenUmbrella", new Vector3(0.35f, 0f, 19f), Quaternion.Euler(10f, 30f, 22f));
            Material canopy = Mats.Lit(new Color(0.25f, 0.03f, 0.04f), 0.4f);
            Prim(PrimitiveType.Sphere, "Canopy", u, new Vector3(0f, 0.78f, 0f), new Vector3(1.1f, 0.42f, 1.1f), canopy);
            Prim(PrimitiveType.Cylinder, "Shaft", u, new Vector3(0f, 0.5f, 0f), new Vector3(0.025f, 0.5f, 0.025f), Mats.Lit(Black));
            Box("Handle", new Vector3(0f, 0.03f, 0.05f), new Vector3(0.03f, 0.03f, 0.12f), Mats.Lit(Wood, 0.5f), false, u);
        }

        void RedLight(int index)
        {
            Light l = lights[index];
            l.color = new Color(1f, 0.08f, 0.06f);
            l.intensity *= 1.3f;
            Material on = Mats.Unlit(new Color(1f, 0.12f, 0.1f));
            panels[index].sharedMaterial = on;
            var f = l.gameObject.AddComponent<Flicker>();
            f.Light = l;
            f.Panel = panels[index];
            f.OnMat = on;
            f.OffMat = Mats.Unlit(new Color(0.15f, 0.02f, 0.02f));
        }

        void AddGuest()
        {
            Material dark = Mats.Lit(new Color(0.01f, 0.01f, 0.012f), 0.1f);
            Transform g = Empty("Guest", new Vector3(0f, 0f, 24.3f), Quaternion.identity);
            Prim(PrimitiveType.Capsule, "Body", g, new Vector3(0f, 1.05f, 0f), new Vector3(0.55f, 1.05f, 0.35f), dark);
            Prim(PrimitiveType.Sphere, "Head", g, new Vector3(0f, 2.12f, 0f), new Vector3(0.28f, 0.34f, 0.28f), dark);
            Material eye = Mats.Unlit(new Color(0.95f, 0.95f, 0.9f));
            Prim(PrimitiveType.Sphere, "Eye", g, new Vector3(-0.06f, 2.16f, 0.13f), Vector3.one * 0.03f, eye);
            Prim(PrimitiveType.Sphere, "Eye", g, new Vector3(0.06f, 2.16f, 0.13f), Vector3.one * 0.03f, eye);
            var v = g.gameObject.AddComponent<FigureVanish>();
            v.Target = pc.transform;
            v.Distance = 8f;
        }

        void OpenDoor(int index)
        {
            Transform pivot = doorPivots[index];
            int s = doorSides[index];
            pivot.localRotation = Quaternion.Euler(0f, s < 0 ? -38f : 38f, 0f);
            var src = new GameObject("Whisper").AddComponent<AudioSource>();
            src.transform.SetParent(transform, false);
            src.transform.localPosition = new Vector3(s * 2.4f, 1.3f, pivot.localPosition.z + 0.5f);
            src.clip = Sfx.Whisper;
            src.loop = true;
            src.spatialBlend = 1f;
            src.minDistance = 1f;
            src.maxDistance = 11f;
            src.rolloffMode = AudioRolloffMode.Linear;
            var loop = src.gameObject.AddComponent<LoopVolume>();
            loop.Source = src;
            loop.BaseVolume = 0.8f;
            src.volume = 0.8f * Settings.Effects;
            src.Play();
        }
    }
}
