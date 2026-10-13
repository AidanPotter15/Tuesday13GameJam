using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace XIII
{
    // The black cat waits until you get close, walks across your path, then turns to stare at you.
    public class CatWalker : MonoBehaviour
    {
        public Transform Target;   // the player
        public Transform LookAt;   // the player's camera
        public Vector3 From, To;
        public Dictionary<string, Transform> Rig;

        enum Mode { Waiting, Walking, Turning, Idle }
        const float Speed = 0.55f;  // m/s
        const float Stride = 0.42f; // metres per full gait cycle

        Mode mode = Mode.Waiting;
        Transform body, head, tail, tail2, eyeL, eyeR;
        Transform[] upper, lower;
        // Lateral-sequence walk: hind left, front left, hind right, front right.
        readonly float[] offsets = { 0.25f, 0.75f, 0f, 0.5f }; // FrontL, FrontR, HindL, HindR
        Vector3 bodyRest;
        float phase, amp, blinkTimer, blink;

        void Start()
        {
            body = Rig["Body"];
            head = Rig["Head"];
            tail = Rig["Tail"];
            tail2 = Rig["Tail2"];
            eyeL = Rig["EyeL"];
            eyeR = Rig["EyeR"];
            upper = new[] { Rig["FrontL"], Rig["FrontR"], Rig["HindL"], Rig["HindR"] };
            lower = new[] { Rig["FrontL2"], Rig["FrontR2"], Rig["HindL2"], Rig["HindR2"] };
            bodyRest = body.localPosition;
            transform.localPosition = From;
            transform.localRotation = Quaternion.LookRotation(To - From);
            blinkTimer = Random.Range(1.5f, 3.5f);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f || Target == null) return;
            float gait = 0f;
            Vector3 toPlayer = Target.position - transform.position;
            toPlayer.y = 0f;

            switch (mode)
            {
                case Mode.Waiting:
                    if (Mathf.Abs(toPlayer.z) < 7f) mode = Mode.Walking;
                    break;
                case Mode.Walking:
                {
                    Vector3 to = To - transform.localPosition;
                    to.y = 0f;
                    float step = Speed * dt;
                    if (to.magnitude <= step)
                    {
                        transform.localPosition = To;
                        mode = Mode.Turning;
                    }
                    else
                    {
                        transform.localPosition += to.normalized * step;
                        transform.localRotation = Quaternion.RotateTowards(transform.localRotation, Quaternion.LookRotation(to), 120f * dt);
                    }
                    gait = 1f;
                    phase += step / (Stride * transform.localScale.x);
                    break;
                }
                case Mode.Turning:
                {
                    Quaternion want = Quaternion.LookRotation(toPlayer);
                    transform.rotation = Quaternion.RotateTowards(transform.rotation, want, 75f * dt);
                    gait = 0.5f;
                    phase += dt * 1.1f;
                    if (Quaternion.Angle(transform.rotation, want) < 3f) mode = Mode.Idle;
                    break;
                }
                case Mode.Idle:
                    if (Vector3.Angle(transform.forward, toPlayer) > 70f) mode = Mode.Turning;
                    break;
            }

            // Legs: each swings on its own phase, and the lower leg folds while the paw is lifted.
            amp = Mathf.MoveTowards(amp, gait, dt * 3f);
            for (int i = 0; i < 4; i++)
            {
                float ph = (phase + offsets[i]) * Mathf.PI * 2f;
                float lift = Mathf.Max(0f, -Mathf.Cos(ph)) * amp;
                upper[i].localRotation = Quaternion.Euler(Mathf.Sin(ph) * 24f * amp, 0f, 0f);
                lower[i].localRotation = Quaternion.Euler(lift * (i < 2 ? 55f : 40f), 0f, 0f);
            }
            body.localPosition = bodyRest + Vector3.up * Mathf.Sin(phase * Mathf.PI * 4f) * 0.004f * amp;
            body.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(phase * Mathf.PI * 2f) * 2.5f * amp);

            // Head follows you: glances while walking, stares once stopped.
            Vector3 local = body.InverseTransformDirection(LookAt.position - head.position);
            float yaw = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;
            float pitch = -Mathf.Atan2(local.y, new Vector2(local.x, local.z).magnitude) * Mathf.Rad2Deg;
            float w = mode == Mode.Walking ? 0.35f : 1f;
            Quaternion look = Quaternion.Euler(Mathf.Clamp(pitch, -35f, 20f) * w, Mathf.Clamp(yaw, -80f, 80f) * w, 0f);
            head.localRotation = Quaternion.Slerp(head.localRotation, look, dt * 4f);

            // Tail: slow, lazy swish, the tip lagging behind.
            float t = Time.time;
            tail.localRotation = Quaternion.Euler(Mathf.Sin(t * 1.3f) * 6f, Mathf.Sin(t * 1.1f) * 16f, 0f);
            tail2.localRotation = Quaternion.Euler(Mathf.Sin(t * 1.3f - 0.7f) * 10f, Mathf.Sin(t * 1.1f - 0.9f) * 30f, 0f);

            // Blink.
            blinkTimer -= dt;
            if (blinkTimer <= 0f)
            {
                blink = 0.14f;
                blinkTimer = Random.Range(2.5f, 5f);
            }
            blink -= dt;
            Vector3 eyeScale = new Vector3(1f, blink > 0f ? 0.12f : 1f, 1f);
            eyeL.localScale = eyeScale;
            eyeR.localScale = eyeScale;
        }
    }

    // Painted eyes set into the portrait. At rest they stare straight out; when Follow is on,
    // the pupils slide to keep looking at you.
    public class EyeFollow : MonoBehaviour
    {
        public Transform Target;
        public Transform Pupil;
        public bool Follow;
        public float RangeX = 0.011f, RangeY = 0.005f;
        Vector3 rest;

        void Start() { rest = Pupil.localPosition; }

        void LateUpdate()
        {
            Vector3 want = rest;
            if (Follow && Target != null)
            {
                Vector3 d = transform.InverseTransformDirection(Target.position - transform.position).normalized;
                want += new Vector3(Mathf.Clamp(d.x * 0.03f, -RangeX, RangeX), Mathf.Clamp(d.y * 0.02f, -RangeY, RangeY), 0f);
            }
            Pupil.localPosition = Vector3.Lerp(Pupil.localPosition, want, Time.deltaTime * 8f);
        }
    }

    // Keeps a glow quad facing the camera.
    public class Billboard : MonoBehaviour
    {
        public static Transform View;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { View = null; }

        void LateUpdate()
        {
            if (View != null) transform.rotation = Quaternion.LookRotation(transform.position - View.position);
        }
    }

    public class ClockHands : MonoBehaviour
    {
        public Transform Hour, Minute, Second;
        public bool Reverse;
        AudioSource src;
        float tickTimer;

        void Start()
        {
            src = gameObject.AddComponent<AudioSource>();
            src.spatialBlend = 1f;
            src.minDistance = 1f;
            src.maxDistance = 9f;
            src.rolloffMode = AudioRolloffMode.Linear;
            src.volume = 0.5f;
        }

        void Update()
        {
            float h, m, s;
            if (!Reverse)
            {
                var now = System.DateTime.Now;
                s = now.Second;
                m = now.Minute + s / 60f;
                h = (now.Hour % 12) + m / 60f;
            }
            else
            {
                float t = Time.time;
                s = 60f - (t * 4f) % 60f;
                m = 60f - (t * 0.8f) % 60f;
                h = 12f - (t * 0.07f) % 12f;
            }
            Hour.localRotation = Quaternion.Euler(0f, 0f, h * 30f);
            Minute.localRotation = Quaternion.Euler(0f, 0f, m * 6f);
            Second.localRotation = Quaternion.Euler(0f, 0f, s * 6f);

            tickTimer -= Time.deltaTime;
            if (tickTimer <= 0f)
            {
                tickTimer = Reverse ? 0.25f : 1f;
                src.pitch = Reverse ? 0.7f : 1f;
                src.PlayOneShot(Sfx.Tick, Settings.Effects);
            }
        }
    }

    public class Flicker : MonoBehaviour
    {
        public Light Light;
        public Renderer Panel, Glow;
        public Material OnMat, OffMat;
        float baseIntensity, seed;

        void Start()
        {
            baseIntensity = Light.intensity;
            seed = Random.value * 100f;
        }

        void Update()
        {
            float n = Mathf.PerlinNoise(seed, Time.time * 6f);
            bool off = n < 0.3f;
            Light.intensity = off ? baseIntensity * 0.05f : baseIntensity * (0.7f + 0.3f * n);
            if (Panel != null) Panel.sharedMaterial = off ? OffMat : OnMat;
            if (Glow != null) Glow.enabled = !off && Light.enabled;
        }
    }

    // The other guest: a too-tall bellhop at the far end of the hall, head cocked, watching.
    // Get close and he flickers out, and the light above him stutters with him.
    public class GuestPhantom : MonoBehaviour
    {
        public Transform Target;
        public Dictionary<string, Transform> Rig;
        public Light Lamp;
        public Renderer LampGlow;
        public float Distance = 9f;

        Transform head;
        Renderer[] rends;
        AudioSource src;
        bool vanishing;
        float seed;

        void Start()
        {
            head = Rig["Head"];
            rends = GetComponentsInChildren<Renderer>();
            seed = Random.value * 10f;
            src = gameObject.AddComponent<AudioSource>();
            src.spatialBlend = 1f;
            src.minDistance = 2f;
            src.maxDistance = 20f;
        }

        void Update()
        {
            if (vanishing || Target == null || Time.deltaTime <= 0f) return;
            Vector3 d = Target.position - transform.position;
            d.y = 0f;
            if (d.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(d), 25f * Time.deltaTime);

            float t = Time.time + seed;
            head.localRotation = Quaternion.Euler(6f + Mathf.Sin(t * 0.5f) * 3f, 0f, 17f + Mathf.Sin(t * 0.37f) * 5f);
            if (Random.value < Time.deltaTime * 0.5f) // a one-frame twitch, now and then
                head.localRotation *= Quaternion.Euler(Random.Range(-6f, 6f), Random.Range(-12f, 12f), Random.Range(-10f, 10f));

            if (d.magnitude < Distance) StartCoroutine(Vanish());
        }

        IEnumerator Vanish()
        {
            vanishing = true;
            src.PlayOneShot(Sfx.Whisper, 0.7f * Settings.Effects);
            float baseIntensity = Lamp != null ? Lamp.intensity : 0f;
            for (int k = 0; k < 7; k++)
            {
                bool on = k % 2 == 1;
                foreach (var r in rends) r.enabled = on;
                if (Lamp != null) Lamp.intensity = on ? baseIntensity : baseIntensity * 0.15f;
                if (LampGlow != null) LampGlow.enabled = on;
                yield return new WaitForSeconds(Random.Range(0.04f, 0.11f));
            }
            if (Lamp != null) Lamp.intensity = baseIntensity;
            if (LampGlow != null) LampGlow.enabled = true;
            foreach (var r in rends) r.enabled = false;
        }
    }

    // Jitters a joint every other frame. Used on the jump scare's head.
    public class Twitch : MonoBehaviour
    {
        public float Amount = 6f;
        Quaternion rest;

        void Start() { rest = transform.localRotation; }

        void Update()
        {
            if (Random.value < 0.5f)
                transform.localRotation = rest * Quaternion.Euler(Random.Range(-Amount, Amount), Random.Range(-Amount, Amount), Random.Range(-Amount, Amount));
        }
    }

    // The horseshoe drops off its nail as you approach the far door. The luck runs out.
    public class HorseshoeFall : MonoBehaviour
    {
        public Transform Target;
        public Vector3 FloorPos;
        public Quaternion FloorRot;
        Vector3 startPos;
        Quaternion startRot;
        float t = -1f, delay;
        AudioSource src;

        void Start()
        {
            startPos = transform.localPosition;
            startRot = transform.localRotation;
            delay = Random.Range(0.2f, 1.2f);
            src = gameObject.AddComponent<AudioSource>();
            src.spatialBlend = 1f;
            src.minDistance = 2f;
            src.maxDistance = 25f;
            src.rolloffMode = AudioRolloffMode.Linear;
        }

        void Update()
        {
            if (Target == null || t >= 1f) return;
            if (t < 0f)
            {
                if (Target.position.z > startPos.z - 12f) delay -= Time.deltaTime;
                if (delay <= 0f) t = 0f;
                return;
            }
            t = Mathf.Min(1f, t + Time.deltaTime / 0.5f);
            transform.localPosition = Vector3.Lerp(startPos, FloorPos, t * t); // accelerates like a real drop
            transform.localRotation = Quaternion.Slerp(startRot, FloorRot, t);
            if (t >= 1f) src.PlayOneShot(Sfx.Clink, Settings.Effects);
        }
    }

    // Someone walks behind you: hard heels, out of step with you, and they take two more steps after you stop.
    public class FootstepsBehind : MonoBehaviour
    {
        public PlayerController Player;
        AudioSource src;
        Vector3 lastPos;
        float nextStep, distance = 7f;
        int afterSteps;
        bool wasMoving;

        void Start()
        {
            var go = new GameObject("Follower");
            go.transform.SetParent(transform, false);
            src = go.AddComponent<AudioSource>();
            src.spatialBlend = 1f;
            src.minDistance = 1.5f;
            src.maxDistance = 16f;
            src.volume = 0.9f;
            lastPos = Player.transform.position;
        }

        void Update()
        {
            if (Player == null) return;
            Transform p = Player.transform;
            bool moving = (p.position - lastPos).sqrMagnitude > 0.000001f;
            lastPos = p.position;

            if (moving)
            {
                if (!wasMoving) nextStep = Mathf.Max(nextStep, Time.time + 0.45f);
                afterSteps = 2;
            }
            wasMoving = moving;

            if ((moving || afterSteps > 0) && Time.time >= nextStep)
            {
                if (moving) distance = Mathf.Max(3.5f, distance - 0.05f);
                else { afterSteps--; distance = Mathf.Max(2.5f, distance - 0.6f); } // it keeps coming
                nextStep = Time.time + 0.64f + Random.Range(-0.03f, 0.03f);
                src.transform.position = p.position - p.forward * distance + Vector3.up * 0.1f;
                src.pitch = Random.Range(0.93f, 1.0f);
                src.PlayOneShot(Sfx.Heel, Settings.Effects);
            }
        }
    }

    // Keeps a looping sound in step with the Effects volume slider.
    public class LoopVolume : MonoBehaviour
    {
        public AudioSource Source;
        public float BaseVolume = 1f;

        void Update() { Source.volume = BaseVolume * Settings.Effects; }
    }

    // Grandfather-clock pendulum.
    public class Swing : MonoBehaviour
    {
        public float Angle = 10f, Speed = 2.6f;
        Quaternion rest;

        void Start() { rest = transform.localRotation; }

        void Update() { transform.localRotation = rest * Quaternion.Euler(0f, 0f, Mathf.Sin(Time.time * Speed) * Angle); }
    }
}
