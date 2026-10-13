using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace XIII
{
    // Material factory. Base materials live in Assets/XIII/Resources (created by XIII > Setup Project)
    // so their shaders are guaranteed to be included in the WebGL build.
    public static class Mats
    {
        static Material litBase, unlitBase, textMat, glowBase;
        static Texture2D glowTex;
        static Mesh glowQuad;
        static Font font;
        static readonly Dictionary<long, Material> cache = new Dictionary<long, Material>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            litBase = unlitBase = textMat = glowBase = null;
            glowTex = null;
            glowQuad = null;
            font = null;
            cache.Clear();
        }

        public static bool IsURP { get { return GraphicsSettings.currentRenderPipeline != null; } }

        static Material Base(ref Material slot, string resource, string urpShader, string builtinShader)
        {
            if (slot != null) return slot;
            slot = Resources.Load<Material>(resource);
            if (slot == null)
            {
                Debug.LogWarning("[XIII] Missing Resources/" + resource + ". Run XIII > Setup Project, or the WebGL build may render pink.");
                slot = new Material(Shader.Find(IsURP ? urpShader : builtinShader));
            }
            return slot;
        }

        static long Key(Color c, float s, int kind)
        {
            Color32 k = c;
            return ((long)kind << 40) | ((long)(int)(s * 255f) << 32) | ((long)k.r << 24) | ((long)k.g << 16) | ((long)k.b << 8) | k.a;
        }

        public static Material Lit(Color c, float smoothness = 0.15f)
        {
            long key = Key(c, smoothness, 0);
            Material m;
            if (cache.TryGetValue(key, out m) && m != null) return m;
            m = new Material(Base(ref litBase, "XIII_Lit", "Universal Render Pipeline/Lit", "Standard"));
            m.color = c;
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smoothness);
            cache[key] = m;
            return m;
        }

        // Unaffected by lighting: light panels, glowing eyes, signs.
        public static Material Unlit(Color c)
        {
            long key = Key(c, 0f, 1);
            Material m;
            if (cache.TryGetValue(key, out m) && m != null) return m;
            m = new Material(Base(ref unlitBase, "XIII_Unlit", "Universal Render Pipeline/Unlit", "Unlit/Color"));
            m.color = c;
            cache[key] = m;
            return m;
        }

        // A material that shows a texture, lit like everything else (the portrait).
        public static Material LitTextured(Texture tex, float smoothness = 0.2f)
        {
            var m = new Material(Base(ref litBase, "XIII_Lit", "Universal Render Pipeline/Lit", "Standard"));
            m.color = Color.white;
            m.mainTexture = tex;
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smoothness);
            return m;
        }

        // Additive soft glow (light halos, dust). Alpha scales the brightness. Null if the glow shader is missing.
        public static Material Glow(Color c)
        {
            long key = Key(c, 0f, 2);
            Material m;
            if (cache.TryGetValue(key, out m) && m != null) return m;
            if (glowBase == null)
            {
                glowBase = Resources.Load<Material>("XIII_Glow");
                if (glowBase == null)
                {
                    Shader sh = Shader.Find("XIII/Glow");
                    if (sh == null) return null;
                    glowBase = new Material(sh);
                }
            }
            m = new Material(glowBase);
            m.mainTexture = GlowTexture;
            m.color = c;
            cache[key] = m;
            return m;
        }

        static Texture2D GlowTexture
        {
            get
            {
                if (glowTex != null) return glowTex;
                const int N = 64;
                glowTex = new Texture2D(N, N, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
                var px = new Color32[N * N];
                for (int y = 0; y < N; y++)
                    for (int x = 0; x < N; x++)
                    {
                        float dx = (x + 0.5f) / N * 2f - 1f, dy = (y + 0.5f) / N * 2f - 1f;
                        float r = Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy));
                        float a = Mathf.Pow(1f - r, 2.4f) * 0.8f + Mathf.Pow(1f - r, 8f) * 0.2f;
                        px[y * N + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                    }
                glowTex.SetPixels32(px);
                glowTex.Apply();
                return glowTex;
            }
        }

        // A unit quad with white vertex colours, for glow billboards.
        public static Mesh GlowQuad
        {
            get
            {
                if (glowQuad != null) return glowQuad;
                glowQuad = new Mesh { name = "GlowQuad" };
                glowQuad.vertices = new[] { new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f), new Vector3(0.5f, 0.5f, 0f) };
                glowQuad.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) };
                glowQuad.colors = new[] { Color.white, Color.white, Color.white, Color.white };
                glowQuad.triangles = new[] { 0, 2, 1, 2, 3, 1 };
                glowQuad.RecalculateBounds();
                return glowQuad;
            }
        }

        public static Font Font
        {
            get
            {
                if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                return font;
            }
        }

        // Depth-tested text material (the default font material draws through walls).
        public static Material Text
        {
            get
            {
                if (textMat != null) return textMat;
                var b = Resources.Load<Material>("XIII_Text");
                if (b == null) return textMat = Font.material;
                textMat = new Material(b);
                textMat.mainTexture = Font.material.mainTexture;
                Font.textureRebuilt += f => { if (f == font && textMat != null) textMat.mainTexture = f.material.mainTexture; };
                return textMat;
            }
        }
    }
}
