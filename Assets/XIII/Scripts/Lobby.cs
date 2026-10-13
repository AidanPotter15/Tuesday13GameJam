using UnityEngine;

namespace XIII
{
    // The lobby of Hotel Tredecim, where every run starts. 10 m wide, 14 m deep: you enter at z = 0
    // and take the lift at the far wall (z = Depth) up to Floor I.
    public class Lobby : HotelBuilder
    {
        public const float HalfWidth = 5f, Depth = 14f, Height = 4f;
        public const float ExitZ = 13.2f, EntranceZ = 0.7f;
        public static readonly Vector3 Spawn = new Vector3(0f, 0.05f, 1.6f);
        public static readonly Vector3 Bell = new Vector3(-3.6f, 1.19f, 7f);

        static readonly Color MarbleLight = new Color(0.78f, 0.74f, 0.66f);
        static readonly Color MarbleDark = new Color(0.12f, 0.1f, 0.09f);
        static readonly Color Velvet = new Color(0.12f, 0.2f, 0.15f);
        static readonly Color Gold = new Color(0.86f, 0.68f, 0.32f);

        public static Lobby Build()
        {
            var go = new GameObject("Lobby");
            var l = go.AddComponent<Lobby>();
            l.BuildRoom();
            l.BuildDesk();
            l.BuildLift();
            l.BuildLounge();
            return l;
        }

        void BuildRoom()
        {
            Material wall = Mats.Lit(WallPaper, 0.1f);
            Material wood = Mats.Lit(Wood, 0.35f);
            Material trim = Mats.Lit(Trim, 0.3f);
            Material marbleL = Mats.Lit(MarbleLight, 0.6f);
            Material marbleD = Mats.Lit(MarbleDark, 0.6f);

            // One invisible collider for the floor, then a checkerboard of marble tiles on top.
            Box("FloorCollider", new Vector3(0f, -0.1f, Depth * 0.5f), new Vector3(HalfWidth * 2f + 0.4f, 0.2f, Depth + 0.4f), marbleD, true)
                .GetComponent<Renderer>().enabled = false;
            for (int x = -5; x < 5; x++)
                for (int z = 0; z < 14; z++)
                    Box("Tile", new Vector3(x + 0.5f, -0.05f, z + 0.5f), new Vector3(1f, 0.1f, 1f), (x + z) % 2 == 0 ? marbleL : marbleD);

            // Red runner from the entrance to the lift.
            Box("Runner", new Vector3(0f, 0.004f, 7f), new Vector3(2.4f, 0.008f, 11.6f), Mats.Lit(CarpetA, 0.05f));
            Material stripe = Mats.Lit(CarpetStripe, 0.1f);
            Box("RunnerEdge", new Vector3(-1.1f, 0.009f, 7f), new Vector3(0.05f, 0.004f, 11.6f), stripe);
            Box("RunnerEdge", new Vector3(1.1f, 0.009f, 7f), new Vector3(0.05f, 0.004f, 11.6f), stripe);

            Box("Ceiling", new Vector3(0f, Height + 0.05f, Depth * 0.5f), new Vector3(HalfWidth * 2f + 0.4f, 0.1f, Depth + 0.4f), Mats.Lit(CeilingCol, 0.05f));
            foreach (float z in new[] { 3.5f, 7f, 10.5f })
                Box("Beam", new Vector3(0f, Height - 0.08f, z), new Vector3(HalfWidth * 2f, 0.16f, 0.3f), wood);

            // Side walls.
            foreach (int s in new[] { -1, 1 })
            {
                Box("Wall", new Vector3(s * (HalfWidth + 0.1f), Height * 0.5f, Depth * 0.5f), new Vector3(0.2f, Height, Depth + 0.4f), wall, true);
                Box("Wainscot", new Vector3(s * (HalfWidth - 0.01f), 0.6f, Depth * 0.5f), new Vector3(0.02f, 1.2f, Depth), wood);
                Box("Rail", new Vector3(s * (HalfWidth - 0.02f), 1.22f, Depth * 0.5f), new Vector3(0.04f, 0.06f, Depth), trim);
                Box("Crown", new Vector3(s * (HalfWidth - 0.015f), Height - 0.05f, Depth * 0.5f), new Vector3(0.03f, 0.1f, Depth), trim);
            }

            // End walls.
            foreach (float z in new[] { -0.1f, Depth + 0.1f })
                Box("EndWall", new Vector3(0f, Height * 0.5f, z), new Vector3(HalfWidth * 2f + 0.4f, Height, 0.2f), wall, true);
            Box("Wainscot", new Vector3(0f, 0.6f, 0.01f), new Vector3(HalfWidth * 2f, 1.2f, 0.02f), wood);

            // Entrance: locked glass doors onto a black night.
            Material glass = Mats.Lit(Black, 0.95f);
            Material brass = Mats.Lit(Brass, 0.75f);
            Box("EntranceFrame", new Vector3(0f, 1.4f, 0.025f), new Vector3(2.8f, 2.8f, 0.03f), wood);
            foreach (float x in new[] { -0.67f, 0.67f })
            {
                Box("Glass", new Vector3(x, 1.35f, 0.045f), new Vector3(1.2f, 2.5f, 0.02f), glass);
                Box("PushBar", new Vector3(x, 1.05f, 0.07f), new Vector3(0.9f, 0.05f, 0.04f), brass);
            }

            // Pillars.
            Material marblePillar = Mats.Lit(new Color(0.8f, 0.77f, 0.7f), 0.5f);
            foreach (float x in new[] { -3f, 3f })
                foreach (float z in new[] { 4f, 10f })
                {
                    Prim(PrimitiveType.Cylinder, "Pillar", null, new Vector3(x, Height * 0.5f, z), new Vector3(0.5f, Height * 0.5f, 0.5f), marblePillar, true);
                    Box("PillarBase", new Vector3(x, 0.1f, z), new Vector3(0.7f, 0.2f, 0.7f), trim);
                    Box("PillarCap", new Vector3(x, Height - 0.25f, z), new Vector3(0.7f, 0.2f, 0.7f), trim);
                }

            // Chandeliers and lights.
            foreach (float z in new[] { 4f, 10f })
                Chandelier(new Vector3(0f, 3.25f, z), brass);
            PointLight(new Vector3(0f, 3.1f, 4f), 11f, 1.8f, 6f, LightCol);
            PointLight(new Vector3(0f, 3.1f, 10f), 11f, 1.8f, 6f, LightCol);
            PointLight(new Vector3(-3.2f, 2.8f, 7f), 7f, 1.2f, 3.5f, new Color(1f, 0.8f, 0.55f));
            Dust(new Vector3(0f, 2.2f, 7f), new Vector3(8f, 3.2f, 12f), 160, 14f);
        }

