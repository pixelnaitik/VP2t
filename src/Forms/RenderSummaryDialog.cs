using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using VPT.Core;

namespace VPT.Forms
{
    public enum RenderSummaryAction
    {
        Cancel,
        RenderNow,
        AddToQueue
    }

    public sealed class RenderSummaryDialog : Form
    {
        public RenderSummaryAction SelectedAction { get; private set; } = RenderSummaryAction.Cancel;

        public RenderSummaryDialog(string videoCodec, string audioCodec, string sizeEstimate, string duration, string outputPath)
        {
            Text = "Render Summary";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false;
            MaximizeBox = false;
            ShowInTaskbar = false;
            ClientSize = new Size(620, 340);
            BackColor = Theme.Bg;
            ForeColor = Theme.Fg;

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 3,
                ColumnCount = 1,
                Padding = new Padding(16),
                BackColor = Theme.Bg
            };
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            Controls.Add(root);

            root.Controls.Add(new Label
            {
                Text = "Review render settings before starting",
                AutoSize = true,
                Font = new Font("Segoe UI", 12, FontStyle.Bold),
                ForeColor = Theme.Fg,
                Margin = new Padding(0, 0, 0, 10)
            });

            var card = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 5,
                BackColor = Theme.CardBg,
                Padding = new Padding(14)
            };
            card.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
            card.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            for (int i = 0; i < 5; i++) card.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            AddRow(card, 0, "Video codec", videoCodec);
            AddRow(card, 1, "Audio codec", audioCodec);
            AddRow(card, 2, "Est. size", sizeEstimate);
            AddRow(card, 3, "Duration", duration);
            AddRow(card, 4, "Output", outputPath);
            root.Controls.Add(card);

            var outputBox = card.GetControlFromPosition(1, 4);
            if (outputBox is Label pathLabel)
            {
                pathLabel.MaximumSize = new Size(420, 0);
                pathLabel.AutoEllipsis = true;
                var tip = new ToolTip();
                tip.SetToolTip(pathLabel, outputPath);
            }

            var buttonPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                AutoSize = true,
                Margin = new Padding(0, 12, 0, 0)
            };

            buttonPanel.Controls.Add(CreateButton("Cancel", Theme.CardBgHover, () =>
            {
                SelectedAction = RenderSummaryAction.Cancel;
                DialogResult = DialogResult.Cancel;
                Close();
            }));

            buttonPanel.Controls.Add(CreateButton("Add to Queue", Theme.CardBg, () =>
            {
                SelectedAction = RenderSummaryAction.AddToQueue;
                DialogResult = DialogResult.OK;
                Close();
            }));

            buttonPanel.Controls.Add(CreateButton("Render Now", Theme.Accent, () =>
            {
                SelectedAction = RenderSummaryAction.RenderNow;
                DialogResult = DialogResult.OK;
                Close();
            }, Color.White));

            root.Controls.Add(buttonPanel);
        }

        private static void AddRow(TableLayoutPanel table, int row, string label, string value)
        {
            table.Controls.Add(new Label
            {
                Text = label,
                AutoSize = true,
                ForeColor = Theme.Muted,
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                Margin = new Padding(0, 4, 8, 8)
            }, 0, row);

            table.Controls.Add(new Label
            {
                Text = value,
                AutoSize = true,
                ForeColor = Theme.Fg,
                Font = new Font("Segoe UI", 9),
                Margin = new Padding(0, 4, 0, 8)
            }, 1, row);
        }

        private static Button CreateButton(string text, Color color, Action onClick, Color? foreColor = null)
        {
            var b = new Button
            {
                Text = text,
                AutoSize = true,
                Padding = new Padding(14, 8, 14, 8),
                BackColor = color,
                ForeColor = foreColor ?? Theme.Fg,
                FlatStyle = FlatStyle.Flat,
                Margin = new Padding(8, 0, 0, 0),
                Cursor = Cursors.Hand
            };
            b.FlatAppearance.BorderSize = 0;
            b.Click += (s, e) => onClick();
            return b;
        }
    }
}
