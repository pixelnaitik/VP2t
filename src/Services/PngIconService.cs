using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection;

namespace VPT.Services
{
    // ===================== PNG Icon Service (with DISK FALLBACK) =============
    public static class PngIconService
    {
        // --- PATHS FOR PNG ICONS --------------------------------------------
        public static readonly string[] IconSearchDirs = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "Assets"),
        };

        private static readonly Dictionary<string, Bitmap> OriginalCache = new();

        private static string? FindResource(string fileName)
        {
            var asm = Assembly.GetExecutingAssembly();
            return asm.GetManifestResourceNames()
                      .FirstOrDefault(n => n.EndsWith($".Assets.{fileName}", StringComparison.OrdinalIgnoreCase));
        }

        public static Bitmap Render(Graphics g, Rectangle destRect, string fileName, bool active)
        {
            // Overload for direct graphics drawing
            var bmp = Render(fileName, active ? Color.FromArgb(56, 189, 126) : Color.FromArgb(140, 150, 170), destRect.Width, destRect.Height);
            g.DrawImage(bmp, destRect);
            return bmp;
        }

        public static Bitmap Render(string fileName, Color tint, int width, int height, int padding = 8)
        {
            if (!OriginalCache.TryGetValue(fileName, out var original))
            {
                // 1) Try EMBEDDED resource
                var asm = Assembly.GetExecutingAssembly();
                string? res = FindResource(fileName);
                if (res != null)
                {
                    using var s = asm.GetManifestResourceStream(res)!;
                    original = new Bitmap(s);
                }
                else
                {
                    // 2) Try DISK fallback (VPT\Assets)
                    string? onDisk = IconSearchDirs
                        .Where(Directory.Exists)
                        .Select(dir => Path.Combine(dir, fileName))
                        .FirstOrDefault(File.Exists);

                    original = onDisk != null
                        ? new Bitmap(onDisk)
                        : new Bitmap(Math.Max(1, width), Math.Max(1, height)); // last ditch blank
                }

                OriginalCache[fileName] = original;
            }

            var bmp = new Bitmap(width, height);
            using (var g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.Transparent);
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;

                float availW = Math.Max(1, width - 2f * padding);
                float availH = Math.Max(1, height - 2f * padding);
                float scale = Math.Min(availW / original.Width, availH / original.Height);

                float drawW = original.Width * scale;
                float drawH = original.Height * scale;
                float x = (width - drawW) / 2f;
                float y = (height - drawH) / 2f;

                using var ia = MakeTintAttributes(tint);
                var dest = new RectangleF(x, y, drawW, drawH);
                g.DrawImage(original, Rectangle.Round(dest), 0, 0, original.Width, original.Height, GraphicsUnit.Pixel, ia);
            }
            return bmp;
        }

        private static ImageAttributes MakeTintAttributes(Color tint)
        {
            float r = tint.R / 255f, g = tint.G / 255f, b = tint.B / 255f;
            var matrix = new ColorMatrix(new float[][]
            {
                new float[] { r, 0, 0, 0, 0 },
                new float[] { 0, g, 0, 0, 0 },
                new float[] { 0, 0, b, 0, 0 },
                new float[] { 0, 0, 0, 1, 0 },
                new float[] { 0, 0, 0, 0, 0 }
            });
            var ia = new ImageAttributes();
            ia.SetColorMatrix(matrix, ColorMatrixFlag.Default, ColorAdjustType.Bitmap);
            return ia;
        }
    }
}