        void Chandelier(Vector3 c, Material brass)
        {
            Box("Chain", c + new Vector3(0f, 0.4f, 0f), new Vector3(0.02f, 0.8f, 0.02f), brass);
            Glow(c + Vector3.up * 0.05f, 2.4f, new Color(1f, 0.85f, 0.6f, 0.22f));
            Prim(PrimitiveType.Sphere, "Hub", null, c, Vector3.one * 0.18f, brass);
            Material bulb = Mats.Unlit(new Color(1f, 0.9f, 0.7f));
            for (int i = 0; i < 6; i++)
            {
                float a = i * Mathf.PI / 3f;
                Vector3 tip = c + new Vector3(Mathf.Cos(a) * 0.45f, 0.05f, Mathf.Sin(a) * 0.45f);
                Box("Arm", (c + tip) * 0.5f, new Vector3(0.02f, 0.02f, 0.45f), brass, false, null, Quaternion.LookRotation(tip - c));
                Prim(PrimitiveType.Sphere, "Bulb", null, tip + Vector3.up * 0.06f, new Vector3(0.07f, 0.1f, 0.07f), bulb);
            }
        }

        void BuildDesk()
        {
            Material wood = Mats.Lit(Wood, 0.35f);
            Material brass = Mats.Lit(Brass, 0.75f);
            Material paper = Mats.Lit(new Color(0.85f, 0.8f, 0.66f), 0.05f);

            // Reception desk along the left wall.
            Box("Desk", new Vector3(-4f, 0.55f, 7f), new Vector3(1.2f, 1.1f, 5f), wood, true);
            Box("DeskTop", new Vector3(-4f, 1.125f, 7f), new Vector3(1.3f, 0.05f, 5.1f), Mats.Lit(MarbleDark, 0.7f));
            Material panel = Mats.Lit(DoorPanel, 0.3f);
            for (float z = 5f; z <= 9f; z += 1f)
                Box("DeskPanel", new Vector3(-3.39f, 0.55f, z), new Vector3(0.02f, 0.7f, 0.8f), panel);

            // Service bell, a placard, and the guest book.
            Prim(PrimitiveType.Cylinder, "BellBase", null, new Vector3(Bell.x, 1.157f, Bell.z), new Vector3(0.13f, 0.007f, 0.13f), Mats.Lit(Wood, 0.5f));
            Prim(PrimitiveType.Sphere, "Bell", null, new Vector3(Bell.x, 1.17f, Bell.z), new Vector3(0.1f, 0.08f, 0.1f), brass);
            Prim(PrimitiveType.Sphere, "BellButton", null, new Vector3(Bell.x, 1.215f, Bell.z), Vector3.one * 0.015f, brass);
            Box("Placard", new Vector3(-3.62f, 1.21f, 6.55f), new Vector3(0.015f, 0.12f, 0.24f), paper);
            Label("PLEASE RING", new Vector3(-3.61f, 1.21f, 6.55f), Vector3.left, 0.035f, Ink);
            Box("GuestBook", new Vector3(-3.75f, 1.16f, 7.85f), new Vector3(0.26f, 0.02f, 0.38f), paper);
            Box("Spine", new Vector3(-3.75f, 1.165f, 7.85f), new Vector3(0.27f, 0.012f, 0.012f), Mats.Lit(CarpetA));

            // Key rack: thirteen hooks. Twelve keys. Yours is not there.
            Box("KeyRack", new Vector3(-4.985f, 2.05f, 7f), new Vector3(0.03f, 0.95f, 4.3f), Mats.Lit(new Color(0.14f, 0.08f, 0.05f), 0.4f));
            Material tag = Mats.Lit(new Color(0.5f, 0.06f, 0.05f), 0.2f);
            for (int i = 0; i < 13; i++)
            {
                float z = 7f - 1.92f + i * 0.32f;
                Label((i + 1).ToString(), new Vector3(-4.965f, 2.38f, z), Vector3.left, 0.07f, Gold);
                Box("Hook", new Vector3(-4.95f, 2.2f, z), new Vector3(0.05f, 0.02f, 0.02f), brass);
                if (i == 12) continue;
                Box("Key", new Vector3(-4.955f, 2.06f, z), new Vector3(0.012f, 0.15f, 0.035f), brass);
                Box("KeyTag", new Vector3(-4.955f, 1.91f, z), new Vector3(0.012f, 0.1f, 0.06f), tag);
            }
        }

