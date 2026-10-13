using System.Collections;
using UnityEngine;

namespace XIII
{
    // The black cat waits until you get close, then crosses your path and stares.
    public class CatWalker : MonoBehaviour
    {
        public Transform Target;
        public Vector3 From, To;
        float t = -1f;

        void Update()
        {
            if (Target == null) return;
            if (t < 0f)
            {
                if (Mathf.Abs(Target.position.z - transform.position.z) < 7f) t = 0f;
                return;
            }
            if (t < 1f)
            {
                t = Mathf.Min(1f, t + Time.deltaTime / 2.6f);
                Vector3 p = Vector3.Lerp(From, To, Mathf.SmoothStep(0f, 1f, t));
                p.y = Mathf.Abs(Mathf.Sin(t * 30f)) * 0.02f;
                transform.localPosition = p;
                transform.localRotation = Quaternion.LookRotation(To - From);
            }
            else
            {
                Vector3 d = Target.position - transform.position;
                d.y = 0f;
                if (d.sqrMagnitude > 0.01f)
                    transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(d), Time.deltaTime * 3f);
            }
        }
    }

    // Portrait eyes. At rest they stare straight out of the wall; when Follow is on they track you.
    public class EyeFollow : MonoBehaviour
    {
        public Transform Target;
        public bool Follow;
        Quaternion rest;

        void Start() { rest = transform.rotation; }

        void LateUpdate()
        {
            if (!Follow || Target == null) return;
            Quaternion want = Quaternion.LookRotation(Target.position - transform.position);
            want = Quaternion.RotateTowards(rest, want, 40f);
            transform.rotation = Quaternion.Slerp(transform.rotation, want, Time.deltaTime * 6f);
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
                src.PlayOneShot(Sfx.Tick);
            }
        }
    }

    public class Flicker : MonoBehaviour
    {
        public Light Light;
        public Renderer Panel;
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
        }
    }

    // A tall guest at the far end of the hall. It is not there when you get close.
    public class FigureVanish : MonoBehaviour
    {
        public Transform Target;
        public float Distance = 9f;

        void Update()
        {
            if (Target == null) return;
            Vector3 d = Target.position - transform.position;
            d.y = 0f;
            if (d.magnitude < Distance) gameObject.SetActive(false);
            else transform.rotation = Quaternion.LookRotation(d);
        }
    }

    // Someone walks a few steps behind you, and doesn't always stop when you do.
    public class FootstepsBehind : MonoBehaviour
    {
        public PlayerController Player;
        AudioSource src;
        float lastStep;
        int extra;

        void Start()
        {
            var go = new GameObject("Follower");
            go.transform.SetParent(transform, false);
            src = go.AddComponent<AudioSource>();
            src.spatialBlend = 1f;
            src.minDistance = 1.5f;
            src.maxDistance = 15f;
            src.volume = 0.8f;
            Player.OnStep += HandleStep;
        }

        void OnDestroy()
        {
            if (Player != null) Player.OnStep -= HandleStep;
        }

        void HandleStep()
        {
            lastStep = Time.time;
            extra = 2;
            StartCoroutine(Echo(0.3f));
        }

        void Update()
        {
            if (extra > 0 && Time.time - lastStep > 0.9f)
            {
                extra--;
                lastStep = Time.time - 0.35f;
                StartCoroutine(Echo(0f));
            }
        }

        IEnumerator Echo(float delay)
        {
            if (delay > 0f) yield return new WaitForSeconds(delay);
            if (Player == null) yield break;
            Transform p = Player.transform;
            src.transform.position = p.position - p.forward * 3.2f + Vector3.up * 0.2f;
            src.pitch = Random.Range(0.75f, 0.85f);
            src.PlayOneShot(Sfx.Step);
        }
    }
}
