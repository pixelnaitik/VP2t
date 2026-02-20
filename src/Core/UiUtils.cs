using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace VPT.Core
{
    public static class UiUtils
    {
        public static void ApplyRounded(Control c, int radius)
        {
            if (c.Width <= 0 || c.Height <= 0) return;
            using var path = new GraphicsPath();
            int r = radius * 2;
            Rectangle rect = new Rectangle(0, 0, c.Width, c.Height);
            path.AddArc(rect.X, rect.Y, r, r, 180, 90);
            path.AddArc(rect.Right - r, rect.Y, r, r, 270, 90);
            path.AddArc(rect.Right - r, rect.Bottom - r, r, r, 0, 90);
            path.AddArc(rect.X, rect.Bottom - r, r, r, 90, 90);
            path.CloseFigure();
            c.Region = new Region(path);
        }

        public static string FormatDuration(double seconds)
        {
            if (seconds <= 0) return "0:00";
            int hrs = (int)(seconds / 3600);
            int mins = (int)((seconds % 3600) / 60);
            int secs = (int)(seconds % 60);
            return hrs > 0 ? $"{hrs}:{mins:D2}:{secs:D2}" : $"{mins}:{secs:D2}";
        }
    }
}