        void BuildLift()
        {
            Material brass = Mats.Lit(Brass, 0.8f);
            float z = Depth - 0.015f;
            Box("LiftSurround", new Vector3(0f, 1.35f, z), new Vector3(1.9f, 2.7f, 0.03f), Mats.Lit(Wood, 0.4f));
            Box("LiftDoor", new Vector3(-0.36f, 1.22f, z - 0.03f), new Vector3(0.71f, 2.4f, 0.03f), brass);
            Box("LiftDoor", new Vector3(0.36f, 1.22f, z - 0.03f), new Vector3(0.71f, 2.4f, 0.03f), brass);
            Box("LiftSeam", new Vector3(0f, 1.22f, z - 0.05f), new Vector3(0.015f, 2.4f, 0.01f), Mats.Lit(Black));

            // The floor indicator skips XIII, like every hotel.
            Box("Indicator", new Vector3(0f, 2.9f, z - 0.01f), new Vector3(1.85f, 0.28f, 0.03f), Mats.Lit(Black, 0.6f));
            Label("I  II  III  IV  V  VI  VII  VIII  IX  X  XI  XII  XIV", new Vector3(0f, 2.9f, z - 0.03f), Vector3.forward, 0.06f, new Color(1f, 0.65f, 0.25f));

            Box("CallPanel", new Vector3(1.25f, 1.25f, z - 0.01f), new Vector3(0.14f, 0.3f, 0.02f), brass);
            Prim(PrimitiveType.Sphere, "CallButton", null, new Vector3(1.25f, 1.25f, z - 0.025f), Vector3.one * 0.05f, Mats.Unlit(new Color(1f, 0.6f, 0.2f)));
            Glow(new Vector3(1.25f, 1.25f, z - 0.06f), 0.2f, new Color(1f, 0.6f, 0.2f, 0.45f));

            Label("HOTEL TREDECIM", new Vector3(0f, 3.45f, z - 0.01f), Vector3.forward, 0.3f, Gold);
            Label("EST. MCMXIII", new Vector3(0f, 3.15f, z - 0.01f), Vector3.forward, 0.08f, new Color(0.7f, 0.55f, 0.28f));

            // Potted palms either side of the lift.
            foreach (float x in new[] { -4.3f, 4.3f })
            {
                Prim(PrimitiveType.Cylinder, "Pot", null, new Vector3(x, 0.3f, 12.9f), new Vector3(0.5f, 0.3f, 0.5f), Mats.Lit(new Color(0.35f, 0.16f, 0.1f), 0.3f), true);
                Prim(PrimitiveType.Sphere, "Palm", null, new Vector3(x, 1.05f, 12.9f), new Vector3(0.95f, 1.05f, 0.95f), Mats.Lit(new Color(0.07f, 0.16f, 0.07f), 0.2f));
            }
        }

