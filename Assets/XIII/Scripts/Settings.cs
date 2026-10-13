using UnityEngine;

namespace XIII
{
    // Player options and the omen journal. Saved with PlayerPrefs (IndexedDB in WebGL builds).
    public static class Settings
    {
        public static float Master = 1f, Ambience = 1f, Footsteps = 1f, Effects = 1f;
        public static float Sensitivity = 0.1f; // degrees per pixel of mouse movement
        public static readonly bool[] Witnessed = new bool[Omens.Count];

        const string Prefix = "XIII.";

        public static void Load()
        {
            Master = PlayerPrefs.GetFloat(Prefix + "master", 1f);
            Ambience = PlayerPrefs.GetFloat(Prefix + "ambience", 1f);
            Footsteps = PlayerPrefs.GetFloat(Prefix + "footsteps", 1f);
            Effects = PlayerPrefs.GetFloat(Prefix + "effects", 1f);
            Sensitivity = PlayerPrefs.GetFloat(Prefix + "sensitivity", 0.1f);
            string seen = PlayerPrefs.GetString(Prefix + "omens", "");
            for (int i = 0; i < Omens.Count; i++) Witnessed[i] = i < seen.Length && seen[i] == '1';
        }

        public static void Save()
        {
            PlayerPrefs.SetFloat(Prefix + "master", Master);
            PlayerPrefs.SetFloat(Prefix + "ambience", Ambience);
            PlayerPrefs.SetFloat(Prefix + "footsteps", Footsteps);
            PlayerPrefs.SetFloat(Prefix + "effects", Effects);
            PlayerPrefs.SetFloat(Prefix + "sensitivity", Sensitivity);
            var sb = new System.Text.StringBuilder(Omens.Count);
            for (int i = 0; i < Omens.Count; i++) sb.Append(Witnessed[i] ? '1' : '0');
            PlayerPrefs.SetString(Prefix + "omens", sb.ToString());
            PlayerPrefs.Save();
        }

        public static int WitnessedCount()
        {
            int n = 0;
            for (int i = 0; i < Omens.Count; i++) if (Witnessed[i]) n++;
            return n;
        }
    }
}
