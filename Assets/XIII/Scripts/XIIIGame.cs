using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace XIII
{
    // Entry point. Drop this on an empty GameObject in an empty scene (XIII > Setup Project does it for you).
    public class XIIIGame : MonoBehaviour
    {
        public const string Author = "AidanPotter15";

        [Header("Debug (editor testing)")]
        [Tooltip("Force every corridor (after the first) to contain this omen. -1 = off.")]
        public int DebugOmen = -1;
        [Tooltip("Floor to start on. Set to 12 to test the ending quickly.")]
        public int DebugStartFloor = 1;

        enum State { Title, Playing, Transition, Ended }

        State state = State.Title;
        PlayerController player;
        Corridor corridor;
        AudioSource hum, sfx;

        int floor = 1, omen = -1, lastOmen = -1, mistakes;
        bool firstCorridor = true;
        float runTime;
        readonly bool[] witnessed = new bool[Omens.Count];

        float fade, redFlash, grace, thudCooldown;
        float msgTimer, msgDuration;
        string msgTitle = "", msgSub = "";
        Color msgColor = Color.white;

        Texture2D vignette;
        GUIStyle big, mid, small;

        static readonly Color Red = new Color(0.85f, 0.12f, 0.1f);
        bool Finale { get { return floor >= 13; } }

        void Awake()
        {
            Sfx.Init();
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.16f, 0.145f, 0.11f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = new Color(0.04f, 0.033f, 0.024f);

            player = new GameObject("Player").AddComponent<PlayerController>();

            hum = gameObject.AddComponent<AudioSource>();
            hum.clip = Sfx.Hum;
            hum.loop = true;
            hum.volume = 0.22f;
            hum.Play();
            sfx = gameObject.AddComponent<AudioSource>();

            vignette = MakeVignette();
            NewRun();
        }

        void NewRun()
        {
            floor = Mathf.Clamp(DebugStartFloor, 1, 13);
            mistakes = 0;
            runTime = 0f;
            firstCorridor = true;
            lastOmen = -1;
            BuildCorridor();
            player.Teleport(Corridor.Spawn, 0f);
        }

        void BuildCorridor()
        {
            if (corridor != null) Destroy(corridor.gameObject);
            corridor = Corridor.Build(floor, player);
            if (Finale)
            {
                omen = -1;
                corridor.ApplyFinale();
            }
            else
            {
                omen = PickOmen();
                if (omen >= 0) corridor.ApplyOmen(omen);
            }
            firstCorridor = false;
            RenderSettings.fogDensity = Mathf.Lerp(0.035f, 0.075f, (floor - 1) / 12f);
            hum.pitch = Mathf.Lerp(1f, 0.75f, (floor - 1) / 12f);
        }

        int PickOmen()
        {
            if (firstCorridor) return -1; // the first corridor is always safe, so you can learn it
            if (DebugOmen >= 0 && DebugOmen < Omens.Count) return DebugOmen;
            if (Random.value < 0.4f) return -1;

            // Prefer omens the player hasn't met yet.
            var pool = new List<int>();
            for (int i = 0; i < Omens.Count; i++)
                if (!witnessed[i] && i != lastOmen) pool.Add(i);
            if (pool.Count == 0 || Random.value < 0.35f)
            {
                pool.Clear();
                for (int i = 0; i < Omens.Count; i++)
                    if (i != lastOmen) pool.Add(i);
            }
            int o = pool[Random.Range(0, pool.Count)];
            lastOmen = o;
            return o;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            bool locked = Cursor.lockState == CursorLockMode.Locked;
            if (!locked) Cursor.visible = true;

            player.InputEnabled = state == State.Playing && locked;
            if (player.InputEnabled) runTime += dt;

            msgTimer -= dt;
            redFlash = Mathf.MoveTowards(redFlash, 0f, dt * 0.7f);
            grace -= dt;
            thudCooldown -= dt;

            if (state == State.Playing && grace <= 0f)
            {
                float z = player.transform.position.z;
                if (z > Corridor.ForwardTrigger)
                {
                    StartCoroutine(Go(true));
                }
                else if (z < Corridor.BackTrigger)
                {
                    if (!Finale) StartCoroutine(Go(false));
                    else if (thudCooldown <= 0f)
                    {
                        sfx.PlayOneShot(Sfx.Thud, 1f);
                        Show("", "It will not open.", Red, 2.5f);
                        thudCooldown = 2.5f;
                    }
                }
            }
        }

        IEnumerator Go(bool forward)
        {
            state = State.Transition;
            sfx.PlayOneShot(Sfx.Door, 0.8f);
            yield return FadeTo(1f, 0.35f);

            if (Finale)
            {
                state = State.Ended;
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                sfx.PlayOneShot(Sfx.Chime, 0.8f);
                yield break;
            }

            bool hadOmen = omen >= 0;
            bool correct = forward != hadOmen;
            if (hadOmen) witnessed[omen] = true;

            if (correct)
            {
                floor++;
                if (Finale) Show("FLOOR XIII", "There is no thirteenth floor.", Red, 5f);
                else Show("FLOOR " + Omens.Roman(floor), hadOmen ? "You turned away from " + Omens.Names[omen] + "." : "", Color.white, 3.5f);
                sfx.PlayOneShot(Sfx.Chime, 0.6f);
            }
            else
            {
                mistakes++;
                floor = 1;
                Show("FLOOR I", hadOmen ? "You walked past " + Omens.Names[omen] + "." : "Nothing was wrong. You turned back anyway.", Red, 4.5f);
                sfx.PlayOneShot(Sfx.Stinger, 1f);
                redFlash = 1f;
            }

            BuildCorridor();
            player.Teleport(Corridor.Spawn, 0f);
            grace = 0.5f;
            yield return new WaitForSeconds(0.25f);
            yield return FadeTo(0f, 0.6f);
            state = State.Playing;
        }

        IEnumerator FadeTo(float target, float time)
        {
            float start = fade;
            for (float t = 0f; t < time; t += Time.deltaTime)
            {
                fade = Mathf.Lerp(start, target, t / time);
                yield return null;
            }
            fade = target;
        }

        void Show(string title, string sub, Color color, float duration)
        {
            msgTitle = title;
            msgSub = sub;
            msgColor = color;
            msgTimer = msgDuration = duration;
        }

        void Begin()
        {
            state = State.Playing;
            Show("FLOOR " + Omens.Roman(floor), "Your room is on the thirteenth floor.", Color.white, 4f);
        }

        void Lock()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        // ---------- UI (IMGUI: no Canvas, no TextMeshPro, works everywhere) ----------

        void EnsureStyles()
        {
            if (big == null)
            {
                big = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, wordWrap = true };
                mid = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, wordWrap = true };
                small = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.UpperCenter, wordWrap = true };
            }
            float u = Screen.height / 1080f;
            big.fontSize = Mathf.RoundToInt(72 * u);
            mid.fontSize = Mathf.RoundToInt(30 * u);
            small.fontSize = Mathf.RoundToInt(22 * u);
        }

        void OnGUI()
        {
            EnsureStyles();
            float u = Screen.height / 1080f;
            float W = Screen.width, H = Screen.height;
            var full = new Rect(0f, 0f, W, H);

            GUI.color = Color.white;
            GUI.DrawTexture(full, vignette);
            if (redFlash > 0f) { GUI.color = new Color(0.6f, 0f, 0f, redFlash * 0.55f); GUI.DrawTexture(full, Texture2D.whiteTexture); }
            if (fade > 0f) { GUI.color = new Color(0f, 0f, 0f, fade); GUI.DrawTexture(full, Texture2D.whiteTexture); }
            GUI.color = Color.white;

            if (msgTimer > 0f && state != State.Ended)
            {
                float a = Mathf.Clamp01(msgTimer) * Mathf.Clamp01((msgDuration - msgTimer) * 3f);
                Color c = msgColor; c.a = a;
                big.normal.textColor = c;
                mid.normal.textColor = new Color(0.9f, 0.88f, 0.82f, a);
                GUI.Label(new Rect(0f, H * 0.33f, W, 110f * u), msgTitle, big);
                GUI.Label(new Rect(W * 0.15f, H * 0.33f + 110f * u, W * 0.7f, 60f * u), msgSub, mid);
            }

            bool locked = Cursor.lockState == CursorLockMode.Locked;
            if (state == State.Title) DrawMenu(true, u);
            else if ((state == State.Playing || state == State.Transition) && !locked) DrawMenu(false, u);
            else if (state == State.Ended) DrawEnd(u);
        }

        void DrawMenu(bool title, float u)
        {
            float W = Screen.width, H = Screen.height;
            GUI.color = new Color(0f, 0f, 0f, title ? 0.88f : 0.75f);
            GUI.DrawTexture(new Rect(0f, 0f, W, H), Texture2D.whiteTexture);
            GUI.color = Color.white;

            float y = H * 0.12f;
            big.normal.textColor = Red;
            GUI.Label(new Rect(0f, y, W, 100f * u), title ? "X I I I" : "PAUSED", big);
            y += 95f * u;
            mid.normal.textColor = new Color(0.8f, 0.76f, 0.66f);
            GUI.Label(new Rect(0f, y, W, 40f * u), "there is no thirteenth floor", mid);
            y += 70f * u;

            small.normal.textColor = new Color(0.85f, 0.82f, 0.75f);
            string rules =
                "You are a guest on the first floor. Your room is on the thirteenth.\n\n" +
                "If you see an OMEN, anything at all that is not as it should be, turn back.\n" +
                "If nothing is wrong, keep walking forward.\n" +
                "One wrong choice and you start again on Floor I.";
            GUI.Label(new Rect(W * 0.2f, y, W * 0.6f, 170f * u), rules, small);
            y += 175f * u;

            mid.normal.textColor = Color.white;
            float pulse = 0.6f + 0.4f * Mathf.Sin(Time.unscaledTime * 3f);
            mid.normal.textColor = new Color(1f, 1f, 1f, pulse);
            GUI.Label(new Rect(0f, y, W, 40f * u), "[ click to " + (title ? "check in" : "continue") + " ]", mid);
            y += 55f * u;

            small.normal.textColor = new Color(0.6f, 0.58f, 0.52f);
            GUI.Label(new Rect(0f, y, W, 30f * u), "WASD move  ·  Mouse look  ·  Shift hurry  ·  Esc pause", small);
            y += 40f * u;

            // Mouse sensitivity slider.
            Rect sliderLabel = new Rect(W * 0.5f - 250f * u, y, 200f * u, 30f * u);
            Rect slider = new Rect(W * 0.5f - 40f * u, y + 8f * u, 290f * u, 20f * u);
            small.alignment = TextAnchor.UpperRight;
            GUI.Label(sliderLabel, "Mouse sensitivity ", small);
            small.alignment = TextAnchor.UpperCenter;
            player.Sensitivity = GUI.HorizontalSlider(slider, player.Sensitivity, 0.02f, 0.3f);
            y += 50f * u;

            int seen = 0;
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < Omens.Count; i++)
            {
                if (witnessed[i]) seen++;
                sb.Append(witnessed[i] ? Omens.Names[i] : "? ? ?");
                if (i < Omens.Count - 1) sb.Append(i % 5 == 4 ? "\n" : "   ·   ");
            }
            small.normal.textColor = new Color(0.75f, 0.35f, 0.3f);
            GUI.Label(new Rect(0f, y, W, 30f * u), "OMENS WITNESSED  " + seen + " / 13", small);
            y += 32f * u;
            small.normal.textColor = new Color(0.6f, 0.58f, 0.52f);
            GUI.Label(new Rect(W * 0.1f, y, W * 0.8f, 100f * u), sb.ToString(), small);

            GUI.Label(new Rect(0f, H - 45f * u, W, 30f * u), "Made in a day for the Tuesday the 13th Jam by " + Author + " with Unity 6 and Claude Code", small);

            // Click anywhere except the slider to (re)enter the game.
            Event e = Event.current;
            if (e.type == EventType.MouseDown && !slider.Contains(e.mousePosition))
            {
                Lock();
                if (title) Begin();
                e.Use();
            }
        }

        void DrawEnd(float u)
        {
            float W = Screen.width, H = Screen.height;
            GUI.color = Color.black;
            GUI.DrawTexture(new Rect(0f, 0f, W, H), Texture2D.whiteTexture);
            GUI.color = Color.white;

            float y = H * 0.14f;
            big.normal.textColor = Red;
            GUI.Label(new Rect(0f, y, W, 100f * u), "FLOOR XIII", big);
            y += 110f * u;
            mid.normal.textColor = new Color(0.85f, 0.82f, 0.75f);
            GUI.Label(new Rect(0f, y, W, 100f * u), "You found the floor that isn't there.\nCheck-out is never.", mid);
            y += 140f * u;

            int seen = 0;
            for (int i = 0; i < Omens.Count; i++) if (witnessed[i]) seen++;
            int mins = (int)(runTime / 60f), secs = (int)(runTime % 60f);
            small.normal.textColor = new Color(0.8f, 0.78f, 0.7f);
            GUI.Label(new Rect(0f, y, W, 100f * u),
                "Time  " + mins + ":" + secs.ToString("00") + "      Wrong turns  " + mistakes + "      Omens witnessed  " + seen + " / 13", small);
            y += 80f * u;

            small.normal.textColor = new Color(0.55f, 0.53f, 0.48f);
            GUI.Label(new Rect(W * 0.15f, y, W * 0.7f, 200f * u),
                "XIII was made in one day for the Tuesday the 13th Jam (13 October 2026).\n" +
                "Theme: \"Unlucky XIII\"  ·  Secondary theme: liminal horror\n\n" +
                "Design & direction: " + Author + "\n" +
                "Built with Unity 6. Code co-written with Claude Code (Anthropic).\n" +
                "All geometry and sound are generated procedurally in code. No asset packs.", small);
            y += 200f * u;

            mid.normal.textColor = new Color(1f, 1f, 1f, 0.6f + 0.4f * Mathf.Sin(Time.unscaledTime * 3f));
            GUI.Label(new Rect(0f, y, W, 40f * u), "[ click to check in again ]", mid);

            Event e = Event.current;
            if (e.type == EventType.MouseDown)
            {
                e.Use();
                NewRun();
                fade = 0f;
                Lock();
                Begin();
            }
        }

        static Texture2D MakeVignette()
        {
            const int N = 256;
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            var px = new Color32[N * N];
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float dx = (x + 0.5f) / N * 2f - 1f, dy = (y + 0.5f) / N * 2f - 1f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy) / 1.41421f;
                    float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.35f, 1f, d)) * 0.9f;
                    px[y * N + x] = new Color32(0, 0, 0, (byte)(a * 255f));
                }
            tex.SetPixels32(px);
            tex.Apply();
            return tex;
        }
    }
}
