using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using VPT.Core;

namespace VPT.Forms.Controls
{
    public class CustomCheckBox : Control
    {
        private bool _isChecked = true;
        public bool Checked
        {
            get => _isChecked;
            set
            {
                if (_isChecked != value)
                {
                    _isChecked = value;
                    CheckedChanged?.Invoke(this, EventArgs.Empty);
                    Invalidate();
                }
            }
        }

        public event EventHandler? CheckedChanged;

        public CustomCheckBox()
        {
            this.SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
            this.Size = new Size(150, 24);
            this.Cursor = Cursors.Hand;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            int boxSize = 16;
            int boxY = (Height - boxSize) / 2;
            Rectangle boxRect = new Rectangle(0, boxY, boxSize, boxSize);

            if (Checked)
            {
                // Green filled box
                using var bg = new SolidBrush(Theme.Accent);
                using var path = GetRoundedRect(boxRect, 3);
                g.FillPath(bg, path);

                // Checkmark (white)
                using var pen = new Pen(Color.White, 2f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
                g.DrawLine(pen, 3, boxY + 8, 7, boxY + 12);
                g.DrawLine(pen, 7, boxY + 12, 13, boxY + 4);
            }
            else
            {
                // Empty dark box with border
                using var bg = new SolidBrush(Color.FromArgb(20, 24, 32));
                using var pen = new Pen(Theme.BorderColor, 1.5f);
                using var path = GetRoundedRect(boxRect, 3);
                g.FillPath(bg, path);
                g.DrawPath(pen, path);
            }

            // Text
            Rectangle textRect = new Rectangle(boxSize + 8, 0, Width - boxSize - 8, Height);
            TextRenderer.DrawText(g, Text, new Font("Segoe UI", 9), textRect, Theme.Muted, TextFormatFlags.VerticalCenter | TextFormatFlags.Left);
        }

        private GraphicsPath GetRoundedRect(Rectangle bounds, int radius)
        {
            int diameter = radius * 2;
            Size size = new Size(diameter, diameter);
            Rectangle arc = new Rectangle(bounds.Location, size);
            GraphicsPath path = new GraphicsPath();

            if (radius == 0)
            {
                path.AddRectangle(bounds);
                return path;
            }

            path.AddArc(arc, 180, 90);
            arc.X = bounds.Right - diameter;
            path.AddArc(arc, 270, 90);
            arc.Y = bounds.Bottom - diameter;
            path.AddArc(arc, 0, 90);
            arc.X = bounds.Left;
            path.AddArc(arc, 90, 90);
            path.CloseFigure();
            return path;
        }

        protected override void OnClick(EventArgs e)
        {
            base.OnClick(e);
            Checked = !Checked;
        }
    }
}
