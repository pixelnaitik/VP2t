using System;
using System.Drawing;
using System.Windows.Forms;

namespace VPT.Forms
{
    /// <summary>
    /// Dark-theme dialog for selecting render quality/resolution.
    /// Provides resolution options from 240p to 4K.
    /// </summary>
    public class RenderQualityDialog : Form
    {
        private readonly ComboBox qualityCombo;
        private readonly Button renderButton;
        private readonly Button cancelButton;

        /// <summary>
        /// Returns the selected quality option.
        /// </summary>
        public string SelectedQuality => qualityCombo.SelectedItem?.ToString() ?? "Original";

        /// <summary>
        /// Returns the FFmpeg scale filter string for the selected quality.
        /// Returns empty string for "Original" (no scaling).
        /// </summary>
        public string GetScaleFilter()
        {
            return SelectedQuality switch
            {
                "240p" => "scale=426:240:force_original_aspect_ratio=decrease,pad=426:240:(ow-iw)/2:(oh-ih)/2",
                "360p" => "scale=640:360:force_original_aspect_ratio=decrease,pad=640:360:(ow-iw)/2:(oh-ih)/2",
                "480p" => "scale=854:480:force_original_aspect_ratio=decrease,pad=854:480:(ow-iw)/2:(oh-ih)/2",
                "720p HD" => "scale=1280:720:force_original_aspect_ratio=decrease,pad=1280:720:(ow-iw)/2:(oh-ih)/2",
                "1080p HD" => "scale=1920:1080:force_original_aspect_ratio=decrease,pad=1920:1080:(ow-iw)/2:(oh-ih)/2",
                "1440p HD" => "scale=2560:1440:force_original_aspect_ratio=decrease,pad=2560:1440:(ow-iw)/2:(oh-ih)/2",
                "2160p 4K" => "scale=3840:2160:force_original_aspect_ratio=decrease,pad=3840:2160:(ow-iw)/2:(oh-ih)/2",
                _ => ""
            };
        }

        /// <summary>
        /// Parameterless constructor for designer/tests.
        /// </summary>
        public RenderQualityDialog() : this(null) { }

        /// <summary>
        /// Main constructor used by Form1.
        /// </summary>
        public RenderQualityDialog(Form? parent)
        {
            // Window setup (dark theme)
            Text = "Select Render Quality";
            StartPosition = parent is null ? FormStartPosition.CenterScreen : FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(380, 250);

            BackColor = Color.FromArgb(22, 26, 35);
            ForeColor = Color.FromArgb(240, 242, 248);

            if (parent != null) Owner = parent;

            // Title Label
            var titleLabel = new Label
            {
                AutoSize = true,
                Text = "Select output resolution:",
                Location = new Point(20, 20),
                Font = new Font("Segoe UI", 11, FontStyle.Bold)
            };
            Controls.Add(titleLabel);

            // Quality ComboBox
            qualityCombo = new ComboBox
            {
                Location = new Point(20, 55),
                Width = ClientSize.Width - 40,
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.FromArgb(32, 38, 52),
                ForeColor = Color.FromArgb(240, 242, 248),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10)
            };

            // Add quality options
            qualityCombo.Items.AddRange(new object[]
            {
                "Original",
                "240p",
                "360p",
                "480p",
                "720p HD",
                "1080p HD",
                "1440p HD",
                "2160p 4K"
            });
            qualityCombo.SelectedIndex = 4; // Default to 1080p HD
            Controls.Add(qualityCombo);

            // Description label
            var descLabel = new Label
            {
                AutoSize = false,
                Text = "Higher resolutions produce larger files.\n'Original' keeps the source resolution.",
                Location = new Point(20, 95),
                Size = new Size(ClientSize.Width - 40, 50),
                ForeColor = Color.FromArgb(140, 150, 170),
                Font = new Font("Segoe UI", 9)
            };
            Controls.Add(descLabel);

            // Cancel button
            cancelButton = new Button
            {
                Text = "Cancel",
                Width = 100,
                Height = 35,
                Location = new Point(ClientSize.Width - 230, 200),
                BackColor = Color.FromArgb(45, 52, 68),
                ForeColor = Color.FromArgb(240, 242, 248),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10),
                Cursor = Cursors.Hand
            };
            cancelButton.FlatAppearance.BorderSize = 1;
            cancelButton.FlatAppearance.BorderColor = Color.FromArgb(60, 70, 90);
            cancelButton.Click += (s, e) =>
            {
                DialogResult = DialogResult.Cancel;
                Close();
            };
            Controls.Add(cancelButton);

            // Render button
            renderButton = new Button
            {
                Text = "Render",
                Width = 100,
                Height = 35,
                Location = new Point(ClientSize.Width - 120, 200),
                BackColor = Color.FromArgb(56, 189, 126),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            renderButton.FlatAppearance.BorderSize = 0;
            renderButton.FlatAppearance.MouseOverBackColor = Color.FromArgb(72, 210, 145);
            renderButton.FlatAppearance.MouseDownBackColor = Color.FromArgb(40, 150, 100);
            renderButton.Click += (s, e) =>
            {
                DialogResult = DialogResult.OK;
                Close();
            };
            Controls.Add(renderButton);

            // Handle resize
            Resize += (s, e) =>
            {
                qualityCombo.Width = ClientSize.Width - 40;
                renderButton.Left = ClientSize.Width - 120;
                cancelButton.Left = ClientSize.Width - 230;
            };
        }
    }
}
