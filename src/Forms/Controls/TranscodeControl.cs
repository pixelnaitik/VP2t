using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using VPT.Core;
using VPT.Services;
using VPT.Forms;

namespace VPT.Forms.Controls
{
    public partial class TranscodeControl : UserControl
    {
        private CustomComboBox transcodeFormatCombo = null!;
        private CustomComboBox transcodeQualityCombo = null!;
        private Button transcodeBtn = null!;
        private Label transcodeFileLabel = null!;
        private string? _transcodeInputFile;
        private readonly VideoProcessingService _videoService;
        private readonly Form _parentForm;

        public event Action<string>? VideoLoaded;

        // Preview UI
        private PictureBox transcodePreviewBox = null!;
        private Label transcodeDropOverlay = null!;

        public TranscodeControl(Form parentForm, VideoProcessingService videoService)
        {
            _parentForm = parentForm;
            _videoService = videoService;

            this.Dock = DockStyle.Fill;
            this.BackColor = Theme.Bg;
            this.ForeColor = Theme.Fg;

            InitializeComponent();
        }

        private void InitializeComponent()
        {
            // Root layout
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, BackColor = Theme.Bg };
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 64f));
            this.Controls.Add(root);

            // Main content
            var content = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, BackColor = Theme.Bg, Padding = new Padding(16) };
            content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55f));
            content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45f));
            root.Controls.Add(content, 0, 0);

            // LEFT: Preview Area
            var previewPanel = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(12, 14, 18), Margin = new Padding(0, 0, 8, 0) };
            content.Controls.Add(previewPanel, 0, 0);

            transcodePreviewBox = new PictureBox
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(18, 20, 26),
                SizeMode = PictureBoxSizeMode.Zoom
            };
            previewPanel.Controls.Add(transcodePreviewBox);

            transcodeDropOverlay = new Label
            {
                Dock = DockStyle.Fill,
                Text = "📂  Drop video here\nor click to browse",
                ForeColor = Theme.Muted,
                BackColor = Color.Transparent,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 14),
                Cursor = Cursors.Hand
            };
            transcodePreviewBox.Controls.Add(transcodeDropOverlay);

            // Handlers
            Action selectTranscodeAction = () =>
            {
                var ofd = new OpenFileDialog
                {
                    Title = "Select video to convert",
                    Filter = "Video Files|*.mp4;*.mov;*.avi;*.mkv;*.webm;*.wmv|All Files|*.*"
                };
                if (ofd.ShowDialog(this) == DialogResult.OK)
                {
                    LoadVideo(ofd.FileName);
                }
            };
            transcodePreviewBox.Click += (s, e) => selectTranscodeAction();
            transcodeDropOverlay.Click += (s, e) => selectTranscodeAction();
            previewPanel.AllowDrop = true;
            previewPanel.DragEnter += (s, e) => { if (e.Data?.GetDataPresent(DataFormats.FileDrop) == true) e.Effect = DragDropEffects.Copy; };
            previewPanel.DragDrop += (s, e) =>
            {
                if (e.Data?.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0)
                {
                    LoadVideo(files[0]);
                }
            };

            // ...



            // RIGHT: Controls Panel
            var controlsPanel = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Bg, Margin = new Padding(8, 0, 0, 0), Padding = new Padding(16) };
            content.Controls.Add(controlsPanel, 1, 0);

            // Title
            var title = new Label
            {
                Text = "🔄  Convert Format",
                Font = new Font("Segoe UI", 16, FontStyle.Bold),
                ForeColor = Theme.Fg,
                AutoSize = true,
                Location = new Point(0, 0)
            };
            controlsPanel.Controls.Add(title);

            // Description
            var desc = new Label
            {
                Text = "Convert your video to different formats\nwith customizable quality settings.",
                Font = new Font("Segoe UI", 10),
                ForeColor = Theme.Muted,
                AutoSize = true,
                Location = new Point(0, 35)
            };
            controlsPanel.Controls.Add(desc);

            // File info
            transcodeFileLabel = new Label
            {
                Text = "No file selected",
                Location = new Point(0, 90),
                Size = new Size(300, 25),
                ForeColor = Theme.Accent,
                Font = new Font("Segoe UI", 10, FontStyle.Bold)
            };
            controlsPanel.Controls.Add(transcodeFileLabel);

            // Card for settings
            var settingsCard = new Panel
            {
                Location = new Point(0, 130),
                Size = new Size(350, 235),
                BackColor = Theme.CardBg,
                Padding = new Padding(16)
            };
            controlsPanel.Controls.Add(settingsCard);

            // Format descriptions dictionary
            var formatDescriptions = new Dictionary<string, string>
            {
                { "MP4 (H.264)", "Most compatible format. Works everywhere." },
                { "MKV (Matroska)", "High quality container for archiving." },
                { "AVI (Legacy)", "Older Windows format, widely supported." },
                { "MOV (Apple)", "Best for Mac/iOS and Final Cut Pro." },
                { "WMV (Windows)", "Windows Media format, good compression." },
                { "FLV (Flash)", "Legacy web format, small file size." },
                { "WebM (VP9)", "Open format, great for modern browsers." },
                { "MPEG/MPG", "DVD-compatible, universal playback." },
                { "3GP (Mobile)", "Mobile format for older phones." },
                { "AVCHD", "Camcorder format, high quality HD." }
            };

            // Consistent spacing constants
            const int margin = 16;
            const int rowHeight = 24;
            int y = margin;

            // Output Format Label
            var formatLabel = new Label
            {
                Text = "Output Format",
                Location = new Point(margin, y),
                AutoSize = true,
                ForeColor = Theme.Muted,
                Font = new Font("Segoe UI", 9)
            };
            settingsCard.Controls.Add(formatLabel);
            y += rowHeight;

            // Format ComboBox
            transcodeFormatCombo = new CustomComboBox
            {
                Location = new Point(margin, y),
                Width = 200,
                Font = new Font("Segoe UI", 10)
            };
            transcodeFormatCombo.Items.AddRange(new[] {
                "MP4 (H.264)", "MKV (Matroska)", "AVI (Legacy)", "MOV (Apple)", "WMV (Windows)",
                "FLV (Flash)", "WebM (VP9)", "MPEG/MPG", "3GP (Mobile)", "AVCHD"
            });

            string defaultFormat = SettingsService.Load("DefaultTranscodeFormat", "MP4 (H.264)");
            int defaultIdx = transcodeFormatCombo.Items.Cast<string>().ToList().FindIndex(f => f == defaultFormat);
            transcodeFormatCombo.SelectedIndex = defaultIdx >= 0 ? defaultIdx : 0;
            settingsCard.Controls.Add(transcodeFormatCombo);
            y += 28;

            // Format description
            var formatDescLabel = new Label
            {
                Location = new Point(margin, y),
                Size = new Size(300, 20),
                ForeColor = Theme.Accent,
                Font = new Font("Segoe UI", 9),
                Text = formatDescriptions.GetValueOrDefault(transcodeFormatCombo.SelectedItem?.ToString() ?? "", "")
            };
            settingsCard.Controls.Add(formatDescLabel);
            y += rowHeight;

            transcodeFormatCombo.SelectedIndexChanged += (s, e) =>
            {
                string selected = transcodeFormatCombo.SelectedItem?.ToString() ?? "";
                formatDescLabel.Text = formatDescriptions.GetValueOrDefault(selected, "");
            };

            // Set as default checkbox
            var defaultCheckbox = new CustomCheckBox
            {
                Text = "Set as default format",
                Location = new Point(margin - 4, y),
                Width = 200
            };
            defaultCheckbox.CheckedChanged += (s, e) =>
            {
                if (defaultCheckbox.Checked)
                    SettingsService.Save("DefaultTranscodeFormat", transcodeFormatCombo.SelectedItem?.ToString() ?? "MP4 (H.264)");
            };
            settingsCard.Controls.Add(defaultCheckbox);
            y += rowHeight + 8;

            // Quality Preset Label
            var qualityLabel = new Label
            {
                Text = "Quality Preset",
                Location = new Point(margin, y),
                AutoSize = true,
                ForeColor = Theme.Muted,
                Font = new Font("Segoe UI", 9)
            };
            settingsCard.Controls.Add(qualityLabel);
            y += rowHeight;

            // Quality ComboBox
            transcodeQualityCombo = new CustomComboBox
            {
                Location = new Point(margin, y),
                Width = 200,
                Font = new Font("Segoe UI", 10)
            };
            transcodeQualityCombo.Items.AddRange(new[] { "Web Optimized", "Social Media", "High Quality", "Maximum" });
            transcodeQualityCombo.SelectedIndex = 1;
            settingsCard.Controls.Add(transcodeQualityCombo);
            y += 32;

            // Info label
            var infoLabel = new Label
            {
                Text = "Output saved to same folder as source",
                Location = new Point(margin, y),
                AutoSize = true,
                ForeColor = Theme.Muted,
                Font = new Font("Segoe UI", 9)
            };
            settingsCard.Controls.Add(infoLabel);

            // Convert button
            transcodeBtn = new Button
            {
                Dock = DockStyle.Fill,
                Text = "  🔄  Convert Video",
                Height = 52,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI Semibold", 12, FontStyle.Bold),
                BackColor = Theme.Accent,
                ForeColor = Color.White,
                Margin = new Padding(16, 8, 16, 12),
                Cursor = Cursors.Hand
            };
            transcodeBtn.FlatAppearance.BorderSize = 0;
            transcodeBtn.FlatAppearance.MouseOverBackColor = Theme.AccentHover;
            transcodeBtn.Click += async (s, e) => await ProcessActionAsync();
            transcodeBtn.Resize += (s, e) => UiUtils.ApplyRounded(transcodeBtn, 12);
            root.Controls.Add(transcodeBtn, 0, 1);
        }

        private void LoadThumbnailAsync(string filePath, PictureBox target)
        {
            Task.Run(() =>
            {
                try
                {
                    string ffmpegPath = _videoService.ExtractFfmpegTool("ffmpeg.exe");
                    string tempThumb = Path.Combine(Path.GetTempPath(), $"vpt_thumb_{Guid.NewGuid()}.jpg");
                    var psi = new ProcessStartInfo
                    {
                        FileName = ffmpegPath,
                        Arguments = $"-y -i \"{filePath}\" -ss 00:00:01 -vframes 1 -q:v 2 \"{tempThumb}\"",
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };
                    using var p = Process.Start(psi);
                    p?.WaitForExit();
                    if (File.Exists(tempThumb))
                    {
                        using var fs = new FileStream(tempThumb, FileMode.Open, FileAccess.Read);
                        var img = Image.FromStream(fs);
                        this.Invoke((Action)(() => { target.Image = img; }));
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error("Failed to load transcode thumbnail", ex);
                }
            });
        }

        private async Task ProcessActionAsync()
        {
            if (string.IsNullOrEmpty(_transcodeInputFile))
            {
                MessageBox.Show("Please select a video file first.", "No File", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string selectedFormat = transcodeFormatCombo.SelectedItem?.ToString() ?? "MP4 (H.264)";

            // Map format name to extension and codecs
            var (ext, vcodec, acodec) = GetFormatDetails(selectedFormat);

            string qualityArgs = transcodeQualityCombo.SelectedIndex switch
            {
                0 => "-b:v 2M -b:a 128k",
                1 => "-b:v 5M -b:a 192k",
                2 => "-b:v 10M -b:a 320k",
                _ => "-crf 18"
            };

            var options = new VideoProcessingOptions
            {
                InputPath = _transcodeInputFile,
                Quality = selectedFormat,
                OutputExtension = ext,
                VideoCodec = vcodec,
                AudioCodec = acodec,
                CustomArgs = qualityArgs
            };

            using var progressDlg = new RenderProgressDialog(_parentForm);
            progressDlg.Show(_parentForm);

            bool success = await _videoService.ProcessVideoAsync(options, progressDlg);

            progressDlg.Close();

            // We don't need a massive success box for every file unless desired, but it helps here.
            if (success)
            {
                MessageBox.Show("Conversion successful!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private (string ext, string vcodec, string acodec) GetFormatDetails(string format)
        {
            return format switch
            {
                "MP4 (H.264)" => ("mp4", "libx264", "aac"),
                "MKV (Matroska)" => ("mkv", "libx264", "aac"),
                "AVI (Legacy)" => ("avi", "mpeg4", "mp3"),
                "MOV (Apple)" => ("mov", "libx264", "aac"),
                "WMV (Windows)" => ("wmv", "wmv2", "wmav2"),
                "FLV (Flash)" => ("flv", "flv1", "mp3"),
                "WebM (VP9)" => ("webm", "libvpx-vp9", "libopus"),
                "MPEG/MPG" => ("mpg", "mpeg2video", "mp2"),
                "3GP (Mobile)" => ("3gp", "h263", "aac"),
                "AVCHD" => ("mts", "libx264", "ac3"),
                _ => ("mp4", "libx264", "aac")
            };
        }
        public void LoadVideo(string filePath, bool silent = false)
        {
            _transcodeInputFile = filePath;
            if (!silent) VideoLoaded?.Invoke(filePath);
            if (this.InvokeRequired)
            {
                this.Invoke((Action)(() =>
                {
                    transcodeDropOverlay.Visible = false;
                    transcodeFileLabel.Text = $"📄 {Path.GetFileName(filePath)}";
                }));
            }
            else
            {
                transcodeDropOverlay.Visible = false;
                transcodeFileLabel.Text = $"📄 {Path.GetFileName(filePath)}";
            }
            LoadThumbnailAsync(filePath, transcodePreviewBox);
        }
    }
}
