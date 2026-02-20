using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using VPT.Core;

namespace VPT.Forms.Controls
{
    public class CustomComboBox : ComboBox
    {
        private const int WM_PAINT = 0x000F;
        private int _buttonWidth = SystemInformation.VerticalScrollBarWidth;

        public CustomComboBox()
        {
            SetStyle(ControlStyles.UserPaint, false);
            DrawMode = DrawMode.OwnerDrawFixed;
            DropDownStyle = ComboBoxStyle.DropDownList;
            BackColor = Color.FromArgb(45, 52, 68);
            ForeColor = Theme.Fg;
            FlatStyle = FlatStyle.Flat;
        }

        protected override void OnDrawItem(DrawItemEventArgs e)
        {
            if (e.Index < 0) return;

            e.DrawBackground();
            bool selected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;

            using var bgBrush = new SolidBrush(selected ? Theme.Accent : BackColor);
            e.Graphics.FillRectangle(bgBrush, e.Bounds);

            using var textBrush = new SolidBrush(selected ? Color.White : ForeColor);
            e.Graphics.DrawString(Items[e.Index]?.ToString() ?? "", e.Font ?? Font, textBrush, e.Bounds, StringFormat.GenericDefault);
            e.DrawFocusRectangle();
        }

        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
            if (m.Msg == WM_PAINT)
            {
                using var g = Graphics.FromHwnd(Handle);

                // Draw drop down button area (overriding the white native draw)
                Rectangle btnRect = new Rectangle(Width - _buttonWidth, 0, _buttonWidth, Height);
                using var btnBrush = new SolidBrush(BackColor);
                g.FillRectangle(btnBrush, btnRect);

                // Draw border
                using var borderPen = new Pen(Theme.BorderColor, 1);
                g.DrawRectangle(borderPen, 0, 0, Width - 1, Height - 1);
                g.DrawLine(borderPen, Width - _buttonWidth, 0, Width - _buttonWidth, Height); // vertical separator

                // Draw arrow
                int arrowW = 8;
                int arrowH = 4;
                int arrowX = btnRect.X + (btnRect.Width - arrowW) / 2;
                int arrowY = btnRect.Y + (btnRect.Height - arrowH) / 2;
                using var arrowBrush = new SolidBrush(Theme.Muted);
                Point[] arrowPoints = {
                    new Point(arrowX, arrowY),
                    new Point(arrowX + arrowW, arrowY),
                    new Point(arrowX + arrowW / 2, arrowY + arrowH)
                };
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.FillPolygon(arrowBrush, arrowPoints);

                // Re-draw text in main area to cover native rendering glitches
                Rectangle textRect = new Rectangle(Color.Transparent.IsEmpty ? 2 : 2, 0, Width - _buttonWidth - 4, Height);
                using var bgWiper = new SolidBrush(BackColor);
                g.FillRectangle(bgWiper, 1, 1, Width - _buttonWidth - 1, Height - 2);

                if (SelectedIndex >= 0)
                {
                    g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
                    TextRenderer.DrawText(g, SelectedItem?.ToString() ?? "", Font, textRect, ForeColor, TextFormatFlags.VerticalCenter | TextFormatFlags.Left);
                }
            }
        }
    }
}
