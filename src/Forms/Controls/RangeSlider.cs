using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using VPT.Core;

namespace VPT.Forms.Controls
{
    public class RangeSlider : Control
    {
        public double Min { get; set; } = 0;
        public double Max { get; set; } = 100;
        public double SelectedMin { get; private set; } = 0;
        public double SelectedMax { get; private set; } = 100;

        public event EventHandler? SelectionChanged;

        private bool _draggingMin = false;
        private bool _draggingMax = false;
        private bool _draggingRange = false;
        private float _dragStartX = 0;

        public RangeSlider()
        {
            this.SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
            this.Height = 30;
            this.Cursor = Cursors.Hand;
        }

        public void SetRange(double min, double max)
        {
            Min = min;
            Max = max;
            SelectedMin = min;
            SelectedMax = max;
            Invalidate();
        }

        public void SetSelection(double min, double max)
        {
            SelectedMin = Math.Max(Min, Math.Min(max, min));
            SelectedMax = Math.Min(Max, Math.Max(min, max));
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // Background track
            int trackY = Height / 2 - 2;
            int trackH = 4;
            using (var brush = new SolidBrush(Color.FromArgb(45, 52, 68)))
            {
                g.FillRectangle(brush, 0, trackY, Width, trackH);
            }

            // Selected range
            float scale = (float)(Width / (Max - Min));
            float x1 = (float)((SelectedMin - Min) * scale);
            float x2 = (float)((SelectedMax - Min) * scale);

            using (var brush = new SolidBrush(Color.FromArgb(80, 130, 190)))
            {
                g.FillRectangle(brush, x1, trackY, x2 - x1, trackH);
            }

            // Thumbs
            DrawThumb(g, x1, trackY + 2);
            DrawThumb(g, x2, trackY + 2);
        }

        private void DrawThumb(Graphics g, float x, int y)
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
            float scale = (float)(Width / (Max - Min));
            float x1 = (float)((SelectedMin - Min) * scale);
            float x2 = (float)((SelectedMax - Min) * scale);
            int thumbRadius = 10;

            if (Math.Abs(e.X - x1) < thumbRadius) _draggingMin = true;
            else if (Math.Abs(e.X - x2) < thumbRadius) _draggingMax = true;
            else if (e.X > x1 && e.X < x2)
            {
                _draggingRange = true;
                _dragStartX = e.X;
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (!_draggingMin && !_draggingMax && !_draggingRange) return;

            double range = Max - Min;
            double val = Min + (e.X / (double)Width) * range;

            if (_draggingMin)
            {
                SelectedMin = Math.Max(Min, Math.Min(SelectedMax, val));
            }
            else if (_draggingMax)
            {
                SelectedMax = Math.Min(Max, Math.Max(SelectedMin, val));
            }
            else if (_draggingRange)
            {
                double diff = (e.X - _dragStartX) / (double)Width * range;
                if (SelectedMin + diff >= Min && SelectedMax + diff <= Max)
                {
                    SelectedMin += diff;
                    SelectedMax += diff;
                    _dragStartX = e.X;
                }
            }

            SelectionChanged?.Invoke(this, EventArgs.Empty);
            Invalidate();
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            _draggingMin = false;
            _draggingMax = false;
            _draggingRange = false;
        }
    }
}
