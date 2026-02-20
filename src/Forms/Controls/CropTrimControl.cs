using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using VPT.Core;
using VPT.Services;
using VPT.Forms;

namespace VPT.Forms.Controls
{
    public partial class CropTrimControl : UserControl
    {
        private TextBox trimStartInput = null!;
        private TextBox trimEndInput = null!;
        private Button trimBtn = null!;
        private Label trimFileLabel = null!;
        private RangeSlider trimSlider = null!;
        private CustomCheckBox maintainAspectCb = null!;

        private string? _trimInputFile;
        private double _videoDuration = 0;
        private int _videoWidth = 0;
        private int _videoHeight = 0;

        // Crop State
        private Rectangle? _cropRect;
        private bool _drawingCrop = false;
        private Point _cropStartPoint;
        private Rectangle _currentDragRect;
        private string _currentAspectRatio = "Original";
        private bool _maintainAspectRatio = true;

        private readonly VideoProcessingService _videoService;
        private readonly Form _parentForm;

        public event Action<string>? VideoLoaded;

        // Preview UI
        private PictureBox trimPreviewBox = null!;
        private Label trimDropOverlay = null!;

        public CropTrimControl(Form parentForm, VideoProcessingService videoService)
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
            // ... existing init ...
            // Root layout
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, BackColor = Theme.Bg };
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 80f));
            this.Controls.Add(root);

            // Main content
            var content = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, BackColor = Theme.Bg, Padding = new Padding(16) };
            content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60f));
            content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40f));
            root.Controls.Add(content, 0, 0);

            // LEFT: Preview Area
            var previewPanel = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(12, 14, 18), Margin = new Padding(0, 0, 8, 0) };
            content.Controls.Add(previewPanel, 0, 0);

            trimPreviewBox = new PictureBox
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(18, 20, 26),
                SizeMode = PictureBoxSizeMode.Zoom,
                Cursor = Cursors.Cross
            };
            trimPreviewBox.Paint += TrimPreviewBox_Paint;
            trimPreviewBox.MouseDown += TrimPreviewBox_MouseDown;
            trimPreviewBox.MouseMove += TrimPreviewBox_MouseMove;
            trimPreviewBox.MouseUp += TrimPreviewBox_MouseUp;
            previewPanel.Controls.Add(trimPreviewBox);

            trimDropOverlay = new Label
            {
                Dock = DockStyle.Fill,
                Text = "📂  Drop video here\nor click to browse",
                ForeColor = Theme.Muted,
                BackColor = Color.Transparent,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 14),
                Cursor = Cursors.Hand
            };
            trimPreviewBox.Controls.Add(trimDropOverlay);

            // Handlers
            Action selectTrimAction = () =>
            {
                var ofd = new OpenFileDialog
                {
                    Title = "Select video to trim/crop",
                    Filter = "Video Files|*.mp4;*.mov;*.avi;*.mkv;*.webm;*.wmv|All Files|*.*"
                };
                if (ofd.ShowDialog(this) == DialogResult.OK)
                {
                    LoadVideo(ofd.FileName);
                }
            };
            trimDropOverlay.Click += (s, e) => selectTrimAction();
            previewPanel.AllowDrop = true;
            previewPanel.DragEnter += (s, e) => { if (e.Data?.GetDataPresent(DataFormats.FileDrop) == true) e.Effect = DragDropEffects.Copy; };
            previewPanel.DragDrop += (s, e) =>
            {
                if (e.Data?.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0)
                {
                    LoadVideo(files[0]);
                }
            };

            // RIGHT: Controls Panel
            var controlsPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Theme.Bg,
                Margin = new Padding(8, 0, 0, 0),
                Padding = new Padding(8),
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoScroll = true
            };
            content.Controls.Add(controlsPanel, 1, 0);

            // Title
            var title = new Label
            {
                Text = "✂️  Trim & Crop",
                Font = new Font("Segoe UI", 16, FontStyle.Bold),
                ForeColor = Theme.Fg,
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 10)
            };
            controlsPanel.Controls.Add(title);

            // Description
            var desc = new Label
            {
                Text = "Drag on preview to Crop.\nUse slider or inputs to Trim.",
                Font = new Font("Segoe UI", 10),
                ForeColor = Theme.Muted,
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 20)
            };
            controlsPanel.Controls.Add(desc);

            // File info
            trimFileLabel = new Label
            {
                Text = "No file selected",
                Size = new Size(300, 25), // Width will be updated on resize
                ForeColor = Theme.Accent,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                Margin = new Padding(0, 0, 0, 10)
            };
            controlsPanel.Controls.Add(trimFileLabel);

            // Handle Resize to stretch controls
            controlsPanel.SizeChanged += (s, e) =>
            {
                int w = controlsPanel.ClientSize.Width - controlsPanel.Padding.Horizontal - 10;
                foreach (Control c in controlsPanel.Controls)
                {
                    if (c is Panel || c is GroupBox) c.Width = w;
                }
            };

            // Card for time inputs
            var timeCard = new Panel
            {
                Size = new Size(320, 242), // Increased height for aspect ratio tools
                BackColor = Theme.CardBg,
                Padding = new Padding(16),
                Margin = new Padding(0, 0, 0, 20)
            };
            UiUtils.ApplyRounded(timeCard, 12);
            controlsPanel.Controls.Add(timeCard);

            // Start time
            var startLabel = new Label { Text = "Start Time", Location = new Point(16, 16), AutoSize = true, ForeColor = Theme.Muted, Font = new Font("Segoe UI", 9) };
            timeCard.Controls.Add(startLabel);
            trimStartInput = new TextBox
            {
                Location = new Point(16, 38),
                Width = 130,
                Height = 32,
                BackColor = Color.FromArgb(45, 52, 68),
                ForeColor = Theme.Fg,
                BorderStyle = BorderStyle.FixedSingle,
                Text = "00:00:00.000",
                Font = new Font("Segoe UI", 10)
            };
            trimStartInput.Leave += (s, e) => UpdateSliderFromInputs();
            timeCard.Controls.Add(trimStartInput);

            // End time
            var endLabel = new Label { Text = "End Time", Location = new Point(160, 16), AutoSize = true, ForeColor = Theme.Muted, Font = new Font("Segoe UI", 9) };
            timeCard.Controls.Add(endLabel);
            trimEndInput = new TextBox
            {
                Location = new Point(160, 38),
                Width = 130,
                Height = 32,
                BackColor = Color.FromArgb(45, 52, 68),
                ForeColor = Theme.Fg,
                BorderStyle = BorderStyle.FixedSingle,
                Text = "00:00:00.000",
                Font = new Font("Segoe UI", 10)
            };
            trimEndInput.Leave += (s, e) => UpdateSliderFromInputs();
            timeCard.Controls.Add(trimEndInput);

            // Reset Crop Button
            var resetCropBtn = new Button
            {
                Text = "Reset Crop",
                Location = new Point(16, 80),
                Width = 110,
                Height = 30,
                FlatStyle = FlatStyle.Flat,
                BackColor = Theme.PanelBg,
                ForeColor = Theme.Muted
            };
            resetCropBtn.FlatAppearance.BorderSize = 0;
            resetCropBtn.Click += (s, e) => { _cropRect = null; trimPreviewBox.Invalidate(); };
            timeCard.Controls.Add(resetCropBtn);

            // Aspect Ratio Group (Original, 16:9, etc)
            var aspectGroup = new FlowLayoutPanel
            {
                Location = new Point(16, 125),
                Width = 288,
                Height = 30,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Margin = new Padding(0),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            timeCard.Controls.Add(aspectGroup);

            string[] aspects = { "Original", "16:9", "9:16", "4:3", "1:1", "Free" };
            CustomToggleButton? currentAspectBtn = null;

            for (int i = 0; i < aspects.Length; i++)
            {
                var aspect = aspects[i];
                var btn = new CustomToggleButton
                {
                    Text = aspect,
                    Width = aspect == "Original" ? 68 : 44,
                    Height = 28,
                    Margin = new Padding(0), // No gaps so borders align
                    IsGrouped = true,
                    IsFirst = i == 0,
                    IsLast = i == aspects.Length - 1
                };

                if (aspect == "Original")
                {
                    btn.IsChecked = true;
                    currentAspectBtn = btn;
                }

                btn.Click += (s, e) =>
                {
                    if (currentAspectBtn != null) currentAspectBtn.IsChecked = false;
                    btn.IsChecked = true;
                    currentAspectBtn = btn;
                    _currentAspectRatio = aspect;

                    // If we already have a crop rect, we might want to resize it or clear it.
                    // For now, let's just clear it to force them to draw a new one with the new ratio.
                    _cropRect = null;
                    trimPreviewBox.Invalidate();
                };
                aspectGroup.Controls.Add(btn);
            }

            // Maintain aspect ratio CheckBox
            maintainAspectCb = new CustomCheckBox
            {
                Text = "Maintain aspect ratio",
                Location = new Point(16, 182),
                Width = 200,
                Checked = true
            };
            maintainAspectCb.CheckedChanged += (s, e) => _maintainAspectRatio = maintainAspectCb.Checked;
            timeCard.Controls.Add(maintainAspectCb);

            var cropHint = new Label
            {
                Text = "Tip: choose a ratio then drag on preview.",
                Location = new Point(16, 202),
                AutoSize = true,
                ForeColor = Theme.Muted,
                Font = new Font("Segoe UI", 8.5f)
            };
            timeCard.Controls.Add(cropHint);

            // Bottom Bar (Slider + Render)
            var bottomBar = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, BackColor = Theme.Bg, Padding = new Padding(16, 8, 16, 8) };
            bottomBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55f)); // Slider
            bottomBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45f)); // Button
            root.Controls.Add(bottomBar, 0, 1);

            trimSlider = new RangeSlider
            {
                Dock = DockStyle.Fill,
                Height = 40,
                Min = 0,
                Max = 100
            };
            trimSlider.SelectionChanged += (s, e) => UpdateInputsFromSlider();
            bottomBar.Controls.Add(trimSlider, 0, 0);

            // Trim/Crop button
            trimBtn = new Button
            {
                Dock = DockStyle.Fill,
                Text = "  🎬  Render",
                Height = 40,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI Semibold", 12, FontStyle.Bold),
                BackColor = Theme.Accent,
                ForeColor = Color.White,
                Cursor = Cursors.Hand,
                Margin = new Padding(8, 4, 0, 4)
            };
            trimBtn.FlatAppearance.BorderSize = 0;
            trimBtn.FlatAppearance.MouseOverBackColor = Theme.AccentHover;
            trimBtn.Click += (s, e) => AddToQueueAction();
            trimBtn.Resize += (s, e) => UiUtils.ApplyRounded(trimBtn, 8);
            bottomBar.Controls.Add(trimBtn, 1, 0);
        }

        public void LoadVideo(string filePath, bool silent = false)
        {
            _trimInputFile = filePath;
            if (!silent) VideoLoaded?.Invoke(filePath);
            trimDropOverlay.Visible = false;
            trimFileLabel.Text = $"📄 {Path.GetFileName(filePath)}";

            Task.Run(() =>
            {
                try
                {
                    // Get info via ffprobe
                    string ffprobe = _videoService.ExtractFfmpegTool("ffprobe.exe");
                    var psi = new ProcessStartInfo
                    {
                        FileName = ffprobe,
                        Arguments = $"-v error -select_streams v:0 -show_entries stream=width,height,duration -of default=noprint_wrappers=1:nokey=1 \"{filePath}\"",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        CreateNoWindow = true
                    };

                    using var p = Process.Start(psi);
                    string output = p?.StandardOutput.ReadToEnd() ?? "";
                    p?.WaitForExit();

                    var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    if (lines.Length >= 2)
                    {
                        int.TryParse(lines[0], out _videoWidth);
                        int.TryParse(lines[1], out _videoHeight);
                        if (lines.Length >= 3)
                            double.TryParse(lines[2], NumberStyles.Float, CultureInfo.InvariantCulture, out _videoDuration);
                    }

                    // Get thumbnail
                    string ffmpeg = _videoService.ExtractFfmpegTool("ffmpeg.exe");
                    string tempThumb = Path.Combine(Path.GetTempPath(), $"vpt_crop_{Guid.NewGuid()}.jpg");
                    var psi2 = new ProcessStartInfo
                    {
                        FileName = ffmpeg,
                        Arguments = $"-y -i \"{filePath}\" -ss {Math.Min(1, _videoDuration * 0.1).ToString(CultureInfo.InvariantCulture)} -vframes 1 -q:v 2 \"{tempThumb}\"",
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };
                    using var p2 = Process.Start(psi2);
                    p2?.WaitForExit();

                    this.Invoke((Action)(() =>
                    {
                        if (File.Exists(tempThumb))
                        {
                            using var fs = new FileStream(tempThumb, FileMode.Open, FileAccess.Read);
                            trimPreviewBox.Image = Image.FromStream(fs);
                        }

                        trimSlider.SetRange(0, _videoDuration);
                        trimSlider.SetSelection(0, _videoDuration);
                        UpdateInputsFromSlider();
                    }));
                }
                catch (Exception ex)
                {
                    Logger.Error("Failed to load video info", ex);
                }
            });
        }

        private void UpdateInputsFromSlider()
        {
            trimStartInput.Text = TimeSpan.FromSeconds(trimSlider.SelectedMin).ToString(@"hh\:mm\:ss\.fff");
            trimEndInput.Text = TimeSpan.FromSeconds(trimSlider.SelectedMax).ToString(@"hh\:mm\:ss\.fff");
        }

        private void UpdateSliderFromInputs()
        {
            if (TimeSpan.TryParse(trimStartInput.Text, out var start) && TimeSpan.TryParse(trimEndInput.Text, out var end) && start < end)
            {
                trimSlider.SetSelection(start.TotalSeconds, end.TotalSeconds);
            }
        }

        private void TrimPreviewBox_Paint(object? sender, PaintEventArgs e)
        {
            if (trimPreviewBox.Image == null) return;

            Rectangle rectToDraw = Rectangle.Empty;

            if (_drawingCrop)
            {
                rectToDraw = _currentDragRect;
            }
            else if (_cropRect.HasValue)
            {
                // Convert video coords to screen coords
                float scaleX = (float)trimPreviewBox.Width / _videoWidth; // Zoom mode makes this tricky.
                                                                          // PictureBox Zoom mode creates simulated image rect centered.

                // Keep it simple: Assuming aspect ratio is preserved and centered.
                // We need to calculate the actual image rectangle inside PictureBox.
                Rectangle imgRect = GetImageRectangle(trimPreviewBox);

                float scale = (float)imgRect.Width / _videoWidth;

                rectToDraw = new Rectangle(
                    imgRect.X + (int)(_cropRect.Value.X * scale),
                    imgRect.Y + (int)(_cropRect.Value.Y * scale),
                    (int)(_cropRect.Value.Width * scale),
                    (int)(_cropRect.Value.Height * scale)
                );
            }

            if (rectToDraw != Rectangle.Empty && rectToDraw.Width > 0 && rectToDraw.Height > 0)
            {
                using var pen = new Pen(Theme.Accent, 2);
                using var brush = new SolidBrush(Color.FromArgb(100, 0, 0, 0)); // Dim outside

                // Draw dimming is hard with just one rect.
                // Let's just draw the crop rect.
                e.Graphics.DrawRectangle(pen, rectToDraw);
                using (var fill = new SolidBrush(Color.FromArgb(50, Theme.Accent.R, Theme.Accent.G, Theme.Accent.B)))
                {
                    e.Graphics.FillRectangle(fill, rectToDraw);
                }
            }
        }

        private Rectangle GetImageRectangle(PictureBox pb)
        {
            if (pb.Image == null) return Rectangle.Empty;

            Size imgSize = pb.Image.Size;
            Size ctrlSize = pb.ClientSize;
            float imgAspect = (float)imgSize.Width / imgSize.Height;
            float ctrlAspect = (float)ctrlSize.Width / ctrlSize.Height;

            int x = 0, y = 0, w = 0, h = 0;

            if (imgAspect > ctrlAspect)
            {
                // Width bound
                w = ctrlSize.Width;
                h = (int)(w / imgAspect);
                x = 0;
                y = (ctrlSize.Height - h) / 2;
            }
            else
            {
                // Height bound
                h = ctrlSize.Height;
                w = (int)(h * imgAspect);
                y = 0;
                x = (ctrlSize.Width - w) / 2;
            }
            return new Rectangle(x, y, w, h);
        }

        private void TrimPreviewBox_MouseDown(object? sender, MouseEventArgs e)
        {
            if (trimPreviewBox.Image == null || e.Button != MouseButtons.Left) return;
            _drawingCrop = true;
            _cropStartPoint = e.Location;
            _currentDragRect = new Rectangle(e.X, e.Y, 0, 0);
            trimPreviewBox.Invalidate();
        }

        private void TrimPreviewBox_MouseMove(object? sender, MouseEventArgs e)
        {
            if (!_drawingCrop) return;

            int x = Math.Min(_cropStartPoint.X, e.X);
            int y = Math.Min(_cropStartPoint.Y, e.Y);
            int w = Math.Abs(e.X - _cropStartPoint.X);
            int h = Math.Abs(e.Y - _cropStartPoint.Y);

            // Apply Aspect Ratio Constraints
            if (_maintainAspectRatio && _currentAspectRatio != "Free")
            {
                float targetRatio = 0;

                if (_currentAspectRatio == "16:9") targetRatio = 16f / 9f;
                else if (_currentAspectRatio == "9:16") targetRatio = 9f / 16f;
                else if (_currentAspectRatio == "4:3") targetRatio = 4f / 3f;
                else if (_currentAspectRatio == "1:1") targetRatio = 1f;
                else if (_currentAspectRatio == "Original" && _videoHeight > 0)
                    targetRatio = (float)_videoWidth / _videoHeight;

                if (targetRatio > 0)
                {
                    // Adjust height based on width to maintain ratio
                    // Depending on drag direction, we might want to prioritize the larger dimension,
                    // but fixing width and adjusting height is standard.
                    h = (int)(w / targetRatio);

                    // Re-calculate Y if dragging upwards so the box doesn't detach from the cursor start point
                    if (e.Y < _cropStartPoint.Y)
                    {
                        y = _cropStartPoint.Y - h;
                    }
                }
            }

            _currentDragRect = new Rectangle(x, y, w, h);
            trimPreviewBox.Invalidate();
        }

        private void TrimPreviewBox_MouseUp(object? sender, MouseEventArgs e)
        {
            if (!_drawingCrop) return;
            _drawingCrop = false;

            // Finalize crop rect
            // Convert back to video coordinates
            Rectangle imgRect = GetImageRectangle(trimPreviewBox);

            // Intersect drag rect with image rect
            Rectangle intersection = Rectangle.Intersect(_currentDragRect, imgRect);

            if (intersection.Width > 10 && intersection.Height > 10)
            {
                // Re-apply aspect ratio to the intersected rectangle to prevent distortion from edge clipping
                if (_maintainAspectRatio && _currentAspectRatio != "Free")
                {
                    float targetRatio = 0;
                    if (_currentAspectRatio == "16:9") targetRatio = 16f / 9f;
                    else if (_currentAspectRatio == "9:16") targetRatio = 9f / 16f;
                    else if (_currentAspectRatio == "4:3") targetRatio = 4f / 3f;
                    else if (_currentAspectRatio == "1:1") targetRatio = 1f;
                    else if (_currentAspectRatio == "Original" && _videoHeight > 0)
                        targetRatio = (float)_videoWidth / _videoHeight;

                    if (targetRatio > 0)
                    {
                        // Given we might have hit an edge, we need to shrink the other dimension to match the ratio
                        float currentRatio = (float)intersection.Width / intersection.Height;
                        if (currentRatio > targetRatio)
                        {
                            // It's too wide, so shrink the width to match the height's ratio bounds
                            intersection.Width = (int)(intersection.Height * targetRatio);
                        }
                        else if (currentRatio < targetRatio)
                        {
                            // It's too tall, so shrink the height to match the width's ratio bounds
                            intersection.Height = (int)(intersection.Width / targetRatio);
                        }
                    }
                }

                float scale = (float)_videoWidth / imgRect.Width;
                _cropRect = new Rectangle(
                    (int)((intersection.X - imgRect.X) * scale),
                    (int)((intersection.Y - imgRect.Y) * scale),
                    (int)(intersection.Width * scale),
                    (int)(intersection.Height * scale)
                );
            }
            else
            {
                _cropRect = null;
            }
            trimPreviewBox.Invalidate();
        }

        private void AddToQueueAction()
        {
            if (string.IsNullOrEmpty(_trimInputFile))
            {
                MessageBox.Show("Please select a video file first.", "No File", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (!TimeSpan.TryParse(trimStartInput.Text, CultureInfo.InvariantCulture, out var start) ||
                !TimeSpan.TryParse(trimEndInput.Text, CultureInfo.InvariantCulture, out var end))
            {
                MessageBox.Show("Invalid time format. Please use HH:mm:ss.fff", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (start >= end)
            {
                MessageBox.Show("Start time must be less than end time.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            using var qualityDlg = new RenderQualityDialog(_parentForm);
            if (qualityDlg.ShowDialog(_parentForm) != DialogResult.OK) return;

            var options = new VideoProcessingOptions
            {
                InputPath = _trimInputFile,
                Quality = qualityDlg.SelectedQuality,
                ScaleFilter = qualityDlg.GetScaleFilter(),
                TrimStart = start,
                TrimEnd = end,
                CropRectangle = _cropRect,
                TotalDuration = _videoDuration
            };

            if (_parentForm is Form1 f1)
            {
                f1.AddToBatch(_trimInputFile, options);
            }
        }
    }
}
