using UnityEngine;

namespace XIII
{
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        public const float EyeHeight = 1.6f;

        public Camera Cam;
        public bool InputEnabled;
        public float WalkSpeed = 2.3f;
        public float RunSpeed = 3.8f;

        CharacterController cc;
        AudioSource stepSource;
        float yaw, pitch, vy, stepAcc, bobPhase, bobAmount;

        void Awake()
        {
            cc = GetComponent<CharacterController>();
            cc.height = 1.8f;
            cc.radius = 0.3f;
            cc.center = new Vector3(0f, 0.9f, 0f);
            cc.stepOffset = 0.3f;

            var camGo = new GameObject("Camera");
            camGo.transform.SetParent(transform, false);
            camGo.transform.localPosition = new Vector3(0f, EyeHeight, 0f);
            Cam = camGo.AddComponent<Camera>();
            Cam.clearFlags = CameraClearFlags.SolidColor;
            Cam.backgroundColor = Color.black;
            Cam.nearClipPlane = 0.05f;
            Cam.farClipPlane = 80f;
            Cam.fieldOfView = 70f;
            camGo.AddComponent<AudioListener>();

            stepSource = gameObject.AddComponent<AudioSource>();
            stepSource.playOnAwake = false;
            stepSource.spatialBlend = 0f;
            stepSource.volume = 0.2f; // quiet: it plays constantly
        }

        public void Teleport(Vector3 position, float yawDegrees)
        {
            cc.enabled = false;
            transform.position = position;
            yaw = yawDegrees;
            pitch = 0f;
            vy = 0f;
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            Cam.transform.localRotation = Quaternion.identity;
            cc.enabled = true;
        }

        void Update()
        {
            float dt = Time.deltaTime;

            Vector2 look = InputEnabled ? XInput.LookDelta() : Vector2.zero;
            yaw += look.x * Settings.Sensitivity;
            pitch = Mathf.Clamp(pitch - look.y * Settings.Sensitivity, -80f, 80f);
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            Cam.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);

            Vector2 mv = InputEnabled ? XInput.Move() : Vector2.zero;
            if (mv.sqrMagnitude > 1f) mv.Normalize();
            bool running = InputEnabled && XInput.Run();
            float speed = running ? RunSpeed : WalkSpeed;
            Vector3 wish = (transform.forward * mv.y + transform.right * mv.x) * speed;
            vy = cc.isGrounded ? -1f : vy - 9.81f * dt;

            Vector3 before = transform.position;
            cc.Move((wish + Vector3.up * vy) * dt);
            Vector3 delta = transform.position - before;
            delta.y = 0f;
            float moved = delta.magnitude;

            float stride = running ? 1.0f : 0.8f;
            stepAcc += moved;
            if (stepAcc >= stride)
            {
                stepAcc = 0f;
                stepSource.pitch = Random.Range(0.92f, 1.05f);
                stepSource.PlayOneShot(Sfx.Step, Settings.Footsteps);
            }

            // head bob
            bool walking = moved > 0.0005f;
            bobAmount = Mathf.MoveTowards(bobAmount, walking ? 1f : 0f, dt * 4f);
            if (walking) bobPhase += moved / stride * Mathf.PI;
            float bob = -Mathf.Abs(Mathf.Sin(bobPhase)) * 0.045f * bobAmount;
            Cam.transform.localPosition = new Vector3(0f, EyeHeight + bob, 0f);
        }
    }
}
