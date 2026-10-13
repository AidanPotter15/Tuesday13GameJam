using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace XIII
{
    // Material factory. Base materials live in Assets/XIII/Resources (created by XIII > Setup Project)
    // so their shaders are guaranteed to be included in the WebGL build.
    public static class Mats
    {
        static Material litBase, unlitBase, textMat;
        static Font font;
        static readonly Dictionary<long, Material> cache = new Dictionary<long, Material>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            litBase = unlitBase = textMat = null;
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
