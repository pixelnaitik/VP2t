using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using VPT.Core;

namespace VPT.Forms.Controls
{
    public class CustomSlider : Control
    {
        public int Min { get; set; } = 0;
        public int Max { get; set; } = 100;

        private int _value = 0;
        public int Value
        {
            get => _value;
            set
            {
                if (value < Min) _value = Min;
                else if (value > Max) _value = Max;
                else _value = value;
                Invalidate();
            }
        }

        public event EventHandler? ValueChanged;

        private bool _isDragging = false;

        public CustomSlider()
        {
            this.SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
            this.Height = 30;
            this.Cursor = Cursors.Hand;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // Track (Thin white line)
            int trackY = Height / 2;
            int trackH = 3;
            using (var brush = new SolidBrush(Color.White))
            {
                g.FillRectangle(brush, 0, trackY - (trackH / 2), Width, trackH);
            }

            // Calculate specific X position for thumb
            float scale = (Max == Min) ? 0 : (float)Width / (Max - Min);
            float thumbX = (Value - Min) * scale;

            // Keep thumb fully within bounds
            if (thumbX < 6) thumbX = 6;
            if (thumbX > Width - 6) thumbX = Width - 6;

            DrawPointerThumb(g, thumbX, trackY);
        }

        private void DrawPointerThumb(Graphics g, float x, int y)
        {
            // Pointy thumb similar to user screenshot (blue rectangle + triangle)
            int w = 12; // Thumb width
            int h = 20; // Total height
            int rectH = 14; // Height of the rectangular part

            PointF[] points = {
                new PointF(x - w / 2, y - h / 2),              // Top Left
                new PointF(x + w / 2, y - h / 2),              // Top Right
                new PointF(x + w / 2, y - h / 2 + rectH),      // Bottom Right of rectangle
                new PointF(x, y + h / 2),                      // Bottom Point (Triangle tip)
                new PointF(x - w / 2, y - h / 2 + rectH)       // Bottom Left of rectangle
            };

            using var brush = new SolidBrush(Color.FromArgb(0, 120, 215)); // Vibrant blue from screenshot
            g.FillPolygon(brush, points);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left)
            {
                _isDragging = true;
                UpdateValueFromMouse(e.X);
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (_isDragging)
            {
                UpdateValueFromMouse(e.X);
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            _isDragging = false;
        }

        private void UpdateValueFromMouse(int mouseX)
        {
            if (Width <= 0) return;

            float percent = (float)mouseX / Width;
            if (percent < 0) percent = 0;
            if (percent > 1) percent = 1;

            int newValue = (int)Math.Round(Min + percent * (Max - Min));
            if (newValue != Value)
            {
                Value = newValue;
                ValueChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }
}
