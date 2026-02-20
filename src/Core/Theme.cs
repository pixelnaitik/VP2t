using System.Drawing;

namespace VPT.Core
{
    public static class Theme
    {
        // --- Professional Dark Theme ----------------------------------------
        public static readonly Color Bg = Color.FromArgb(16, 18, 24);           // Deep navy-black
        public static readonly Color PanelBg = Color.FromArgb(22, 26, 35);      // Rich dark panel
        public static readonly Color CardBg = Color.FromArgb(32, 38, 52);       // Blue-tinted cards
        public static readonly Color CardBgHover = Color.FromArgb(45, 52, 72);  // Subtle hover glow
        public static readonly Color CardActive = Color.FromArgb(35, 65, 90);   // Active selection
        public static readonly Color Fg = Color.FromArgb(240, 242, 248);        // Crisp white text
        public static readonly Color Muted = Color.FromArgb(140, 150, 170);     // Soft gray
        public static readonly Color Accent = Color.FromArgb(56, 189, 126);     // Modern teal-green
        public static readonly Color AccentHover = Color.FromArgb(72, 210, 145);// Lighter on hover
        public static readonly Color AccentDark = Color.FromArgb(40, 150, 100); // Darker shade
        public static readonly Color BorderColor = Color.FromArgb(55, 65, 85);  // Subtle borders
        public static readonly Color BorderHover = Color.FromArgb(70, 85, 110); // Hover borders
    }
}
