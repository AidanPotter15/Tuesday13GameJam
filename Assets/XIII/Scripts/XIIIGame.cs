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
        [Tooltip("Floor to start on. Set to 12 to test the ending quickly. Anything above 1 skips the lobby.")]
        public int DebugStartFloor = 1;
        [Tooltip("Start each run in the corridor instead of the lobby.")]
        public bool DebugSkipLobby;
        [Tooltip("Floor where the jump scare happens. 0 = random floor from IV to IX, once per run.")]
        public int DebugScareFloor;

        enum State { Title, Playing, Transition, Ended }

        State state = State.Title;
        PlayerController player;
        Corridor corridor;
        Lobby lobby;
        AudioSource hum, sfx, ui;

        int floor = 1, omen = -1, lastOmen = -1, mistakes;
        bool firstCorridor = true, paused, inLobby, nearBell, scareArmed, scareDone;
        int scareFloor, bellRings;
        float runTime;

        float fade, redFlash, grace, thudCooldown, nextPreview;
        float msgTimer, msgDuration;
        string msgTitle = "", msgSub = "";
        Color msgColor = Color.white;

        Texture2D vignette, texPanel, texButton, texHover;
        GUIStyle big, mid, small, label, labelRight, heading, button;

        static readonly Color Red = new Color(0.85f, 0.12f, 0.1f);
        static readonly Color Brass = new Color(0.86f, 0.7f, 0.4f);
        static readonly Color Paper = new Color(0.88f, 0.84f, 0.76f);
        static readonly Color Dim = new Color(0.55f, 0.52f, 0.46f);

        const float HumVolume = 0.22f;
        static readonly Color Ambient = new Color(0.16f, 0.145f, 0.11f);
        bool Finale { get { return floor >= 13; } }

        void Awake()
        {
            Settings.Load();
            Sfx.Init();
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = Ambient;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = new Color(0.04f, 0.033f, 0.024f);

            player = new GameObject("Player").AddComponent<PlayerController>();

            hum = gameObject.AddComponent<AudioSource>();
            hum.clip = Sfx.Hum;
            hum.loop = true;
            hum.ignoreListenerPause = true; // keep the hum going in the pause menu so the sliders can be heard
            hum.volume = HumVolume * Settings.Ambience;
            hum.Play();
            sfx = gameObject.AddComponent<AudioSource>();
            ui = gameObject.AddComponent<AudioSource>();
            ui.ignoreListenerPause = true;

            vignette = MakeVignette();
            texPanel = Solid(new Color(0.06f, 0.045f, 0.035f, 0.9f));
            texButton = Solid(new Color(0.14f, 0.09f, 0.07f, 0.95f));
            texHover = Solid(new Color(0.42f, 0.1f, 0.08f, 1f));
            NewRun();
        }

        void NewRun()
        {
            floor = Mathf.Clamp(DebugStartFloor, 1, 13);
            mistakes = 0;
            runTime = 0f;
            firstCorridor = true;
            lastOmen = -1;
            bellRings = 0;
            scareDone = false;
            scareFloor = DebugScareFloor > 0 ? DebugScareFloor : Random.Range(4, 10);
            RestoreAtmosphere();
            if (DebugSkipLobby || floor > 1)
            {
                BuildCorridor();
                player.Teleport(Corridor.Spawn, 0f);
            }
            else
            {
                BuildLobby();
                player.Teleport(Lobby.Spawn, 0f);
            }
        }

        void ClearArea()
        {
            if (corridor != null) Destroy(corridor.gameObject);
            if (lobby != null) Destroy(lobby.gameObject);
            corridor = null;
            lobby = null;
            nearBell = false;
            scareArmed = false;
        }

        // Undo anything the jump scare changed, in case it was cut short.
        void RestoreAtmosphere()
        {
            RenderSettings.ambientLight = Ambient;
            if (!hum.isPlaying) hum.Play();
        }

        void BuildLobby()
        {
            ClearArea();
            lobby = Lobby.Build();
            inLobby = true;
            omen = -1;
            RenderSettings.fogDensity = 0.02f;
            hum.pitch = 1f;
        }

        void BuildCorridor()
        {
            ClearArea();
            inLobby = false;
            corridor = Corridor.Build(floor, player);
            // The scare floor always hides a real omen too, so turning back is still the right call.
            scareArmed = !scareDone && !Finale && floor == scareFloor;
            if (Finale)
            {
                omen = -1;
                corridor.ApplyFinale();
            }
            else
            {
                omen = PickOmen(scareArmed);
                if (omen >= 0) corridor.ApplyOmen(omen);
            }
            firstCorridor = false;
            RenderSettings.fogDensity = Mathf.Lerp(0.035f, 0.075f, (floor - 1) / 12f);
            hum.pitch = Mathf.Lerp(1f, 0.75f, (floor - 1) / 12f);
        }

        int PickOmen(bool force)
        {
            if (DebugOmen >= 0 && DebugOmen < Omens.Count && (force || !firstCorridor)) return DebugOmen;
            if (!force)
            {
                if (firstCorridor) return -1; // the first corridor is always safe, so you can learn it
                if (Random.value < 0.4f) return -1;
            }

            // Prefer omens the player hasn't met yet.
            var pool = new List<int>();
            for (int i = 0; i < Omens.Count; i++)
                if (!Settings.Witnessed[i] && i != lastOmen) pool.Add(i);
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
            bool locked = Cursor.lockState == CursorLockMode.Locked;
            bool inRun = state == State.Playing || state == State.Transition;

            // Esc or P releases the mouse, and a released mouse means paused.
            // (In a browser, Esc releases the mouse on its own.)
            if (locked && inRun && XInput.PausePressed())
            {
                Cursor.lockState = CursorLockMode.None;
                locked = false;
            }
            if (!locked) Cursor.visible = true;

            bool nowPaused = inRun && !locked;
            if (nowPaused != paused)
            {
                paused = nowPaused;
                Time.timeScale = paused ? 0f : 1f;
                AudioListener.pause = paused;
                if (!paused) Settings.Save();
            }
            AudioListener.volume = Settings.Master;
            hum.volume = HumVolume * Settings.Ambience;

            float dt = Time.deltaTime; // 0 while paused, so every timer below freezes
            player.InputEnabled = state == State.Playing && locked;
            if (player.InputEnabled) runTime += dt;

            msgTimer -= dt;
            redFlash = Mathf.MoveTowards(redFlash, 0f, dt * 0.7f);
            grace -= dt;
            thudCooldown -= dt;

            nearBell = false;
            if (state == State.Playing && !paused && grace <= 0f && inLobby)
            {
                Vector3 p = player.transform.position;
                if (p.z > Lobby.ExitZ && Mathf.Abs(p.x) < 1.1f)
                {
                    StartCoroutine(TakeLift());
                }
                else if (p.z < Lobby.EntranceZ && thudCooldown <= 0f)
                {
                    Play(Sfx.Thud, 1f);
                    Show("", "The doors are locked.", Red, 2.5f);
                    thudCooldown = 2.5f;
                }
                Vector2 toBell = new Vector2(Lobby.Bell.x - p.x, Lobby.Bell.z - p.z);
                nearBell = toBell.magnitude < 1.8f;
                if (nearBell && player.InputEnabled && XInput.InteractPressed()) RingBell();
            }
            else if (state == State.Playing && !paused && grace <= 0f)
            {
                float z = player.transform.position.z;
                if (scareArmed && z > 11f) StartCoroutine(JumpScare());
                if (z > Corridor.ForwardTrigger)
                {
                    StartCoroutine(Go(true));
                }
                else if (z < Corridor.BackTrigger)
                {
                    if (!Finale) StartCoroutine(Go(false));
                    else if (thudCooldown <= 0f)
                    {
                        Play(Sfx.Thud, 1f);
                        Show("", "It will not open.", Red, 2.5f);
                        thudCooldown = 2.5f;
                    }
                }
            }
        }

        void Play(AudioClip clip, float volume)
        {
            sfx.PlayOneShot(clip, volume * Settings.Effects);
        }

        IEnumerator Go(bool forward)
        {
            state = State.Transition;
            Play(Sfx.Door, 0.8f);
            yield return FadeTo(1f, 0.35f);

            if (Finale)
            {
                state = State.Ended;
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                Play(Sfx.Chime, 0.8f);
                yield break;
            }

            bool hadOmen = omen >= 0;
            bool correct = forward != hadOmen;
            if (hadOmen && !Settings.Witnessed[omen])
            {
                Settings.Witnessed[omen] = true;
                Settings.Save();
            }

            if (correct)
            {
                floor++;
                if (Finale) Show("FLOOR XIII", "There is no thirteenth floor.", Red, 5f);
                else Show("FLOOR " + Omens.Roman(floor), hadOmen ? "You turned away from " + Omens.Names[omen] + "." : "", Color.white, 3.5f);
                Play(Sfx.Chime, 0.6f);
            }
            else
            {
                mistakes++;
                floor = 1;
                Show("FLOOR I", hadOmen ? "You walked past " + Omens.Names[omen] + "." : "Nothing was wrong. You turned back anyway.", Red, 4.5f);
                Play(Sfx.Stinger, 1f);
                redFlash = 1f;
            }

            BuildCorridor();
            player.Teleport(Corridor.Spawn, 0f);
            grace = 0.5f;
            yield return new WaitForSeconds(0.25f);
            yield return FadeTo(0f, 0.6f);
            state = State.Playing;
        }

        void RingBell()
        {
            Play(Sfx.Bell, 0.7f);
            bellRings++;
            if (bellRings == 3) Show("", "No one is coming.", Paper, 3f);
            else if (bellRings == 7) Show("", "Stop.", Red, 2.5f);
        }

        IEnumerator TakeLift()
        {
            state = State.Transition;
            Play(Sfx.Ding, 0.8f);
            yield return FadeTo(1f, 0.6f);
            yield return new WaitForSeconds(0.6f);
            BuildCorridor();
            player.Teleport(Corridor.Spawn, 0f);
            grace = 0.5f;
            Show("FLOOR " + Omens.Roman(floor), "If you see an omen, turn back.", Color.white, 4f);
            yield return FadeTo(0f, 0.6f);
            state = State.Playing;
        }

        // Once per run: the power dies, and when the lights come back something is right in front of you.
        IEnumerator JumpScare()
        {
            scareArmed = false;
            scareDone = true;
            Corridor c = corridor;

            c.SetLights(false);
            RenderSettings.ambientLight = Ambient * 0.06f;
            hum.Stop();
            Play(Sfx.PowerDown, 1f);
            yield return new WaitForSeconds(1.6f);
            if (c == null) { RestoreAtmosphere(); yield break; } // walked through a door in the dark

            GameObject face = c.SpawnScare(player.Cam.transform);
            c.SetLights(true);
            RenderSettings.ambientLight = Ambient;
            Play(Sfx.Scream, 1f);
            redFlash = 0.7f;
            player.Shake = 1f;
            yield return new WaitForSeconds(0.6f);
            if (c == null) { RestoreAtmosphere(); yield break; }

            c.SetLights(false);
            RenderSettings.ambientLight = Ambient * 0.06f;
            Destroy(face);
            yield return new WaitForSeconds(0.5f);
            if (c != null) c.SetLights(true);
            RestoreAtmosphere();
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
            Lock();
            if (inLobby) Show("HOTEL TREDECIM", "Your room is on the thirteenth floor. Take the lift.", Brass, 4.5f);
            else Show("FLOOR " + Omens.Roman(floor), "Your room is on the thirteenth floor.", Color.white, 4f);
        }

        void Lock()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        void ExitToMenu()
        {
            StopAllCoroutines(); // abandon any door transition in progress
            state = State.Title;
            fade = redFlash = msgTimer = 0f;
            Settings.Save();
            NewRun();
        }

        // ---------- UI (IMGUI: no Canvas, no TextMeshPro, works everywhere) ----------

        void EnsureStyles()
        {
            if (big == null)
            {
                big = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, wordWrap = true };
                mid = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, wordWrap = true };
                small = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.UpperCenter, wordWrap = true };
                label = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleLeft, wordWrap = false, clipping = TextClipping.Clip };
                labelRight = new GUIStyle(label) { alignment = TextAnchor.MiddleRight };
                heading = new GUIStyle(label) { fontStyle = FontStyle.Bold };
                button = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            }
            float u = Screen.height / 1080f;
            big.fontSize = Mathf.RoundToInt(72 * u);
            mid.fontSize = Mathf.RoundToInt(30 * u);
            small.fontSize = Mathf.RoundToInt(22 * u);
            label.fontSize = labelRight.fontSize = Mathf.RoundToInt(22 * u);
            heading.fontSize = Mathf.RoundToInt(20 * u);
            button.fontSize = Mathf.RoundToInt(26 * u);
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

            if (msgTimer > 0f && (state == State.Playing || state == State.Transition) && !paused)
            {
                float a = Mathf.Clamp01(msgTimer) * Mathf.Clamp01((msgDuration - msgTimer) * 3f);
                Color c = msgColor; c.a = a;
                big.normal.textColor = c;
                mid.normal.textColor = new Color(0.9f, 0.88f, 0.82f, a);
                GUI.Label(new Rect(0f, H * 0.33f, W, 110f * u), msgTitle, big);
                GUI.Label(new Rect(W * 0.15f, H * 0.33f + 110f * u, W * 0.7f, 60f * u), msgSub, mid);
            }

            if (nearBell && state == State.Playing && !paused)
            {
                small.normal.textColor = Paper;
                GUI.Label(new Rect(0f, H * 0.62f, W, 30f * u), "[E] Ring the bell", small);
            }

            if (state == State.Title) DrawTitle(u);
            else if (paused) DrawPause(u);
            else if (state == State.Ended) DrawEnd(u);
        }

        void Shade(float alpha)
        {
            GUI.color = new Color(0f, 0f, 0f, alpha);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        // Acts on mouse DOWN, not up: browsers only grant mouse capture during a click event,
        // and Unity applies the lock on the mouse-up that follows.
        bool Button(Rect r, string text)
        {
            Event e = Event.current;
            bool hover = r.Contains(e.mousePosition);
            GUI.DrawTexture(r, hover ? texHover : texButton);
            button.normal.textColor = hover ? Color.white : Brass;
            GUI.Label(r, text, button);
            if (e.type == EventType.MouseDown && e.button == 0 && hover)
            {
                e.Use();
                return true;
            }
            return false;
        }

        void Columns(float u, out float leftX, out float rightX, out float colW)
        {
            float gap = 40f * u;
            colW = Mathf.Min(460f * u, Screen.width * 0.44f);
            leftX = Screen.width * 0.5f - gap * 0.5f - colW;
            rightX = Screen.width * 0.5f + gap * 0.5f;
        }

        void DrawTitle(float u)
        {
            float W = Screen.width, H = Screen.height;
            Shade(0.88f);

            float y = 60f * u;
            big.normal.textColor = Red;
            GUI.Label(new Rect(0f, y, W, 100f * u), "X I I I", big);
            y += 95f * u;
            mid.normal.textColor = new Color(0.8f, 0.76f, 0.66f);
            GUI.Label(new Rect(0f, y, W, 40f * u), "there is no thirteenth floor", mid);
            y += 60f * u;

            small.normal.textColor = Paper;
            string rules =
                "You are a guest on the first floor. Your room is on the thirteenth.\n\n" +
                "If you see an OMEN, anything at all that is not as it should be, turn back.\n" +
                "If nothing is wrong, keep walking forward.\n" +
                "One wrong choice and you start again on Floor I.";
            GUI.Label(new Rect(W * 0.2f, y, W * 0.6f, 160f * u), rules, small);
            y += 165f * u;

            if (Button(new Rect(W * 0.5f - 180f * u, y, 360f * u, 64f * u), "CHECK IN")) Begin();
            y += 80f * u;
            small.normal.textColor = Dim;
            GUI.Label(new Rect(0f, y, W, 30f * u), "WASD move  ·  Mouse look  ·  Shift hurry  ·  Esc or P pause", small);
            y += 50f * u;

            float lx, rx, cw;
            Columns(u, out lx, out rx, out cw);
            DrawSettings(lx, y, cw, u);
            DrawJournal(rx, y, cw, u);

            small.normal.textColor = Dim;
            GUI.Label(new Rect(0f, H - 45f * u, W, 30f * u), "Made in a day for the Tuesday the 13th Jam by " + Author + " with Unity 6 and Claude Code", small);
        }

        void DrawPause(float u)
        {
            float W = Screen.width, H = Screen.height;
            Shade(0.82f);

            float y = 80f * u;
            big.normal.textColor = Red;
            GUI.Label(new Rect(0f, y, W, 100f * u), "PAUSED", big);
            y += 100f * u;
            int mins = (int)(runTime / 60f), secs = (int)(runTime % 60f);
            mid.normal.textColor = Dim;
            GUI.Label(new Rect(0f, y, W, 40f * u),
                (inLobby ? "Lobby" : "Floor " + Omens.Roman(Mathf.Min(floor, 13))) + "    ·    Wrong turns " + mistakes + "    ·    " + mins + ":" + secs.ToString("00"), mid);
            y += 80f * u;

            float lx, rx, cw;
            Columns(u, out lx, out rx, out cw);
            if (Button(new Rect(lx, y, cw, 58f * u), "RESUME")) Lock();
            if (Button(new Rect(lx, y + 72f * u, cw, 58f * u), "EXIT TO MENU")) ExitToMenu();
            DrawSettings(lx, y + 160f * u, cw, u);
            DrawJournal(rx, y, cw, u);

            small.normal.textColor = Dim;
            GUI.Label(new Rect(0f, H - 50f * u, W, 30f * u), "Exit to menu ends this run. The omens you've found stay in your journal.", small);
        }

        void DrawSettings(float x, float y, float w, float u)
        {
            float h = 316f * u;
            GUI.DrawTexture(new Rect(x, y, w, h), texPanel);
            float pad = 22f * u;
            x += pad; w -= pad * 2f; y += 16f * u;

            heading.normal.textColor = Brass;
            GUI.Label(new Rect(x, y, w, 30f * u), "SOUND", heading);
            y += 38f * u;

            float v;
            v = SliderRow(ref y, x, w, u, "Master", Settings.Master, Pct(Settings.Master));
            if (v != Settings.Master) { Settings.Master = v; Preview(Sfx.Chime, 0.35f * Settings.Effects); }
            v = SliderRow(ref y, x, w, u, "Ambience", Settings.Ambience, Pct(Settings.Ambience));
            if (v != Settings.Ambience) Settings.Ambience = v; // the hum keeps playing, so this is heard live
            v = SliderRow(ref y, x, w, u, "Footsteps", Settings.Footsteps, Pct(Settings.Footsteps));
            if (v != Settings.Footsteps) { Settings.Footsteps = v; Preview(Sfx.Step, 0.2f * v); }
            v = SliderRow(ref y, x, w, u, "Effects", Settings.Effects, Pct(Settings.Effects));
            if (v != Settings.Effects) { Settings.Effects = v; Preview(Sfx.Tick, 0.6f * v); }

            y += 10f * u;
            GUI.Label(new Rect(x, y, w, 30f * u), "CONTROLS", heading);
            y += 38f * u;
            // Slider runs 0.2x to 3x the default sensitivity.
            float rel = SliderRow(ref y, x, w, u, "Mouse", Settings.Sensitivity / 0.1f, (Settings.Sensitivity / 0.1f).ToString("0.0") + "x", 0.2f, 3f);
            Settings.Sensitivity = rel * 0.1f;
        }

        float SliderRow(ref float y, float x, float w, float u, string name, float value, string valueText, float min = 0f, float max = 1f)
        {
            label.normal.textColor = Paper;
            labelRight.normal.textColor = Dim;
            GUI.Label(new Rect(x, y, w * 0.3f, 30f * u), name, label);
            float v = GUI.HorizontalSlider(new Rect(x + w * 0.32f, y + 9f * u, w * 0.48f, 20f * u), value, min, max);
            GUI.Label(new Rect(x + w * 0.82f, y, w * 0.18f, 30f * u), valueText, labelRight);
            y += 40f * u;
            return v;
        }

        static string Pct(float v) { return Mathf.RoundToInt(v * 100f) + "%"; }

        void Preview(AudioClip clip, float volume)
        {
            if (Time.unscaledTime < nextPreview) return;
            nextPreview = Time.unscaledTime + 0.25f;
            ui.PlayOneShot(clip, volume);
        }

        void DrawJournal(float x, float y, float w, float u)
        {
            float line = 26f * u;
            float h = 16f * u + 38f * u + Omens.Count * line + 16f * u;
            GUI.DrawTexture(new Rect(x, y, w, h), texPanel);
            float pad = 22f * u;
            x += pad; w -= pad * 2f; y += 16f * u;

            heading.normal.textColor = Brass;
            GUI.Label(new Rect(x, y, w, 30f * u), "OMENS WITNESSED   " + Settings.WitnessedCount() + " / 13", heading);
            y += 38f * u;

            for (int i = 0; i < Omens.Count; i++)
            {
                bool seen = Settings.Witnessed[i];
                labelRight.normal.textColor = seen ? new Color(0.75f, 0.3f, 0.25f) : new Color(0.35f, 0.33f, 0.3f);
                label.normal.textColor = seen ? Paper : new Color(0.35f, 0.33f, 0.3f);
                GUI.Label(new Rect(x, y, 56f * u, line), Omens.Roman(i + 1), labelRight);
                GUI.Label(new Rect(x + 72f * u, y, w - 72f * u, line), seen ? Omens.Names[i] : "· · ·", label);
                y += line;
            }
        }

        void DrawEnd(float u)
        {
            float W = Screen.width, H = Screen.height;
            GUI.color = Color.black;
            GUI.DrawTexture(new Rect(0f, 0f, W, H), Texture2D.whiteTexture);
            GUI.color = Color.white;

            float y = H * 0.12f;
            big.normal.textColor = Red;
            GUI.Label(new Rect(0f, y, W, 100f * u), "FLOOR XIII", big);
            y += 110f * u;
            mid.normal.textColor = new Color(0.85f, 0.82f, 0.75f);
            GUI.Label(new Rect(0f, y, W, 100f * u), "You found the floor that isn't there.\nCheck-out is never.", mid);
            y += 140f * u;

            int mins = (int)(runTime / 60f), secs = (int)(runTime % 60f);
            small.normal.textColor = new Color(0.8f, 0.78f, 0.7f);
            GUI.Label(new Rect(0f, y, W, 100f * u),
                "Time  " + mins + ":" + secs.ToString("00") + "      Wrong turns  " + mistakes + "      Omens witnessed  " + Settings.WitnessedCount() + " / 13", small);
            y += 80f * u;

            small.normal.textColor = new Color(0.55f, 0.53f, 0.48f);
            GUI.Label(new Rect(W * 0.15f, y, W * 0.7f, 200f * u),
                "XIII was made in one day for the Tuesday the 13th Jam (13 October 2026).\n" +
                "Theme: \"Unlucky XIII\"  ·  Secondary theme: liminal horror\n\n" +
                "Design & direction: " + Author + "\n" +
                "Built with Unity 6. Code co-written with Claude Code (Anthropic).\n" +
                "All geometry and sound are generated procedurally in code. No asset packs.", small);
            y += 210f * u;

            float bw = 320f * u, gap = 30f * u;
            if (Button(new Rect(W * 0.5f - bw - gap * 0.5f, y, bw, 60f * u), "CHECK IN AGAIN"))
            {
                NewRun();
                fade = 0f;
                Begin();
            }
            if (Button(new Rect(W * 0.5f + gap * 0.5f, y, bw, 60f * u), "MAIN MENU")) ExitToMenu();
        }

        static Texture2D Solid(Color c)
        {
            var t = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            t.SetPixel(0, 0, c);
            t.Apply();
            return t;
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
