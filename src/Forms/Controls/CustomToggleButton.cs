using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using VPT.Core;

namespace VPT.Forms.Controls
{
    public class CustomToggleButton : Button
    {
        public bool IsFirst { get; set; } = false;
        public bool IsLast { get; set; } = false;
        public bool IsGrouped { get; set; } = false;

        private bool _isChecked = false;
        public bool IsChecked
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

        public CustomToggleButton()
        {
            this.SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
            this.FlatStyle = FlatStyle.Flat;
            this.FlatAppearance.BorderSize = 0;
            this.BackColor = Color.Transparent;
            this.Cursor = Cursors.Hand;
            this.Size = new Size(60, 30);
        }

        protected override void OnPaint(PaintEventArgs pevent)
        {
            var g = pevent.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            // ClearTypeGridFit has some inherent padding, use AntiAliasGridFit or SystemDefault for tighter bounds
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            // Make the rect slightly smaller than the full width to prevent drawing into the next button's space
            Rectangle rect = new Rectangle(0, 0, Width - 1, Height - 1);

            // Use StringFormat to accurately center without internal padding
            StringFormat sf = new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center,
                FormatFlags = StringFormatFlags.NoWrap,
                Trimming = StringTrimming.None
            };

            if (IsGrouped)
            {
                // Draw grouped background/borders
                using (var baseBg = new SolidBrush(Theme.CardBg)) // Explicitly clear the background
                {
                    g.FillRectangle(baseBg, pevent.ClipRectangle);
                }

                if (IsChecked)
                {
                    using var bgBrush = new SolidBrush(Theme.Accent); // Light green background
                    g.FillRectangle(bgBrush, rect);
                }

                // Draw text
                using var font = new Font("Segoe UI", 9, IsChecked ? FontStyle.Bold : FontStyle.Regular);
                using var brush = new SolidBrush(IsChecked ? Color.White : Theme.Muted);
                g.DrawString(Text, font, brush, rect, sf);

                // Draw separators (except for the last item)
                if (!IsLast)
                {
                    using var sepPen = new Pen(Color.FromArgb(60, 65, 75), 1f);
                    g.DrawLine(sepPen, Width - 1, 4, Width - 1, Height - 5);
                }

                // Draw outer border (handled by parent in some cases, or draw top/bottom here)
                using var borderPen = new Pen(Color.FromArgb(60, 65, 75), 1f);
                g.DrawLine(borderPen, 0, 0, Width - 1, 0); // Top
                g.DrawLine(borderPen, 0, Height - 1, Width - 1, Height - 1); // Bottom
                if (IsFirst) g.DrawLine(borderPen, 0, 0, 0, Height); // Left edge
                if (IsLast) g.DrawLine(borderPen, Width - 1, 0, Width - 1, Height); // Right edge

                // If checked, draw a subtle highlight border
                if (IsChecked)
                {
                    using var highlightPen = new Pen(Theme.Accent, 1.5f);
                    g.DrawRectangle(highlightPen, 0, 0, Width - 1, Height - 1);
                }
            }
            else
            {
                if (IsChecked)
                {
                    // Standalone Active State
                    using var bgBrush = new SolidBrush(Theme.Accent);
                    using var borderPen = new Pen(Theme.Accent, 1f);
                    g.FillRectangle(bgBrush, rect);
                    g.DrawRectangle(borderPen, rect);

                    using var font = new Font("Segoe UI", 9, FontStyle.Bold);
                    using var brush = new SolidBrush(Color.White);
                    g.DrawString(Text, font, brush, rect, sf);
                }
                else
                {
                    // Standalone Inactive State
                    using var borderPen = new Pen(Color.FromArgb(40, 50, 60), 1f);
                    g.DrawRectangle(borderPen, rect);

                    using var font = new Font("Segoe UI", 9, FontStyle.Regular);
                    using var brush = new SolidBrush(Theme.Muted);
                    g.DrawString(Text, font, brush, rect, sf);
                }
            }
        }

        protected override void OnClick(EventArgs e)
        {
            base.OnClick(e);
            if (!IsGrouped)
            {
                IsChecked = !IsChecked; // Toggle state for standalone buttons
            }
        }
    }
}
