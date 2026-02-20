using System;
using System.Drawing;
using System.Windows.Forms;

namespace VPT.Controls
{
    // ===================== Dark TabControl (no white gutter) ==================
    internal class DarkTabControl : TabControl
    {
        private readonly Color _strip;
        private readonly Color _bg;
        private readonly Color _fg;
        private readonly Color _border = Color.FromArgb(45, 45, 52);

        public DarkTabControl(Color strip, Color bg, Color fg)
        {
            _strip = strip; _bg = bg; _fg = fg;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
            DrawMode = TabDrawMode.OwnerDrawFixed;
            BackColor = _bg;
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            int stripH = ItemSize.Height + 6;
            using var sb = new SolidBrush(_strip);
            e.Graphics.FillRectangle(sb, new Rectangle(0, 0, Width, stripH));
            using var pb = new SolidBrush(_bg);
            e.Graphics.FillRectangle(pb, new Rectangle(0, stripH, Width, Height - stripH));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            // Enable antialiasing for smooth rounded corners
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            e.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            for (int i = 0; i < TabPages.Count; i++)
            {
                Rectangle rect = GetTabRect(i);

                // Add margins around each tab to create the pill shape gaps
                int marginX = 4;
                int marginY = 4;
                Rectangle pillRect = new Rectangle(
                    rect.X + marginX,
                    rect.Y + marginY,
                    rect.Width - (marginX * 2),
                    rect.Height - (marginY * 2) - 4 // -4 to keep it above the content area
                );

                bool selected = (i == SelectedIndex);

                using var path = CreateRoundedRectanglePath(pillRect, pillRect.Height / 2); // Pill shape -> radius is half height

                if (selected)
                {
                    // Active tab: Semi-transparent Accent background
                    using var bg = new SolidBrush(Color.FromArgb(40, 56, 189, 126)); // Theme.Accent with alpha
                    e.Graphics.FillPath(bg, path);

                    // Optional subtle border for active tab
                    using var pen = new Pen(Color.FromArgb(80, 56, 189, 126), 1);
                    e.Graphics.DrawPath(pen, path);
                }
                else
                {
                    // Inactive tab: Dark background with subtle border
                    using var pen = new Pen(_border, 1.5f);
                    e.Graphics.DrawPath(pen, path);
                }

                // Draw Text centered
                Color textColor = selected ? _fg : Color.FromArgb(170, 180, 200); // Lighter muted color for readability
                Font textFont = new Font("Segoe UI", 10.5f, selected ? FontStyle.Bold : FontStyle.Regular);

                TextRenderer.DrawText(
                    e.Graphics,
                    TabPages[i].Text,
                    textFont,
                    pillRect,
                    textColor,
                    TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter | TextFormatFlags.EndEllipsis);
            }
        }

        private System.Drawing.Drawing2D.GraphicsPath CreateRoundedRectanglePath(Rectangle rect, int radius)
        {
            var path = new System.Drawing.Drawing2D.GraphicsPath();
            int diameter = radius * 2;
            Rectangle arc = new Rectangle(rect.Location, new Size(diameter, diameter));

            // Top left arc
            path.AddArc(arc, 180, 90);

            // Top right arc
            arc.X = rect.Right - diameter;
            path.AddArc(arc, 270, 90);

            // Bottom right arc
            arc.Y = rect.Bottom - diameter;
            path.AddArc(arc, 0, 90);

            // Bottom left arc
            arc.X = rect.Left;
            path.AddArc(arc, 90, 90);

            path.CloseFigure();
            return path;
        }
    }
}
