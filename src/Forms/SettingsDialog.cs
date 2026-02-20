using System;
using System.Drawing;
using System.Windows.Forms;
using VPT.Core;

namespace VPT.Forms
{
    public class SettingsDialog : Form
    {
        public SettingsDialog()
        {
            this.Text = "Settings";
            this.Size = new Size(400, 300);
            this.BackColor = Theme.Bg;
            this.ForeColor = Theme.Fg;
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;

            var label = new Label
            {
                Text = "No settings available yet.",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = Theme.Muted
            };
            this.Controls.Add(label);
        }
    }
}