        void BuildLounge()
        {
            Material velvet = Mats.Lit(Velvet, 0.15f);
            Material wood = Mats.Lit(Wood, 0.4f);

            // Two armchairs and a lamp table, facing into the room.
            foreach (float z in new[] { 2.4f, 5.6f })
            {
                Box("Seat", new Vector3(4.1f, 0.25f, z), new Vector3(0.8f, 0.5f, 0.8f), velvet, true);
                Box("Back", new Vector3(4.45f, 0.75f, z), new Vector3(0.12f, 0.95f, 0.8f), velvet);
                Box("Arm", new Vector3(4.1f, 0.5f, z - 0.36f), new Vector3(0.8f, 0.35f, 0.1f), velvet);
                Box("Arm", new Vector3(4.1f, 0.5f, z + 0.36f), new Vector3(0.8f, 0.35f, 0.1f), velvet);
            }
            Prim(PrimitiveType.Cylinder, "Table", null, new Vector3(4.1f, 0.3f, 4f), new Vector3(0.5f, 0.3f, 0.5f), wood, true);
            Prim(PrimitiveType.Cylinder, "LampStem", null, new Vector3(4.1f, 0.8f, 4f), new Vector3(0.03f, 0.2f, 0.03f), Mats.Lit(Brass, 0.7f));
            Prim(PrimitiveType.Cylinder, "LampShade", null, new Vector3(4.1f, 1.05f, 4f), new Vector3(0.32f, 0.1f, 0.32f), Mats.Unlit(new Color(1f, 0.82f, 0.55f)));
            Glow(new Vector3(4.1f, 1.05f, 4f), 0.9f, new Color(1f, 0.8f, 0.55f, 0.25f));

            // Grandfather clock on the right wall, ticking.
            Box("ClockCase", new Vector3(4.72f, 1.1f, 8.5f), new Vector3(0.5f, 2.2f, 0.6f), wood, true);
            Box("ClockHood", new Vector3(4.72f, 2.3f, 8.5f), new Vector3(0.56f, 0.2f, 0.66f), wood);
            BuildClock(new Vector3(4.46f, 1.85f, 8.5f), Vector3.left, wood);
            Box("PendulumWindow", new Vector3(4.465f, 0.9f, 8.5f), new Vector3(0.01f, 0.85f, 0.32f), Mats.Lit(Black, 0.9f));
            Transform pivot = Empty("Pendulum", new Vector3(4.455f, 1.3f, 8.5f), Quaternion.LookRotation(Vector3.left));
            Material brass = Mats.Lit(Brass, 0.8f);
            Box("Rod", new Vector3(0f, -0.25f, 0f), new Vector3(0.012f, 0.5f, 0.006f), brass, false, pivot);
            Prim(PrimitiveType.Cylinder, "Bob", pivot, new Vector3(0f, -0.52f, 0f), new Vector3(0.12f, 0.004f, 0.12f), brass, false, Quaternion.Euler(90f, 0f, 0f));
            pivot.gameObject.AddComponent<Swing>();

            // Welcome notice by the entrance.
            Box("NoticeFrame", new Vector3(4.985f, 1.75f, 1.9f), new Vector3(0.02f, 1.0f, 1.1f), Mats.Lit(Gold, 0.6f));
            Box("Notice", new Vector3(4.97f, 1.75f, 1.9f), new Vector3(0.01f, 0.9f, 1.0f), Mats.Lit(new Color(0.85f, 0.8f, 0.66f), 0.05f));
            Label("WELCOME, GUEST\n\nYour room is ready.\nIt is on the thirteenth floor.\n\nPlease take the lift.",
                new Vector3(4.962f, 1.75f, 1.9f), Vector3.right, 0.065f, Ink);
        }
    }
}
