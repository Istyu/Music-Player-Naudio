namespace MusicPlayer
{
    /// <summary>
    /// Colour set of the user interface. The background is a soft vertical gradient,
    /// panels ("cards") are solid colours on top of it.
    /// </summary>
    public sealed class Theme
    {
        public string Name = "";
        public Color BgTop, BgBottom;   // window background gradient
        public Color Sidebar;           // left navigation bar
        public Color Card;              // panels
        public Color CardHover;         // list row hover / selected row
        public Color Field;             // text boxes, slider tracks
        public Color Accent;            // play button, progress, current song
        public Color Text = Color.FromArgb(240, 236, 248);
        public Color SubText = Color.FromArgb(160, 152, 184);
        public Color Lyric = Color.FromArgb(150, 236, 160);   // current lyrics line

        public const string Version = "2.0";

        public static readonly Theme[] All =
        {
            new Theme { Name = "Midnight purple", BgTop = Rgb(0x2a1f4a), BgBottom = Rgb(0x120c22), Sidebar = Rgb(0x110b1f), Card = Rgb(0x1d1533), CardHover = Rgb(0x2c2250), Field = Rgb(0x2a2045), Accent = Rgb(0xf08a3c) },
            new Theme { Name = "Ocean blue",      BgTop = Rgb(0x1b4f86), BgBottom = Rgb(0x0b1f3a), Sidebar = Rgb(0x0a1a30), Card = Rgb(0x112a4b), CardHover = Rgb(0x1b3d66), Field = Rgb(0x1a3658), Accent = Rgb(0xff9a3d), SubText = Rgb(0xa3b6cf) },
            new Theme { Name = "Graphite",        BgTop = Rgb(0x34363c), BgBottom = Rgb(0x16171a), Sidebar = Rgb(0x121315), Card = Rgb(0x1f2024), CardHover = Rgb(0x2e3036), Field = Rgb(0x2b2c31), Accent = Rgb(0x4fc3f7), SubText = Rgb(0xa2a5ad) },
            new Theme { Name = "Bordeaux",        BgTop = Rgb(0x5a1a2e), BgBottom = Rgb(0x1e0910), Sidebar = Rgb(0x1a070d), Card = Rgb(0x2c0f19), CardHover = Rgb(0x441828), Field = Rgb(0x3b1523), Accent = Rgb(0xffb547), SubText = Rgb(0xc7a2ad) },
            new Theme { Name = "Emerald",         BgTop = Rgb(0x0f5a4a), BgBottom = Rgb(0x062019), Sidebar = Rgb(0x051a14), Card = Rgb(0x0b2e25), CardHover = Rgb(0x124538), Field = Rgb(0x103a2f), Accent = Rgb(0xf5c542), SubText = Rgb(0x9cc4b8) },
            new Theme { Name = "Sunset",          BgTop = Rgb(0x8a3b4f), BgBottom = Rgb(0x2a1238), Sidebar = Rgb(0x1f0d29), Card = Rgb(0x34173f), CardHover = Rgb(0x4d2257), Field = Rgb(0x43204f), Accent = Rgb(0xff8a5c), SubText = Rgb(0xd0a9c0) },
            new Theme { Name = "Deep teal",       BgTop = Rgb(0x14636e), BgBottom = Rgb(0x0a2329), Sidebar = Rgb(0x071c21), Card = Rgb(0x0f3239), CardHover = Rgb(0x174852), Field = Rgb(0x143f47), Accent = Rgb(0xff7a7a), SubText = Rgb(0x9fc3c8) },
            new Theme { Name = "Black",           BgTop = Rgb(0x141414), BgBottom = Rgb(0x050505), Sidebar = Rgb(0x000000), Card = Rgb(0x111111), CardHover = Rgb(0x222222), Field = Rgb(0x1e1e1e), Accent = Rgb(0x1ed760), SubText = Rgb(0x9a9a9a) },
        };

        public static Theme Current { get; private set; } = All[0];

        private static readonly string SettingsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "theme.txt");

        public static void Load()
        {
            try
            {
                if (File.Exists(SettingsPath))
                {
                    string name = File.ReadAllText(SettingsPath).Trim();
                    Current = All.FirstOrDefault(t => t.Name == name) ?? All[0];
                }
            }
            catch { Current = All[0]; }
        }

        public static void Select(Theme t)
        {
            Current = t;
            try { File.WriteAllText(SettingsPath, t.Name); } catch { /* not important */ }
        }

        private static Color Rgb(int rgb) => Color.FromArgb((rgb >> 16) & 255, (rgb >> 8) & 255, rgb & 255);

        public static Color Mix(Color a, Color b, float t) =>
            Color.FromArgb((int)(a.R + (b.R - a.R) * t), (int)(a.G + (b.G - a.G) * t), (int)(a.B + (b.B - a.B) * t));
    }
}
