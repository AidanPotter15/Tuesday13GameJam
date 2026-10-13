namespace XIII
{
    public static class Omens
    {
        public const int Cat = 0, Ladder = 1, Mirror = 2, Umbrella = 3, Horseshoe = 4, Room13 = 5, Clock = 6,
            Portrait = 7, RedLight = 8, Guest = 9, Footsteps = 10, OpenDoor = 11, ExitSign = 12;
        public const int Count = 13;

        public static readonly string[] Names =
        {
            "the black cat",
            "the ladder",
            "the broken mirror",
            "the open umbrella",
            "the fallen horseshoe",
            "Room 13",
            "the backward clock",
            "the watching portrait",
            "the red light",
            "the other guest",
            "the footsteps",
            "the open door",
            "the thirteenth sign",
        };

        public static string Roman(int n)
        {
            if (n <= 0) return "0";
            int[] v = { 1000, 900, 500, 400, 100, 90, 50, 40, 10, 9, 5, 4, 1 };
            string[] s = { "M", "CM", "D", "CD", "C", "XC", "L", "XL", "X", "IX", "V", "IV", "I" };
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < v.Length; i++)
                while (n >= v[i]) { sb.Append(s[i]); n -= v[i]; }
            return sb.ToString();
        }
    }
}
