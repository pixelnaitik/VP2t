using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;
using VPT.Core;
using VPT.Services;
using VPT.Forms;

namespace VPT.Forms.Controls
{
    public partial class SingleClicksControl : UserControl
    {
        private Panel leftDropArea = null!;
        private TableLayoutPanel grid = null!;
        private Button renderBtn = null!;
        private readonly ToolTip tips = new ToolTip { AutoPopDelay = 8000, InitialDelay = 300, ReshowDelay = 300 };

        public event Action<string>? VideoLoaded;

        // --- Preview & Timeline UI -------------------------------------------
        private PictureBox previewBox = null!;
        private Label uploadHintLabel = null!;
        private Button timelinePlayBtn = null!;
        private Label fileInfoLabel = null!;
        private Panel timelinePanel = null!;
        private Label timelineStartLabel = null!;
        private Label timelineEndLabel = null!;
        private Panel timelineTrack = null!;
        private Panel timelineProgress = null!;
        private double _videoDuration = 0;

        // --- State -----------------------------------------------------------
        private float _customRotateDeg = 0f;
        private string? _pendingInputFile;
        private string _selectedQuality = "1080p HD";
        private Image? _originalThumbnail;
        private float _speedMultiplier = 1.0f;
        private List<string> _batchFiles = new();
        private readonly VideoProcessingService _videoService;
        private readonly Form _parentForm;
        private readonly System.Windows.Forms.Timer _playbackTimer = new() { Interval = 250 };
        private WindowsMediaPlayerHost? _videoPlayer;
        private bool _isPlaying;

        // Button lists
        private List<Button> rotationButtons = new();
        private List<Button> volumeButtons = new();
        private List<Button> bigAudioButtons = new();
        private List<Button> speedButtons = new();

        public SingleClicksControl(Form parentForm, VideoProcessingService videoService)
        {
            _parentForm = parentForm;
            _videoService = videoService;

            this.Dock = DockStyle.Fill;
            this.BackColor = Theme.Bg;
            this.ForeColor = Theme.Fg;

            InitializeComponent();
            _playbackTimer.Tick += (_, __) => UpdatePlaybackUi();
        }



        private sealed class WindowsMediaPlayerHost : AxHost
        {
            public WindowsMediaPlayerHost() : base("6BF52A52-394A-11d3-B153-00C04F79FAA6") { }

            public object? Ocx => GetOcx();
        }

        private void InitializeComponent()
        {
            // Root (content + render bar)
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, BackColor = Theme.Bg };
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 64f));
            this.Controls.Add(root);

            // Content split
            var content = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, BackColor = Theme.Bg, Padding = new Padding(10) };
            content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 58f));
            content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42f));
            root.Controls.Add(content, 0, 0);

            // Left: Container for preview + timeline
            var leftContainer = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, BackColor = Color.FromArgb(12, 14, 18) };
            leftContainer.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));  // Preview
            leftContainer.RowStyles.Add(new RowStyle(SizeType.Absolute, 50f));  // Timeline
            leftContainer.RowStyles.Add(new RowStyle(SizeType.Absolute, 35f));  // File info
            UiUtils.ApplyRounded(leftContainer, 14);
            leftContainer.Resize += (s, e) => UiUtils.ApplyRounded(leftContainer, 14);
            content.Controls.Add(leftContainer, 0, 0);

            // Preview area
            leftDropArea = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(12, 14, 18), Padding = new Padding(10) };

            previewBox = new PictureBox
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(18, 20, 26),
                SizeMode = PictureBoxSizeMode.Zoom,
                Cursor = Cursors.Hand
            };

            uploadHintLabel = new Label
            {
                Dock = DockStyle.Fill,
                ForeColor = Theme.Muted,
                BackColor = Color.Transparent,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 14, FontStyle.Regular),
                Text = "📂  Click to Upload\nor Drag & Drop",
                Cursor = Cursors.Hand
            };
            previewBox.Controls.Add(uploadHintLabel);

            _videoPlayer = new WindowsMediaPlayerHost
            {
                Dock = DockStyle.Fill,
                Enabled = true,
                Visible = false
            };
            _videoPlayer.CreateControl();

            leftDropArea.Controls.Add(previewBox);
            if (_videoPlayer != null) leftDropArea.Controls.Add(_videoPlayer);
            leftDropArea.AllowDrop = true;

            ConfigureVideoPlayer();

            // Click handlers
            Action openFileAction = () =>
            {
                var ofd = new OpenFileDialog
                {
                    Title = "Select video file(s)",
                    Filter = "Video Files|*.mp4;*.mov;*.avi;*.mkv;*.webm;*.wmv;*.flv;*.m4v|All Files|*.*",
                    Multiselect = true
                };
                if (ofd.ShowDialog(this) == DialogResult.OK)
                {
                    foreach (var file in ofd.FileNames)
                    {
                        if (!_batchFiles.Contains(file))
                            _batchFiles.Add(file);
                    }
                    LoadVideoFile(ofd.FileNames[0]);
                    UpdateBatchInfo();
                }
            };

            previewBox.Click += (s, e) => { if (_pendingInputFile == null) openFileAction(); };
            uploadHintLabel.Click += (s, e) => openFileAction();
            leftDropArea.Click += (s, e) => { if (_pendingInputFile == null) openFileAction(); };

            // Drag handlers
            leftDropArea.DragEnter += (s, e) => { if (e.Data?.GetDataPresent(DataFormats.FileDrop) == true) e.Effect = DragDropEffects.Copy; };
            leftDropArea.DragDrop += (s, e) =>
            {
                if (e.Data?.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0)
                {
                    foreach (var file in files)
                    {
                        if (!_batchFiles.Contains(file))
                            _batchFiles.Add(file);
                    }
                    LoadVideoFile(files[0]);
                    UpdateBatchInfo();
                }
            };

            leftContainer.Controls.Add(leftDropArea, 0, 0);

            // Timeline
            timelinePanel = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(18, 22, 30), Padding = new Padding(12, 10, 12, 10) };

            timelinePlayBtn = new Button
            {
                Text = "\u25B6",
                Size = new Size(24, 24),
                Location = new Point(12, 12),
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.White,
                BackColor = Color.Transparent,
                Font = new Font("Segoe UI Symbol", 11, FontStyle.Regular),
                Cursor = Cursors.Hand
            };
            timelinePlayBtn.FlatAppearance.BorderSize = 0;
            timelinePlayBtn.FlatAppearance.MouseOverBackColor = Color.FromArgb(40, 255, 255, 255);
            timelinePlayBtn.FlatAppearance.MouseDownBackColor = Color.FromArgb(60, 255, 255, 255);
            timelinePlayBtn.Click += (s, e) => TogglePlayback();
            timelinePanel.Controls.Add(timelinePlayBtn);
            UiUtils.ApplyRounded(timelinePlayBtn, 12);

            timelineStartLabel = new Label
            {
                Text = "0:00",
                ForeColor = Theme.Muted,
                BackColor = Color.Transparent,
                Font = new Font("Segoe UI", 9, FontStyle.Regular),
                AutoSize = true,
                Location = new Point(44, 16)
            };
            timelinePanel.Controls.Add(timelineStartLabel);

            timelineEndLabel = new Label
            {
                Text = "0:00",
                ForeColor = Theme.Muted,
                BackColor = Color.Transparent,
                Font = new Font("Segoe UI", 9, FontStyle.Regular),
                AutoSize = true,
                Anchor = AnchorStyles.Right | AnchorStyles.Top
            };
            timelinePanel.Controls.Add(timelineEndLabel);

            timelineTrack = new Panel
            {
                BackColor = Color.FromArgb(45, 52, 68),
                Height = 6,
                Anchor = AnchorStyles.Left | AnchorStyles.Right
            };

            timelineProgress = new Panel
            {
                BackColor = Theme.Accent,
                Height = 6,
                Width = 0,
                Location = new Point(0, 0)
            };
            timelineTrack.Controls.Add(timelineProgress);
            timelinePanel.Controls.Add(timelineTrack);

            bool isDraggingTimeline = false;
            Action<MouseEventArgs, Panel> handleTimelineMouse = (e, panel) =>
            {
                if (!isDraggingTimeline) return;
                int x = e.X;
                if (panel == timelineProgress) x += timelineProgress.Left;
                double pct = Math.Max(0, Math.Min(1.0, (double)x / timelineTrack.Width));
                SeekVideo(pct);
            };

            timelineTrack.MouseDown += (s, e) => { isDraggingTimeline = true; handleTimelineMouse(e, timelineTrack); };
            timelineTrack.MouseMove += (s, e) => handleTimelineMouse(e, timelineTrack);
            timelineTrack.MouseUp += (s, e) => isDraggingTimeline = false;

            timelineProgress.MouseDown += (s, e) => { isDraggingTimeline = true; handleTimelineMouse(e, timelineProgress); };
            timelineProgress.MouseMove += (s, e) => handleTimelineMouse(e, timelineProgress);
            timelineProgress.MouseUp += (s, e) => isDraggingTimeline = false;

            timelinePanel.Resize += (s, e) =>
            {
                int leftMargin = 84;
                int rightMargin = 52;
                timelineTrack.Location = new Point(leftMargin, 18);
                timelineTrack.Width = Math.Max(10, timelinePanel.Width - leftMargin - rightMargin);
                timelineEndLabel.Location = new Point(timelinePanel.Width - timelineEndLabel.Width - 12, 16);
                UiUtils.ApplyRounded(timelineTrack, 3);
                UiUtils.ApplyRounded(timelineProgress, 3);
            };

            leftContainer.Controls.Add(timelinePanel, 0, 1);

            // File Info
            fileInfoLabel = new Label
            {
                Dock = DockStyle.Fill,
                ForeColor = Theme.Fg,
                BackColor = Color.FromArgb(20, 24, 32),
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 9, FontStyle.Regular),
                Text = "No file loaded (supports multiple files)"
            };
            leftContainer.Controls.Add(fileInfoLabel, 0, 2);

            // Right Grid
            // Right Scroll Container
            var rightScrollPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Theme.Bg,
                AutoScroll = true
            };
            content.Controls.Add(rightScrollPanel, 1, 0);

            // Right Grid
            grid = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                ColumnCount = 4,
                RowCount = 8,
                BackColor = Theme.Bg,
                Padding = new Padding(8),
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink
            };
            for (int c = 0; c < 4; c++) grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));
            for (int r = 0; r < 8; r++) grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 80f));
            grid.Resize += (_, __) => UpdateSquareGrid();

            rightScrollPanel.Controls.Add(grid);

            // Buttons
            AddBtn("VPU_Icon_Rotation_90.png", "Rotate 90°", col: 0, row: 0);
            AddBtn("VPU_Icon_Rotation_180.png", "Rotate 180°", col: 1, row: 0);
            AddBtn("VPU_Icon_Rotation_270.png", "Rotate 270°", col: 2, row: 0);
            AddBtn("VPU_Icon_Rotation_Custom.png", "Rotate custom", col: 3, row: 0);

            AddBtn("VPU_Icon_Flip_Horizontal.png", "Flip horizontal", col: 0, row: 1, colSpan: 2, rowSpan: 2, big: true);
            AddBtn("VPU_Icon_Flip_Vertical.png", "Flip vertical", col: 2, row: 1, colSpan: 2, rowSpan: 2, big: true);

            AddBtn("VPU_Icon_Volume_50_Up.png", "Volume +50%", col: 0, row: 3);
            AddBtn("VPU_Icon_Volume_25_Up.png", "Volume +25%", col: 1, row: 3);
            AddBtn("VPU_Icon_Volume_25_Down.png", "Volume −25%", col: 2, row: 3);
            AddBtn("VPU_Icon_Volume_50_Down.png", "Volume −50%", col: 3, row: 3);

            AddBtn("01_VPU_Icon_Stereot2mono.png", "Stereo → Mono", col: 0, row: 4, colSpan: 2, rowSpan: 2, big: true);
            AddBtn("VPU_Icon_Volume_Mute.png", "Mute", col: 2, row: 4, colSpan: 2, rowSpan: 2, big: true);

            AddSpeedBtn("0.5×", 0.5f, "Slow motion (half speed)", col: 0, row: 6);
            AddSpeedBtn("0.75×", 0.75f, "Slightly slower", col: 1, row: 6);
            AddSpeedBtn("1.5×", 1.5f, "Speed up 1.5x", col: 2, row: 6);
            AddSpeedBtn("2×", 2.0f, "Double speed", col: 3, row: 6);

            // Render Button
            renderBtn = new Button
            {
                Dock = DockStyle.Fill,
                Text = "  🎬  Render",
                Height = 56,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI Semibold", 12, FontStyle.Bold),
                BackColor = Theme.Accent,
                ForeColor = Color.White,
                Margin = new Padding(12, 8, 12, 12),
                Cursor = Cursors.Hand
            };
            renderBtn.FlatAppearance.BorderSize = 0;
            renderBtn.FlatAppearance.MouseOverBackColor = Theme.AccentHover;
            renderBtn.FlatAppearance.MouseDownBackColor = Theme.AccentDark;
            renderBtn.Click += (s, e) => AddToQueueAction();
            renderBtn.Resize += (s, e) => UiUtils.ApplyRounded(renderBtn, 12);
            root.Controls.Add(renderBtn, 0, 1);
            UiUtils.ApplyRounded(renderBtn, 12);

            UpdateSquareGrid();
        }

        private void AddToQueueAction()
        {
            List<string> filesToProcess = new();
            if (_batchFiles.Count > 0) filesToProcess.AddRange(_batchFiles);
            else if (_pendingInputFile != null) filesToProcess.Add(_pendingInputFile);
            else
            {
                var ofd = new OpenFileDialog
                {
                    Title = "Select video file(s)",
                    Filter = "Video Files|*.mp4;*.mov;*.avi;*.mkv;*.webm;*.wmv|All Files|*.*",
                    Multiselect = true
                };
                if (ofd.ShowDialog(this) == DialogResult.OK)
                    filesToProcess.AddRange(ofd.FileNames);
            }

            if (filesToProcess.Count == 0) return;

            using var qualityDlg = new RenderQualityDialog(_parentForm);
            if (qualityDlg.ShowDialog(_parentForm) != DialogResult.OK) return;

            _selectedQuality = qualityDlg.SelectedQuality;
            string scaleFilter = qualityDlg.GetScaleFilter();

            foreach (var inputFile in filesToProcess)
            {
                var options = new VideoProcessingOptions
                {
                    InputPath = inputFile,
                    ScaleFilter = scaleFilter,
                    Quality = _selectedQuality,
                    CustomRotationDegrees = _customRotateDeg,
                    SpeedMultiplier = _speedMultiplier,
                    // Duration not critical for batch add, service will re-probe or we valid here
                };

                // Apply active buttons (Rotation)
                if (rotationButtons.Any(b => ((PngIconTag)b.Tag!).Active))
                {
                    var active = rotationButtons.First(b => ((PngIconTag)b.Tag!).Active);
                    var tag = (PngIconTag)active.Tag!;
                    if (tag.FileName.Contains("90")) options.Rotate90 = true;
                    else if (tag.FileName.Contains("180")) options.Rotate180 = true;
                    else if (tag.FileName.Contains("270")) options.Rotate270 = true;
                    else if (tag.FileName.Contains("Custom")) options.RotateCustom = true;
                }

                // Apply Flips
                foreach (var b in grid.Controls.OfType<Button>().Where(b => b.Tag is PngIconTag t && t.Active))
                {
                    var t = (PngIconTag)b.Tag!;
                    if (t.FileName.Contains("Flip_Horizontal")) options.FlipHorizontal = true;
                    else if (t.FileName.Contains("Flip_Vertical")) options.FlipVertical = true;
                }

                // Audio
                bool muteActive = bigAudioButtons.Any(b => ((PngIconTag)b.Tag!).Active && ((PngIconTag)b.Tag!).FileName.Contains("Mute"));
                options.Mute = muteActive;
                options.StereoToMono = bigAudioButtons.Any(b => ((PngIconTag)b.Tag!).Active && ((PngIconTag)b.Tag!).FileName.Contains("Stereot2mono"));

                if (!muteActive)
                {
                    foreach (var b in volumeButtons.Where(b => ((PngIconTag)b.Tag!).Active))
                    {
                        var t = (PngIconTag)b.Tag!;
                        if (t.FileName.Contains("50_Up")) options.VolumeAdjustmentsDb.Add(12f);
                        if (t.FileName.Contains("25_Up")) options.VolumeAdjustmentsDb.Add(6f);
                        if (t.FileName.Contains("25_Down")) options.VolumeAdjustmentsDb.Add(-6f);
                        if (t.FileName.Contains("50_Down")) options.VolumeAdjustmentsDb.Add(-12f);
                    }
                }

                if (_parentForm is Form1 f1) f1.AddToBatch(inputFile, options);
            }

            MessageBox.Show($"Added {filesToProcess.Count} files to queue.", "Queued", MessageBoxButtons.OK, MessageBoxIcon.Information);
            ClearBatchQueue();
        }

        private void LoadVideoFile(string filePath, bool notifyVideoLoaded = true)
        {
            _pendingInputFile = filePath;
            if (notifyVideoLoaded) VideoLoaded?.Invoke(filePath);
            uploadHintLabel.Visible = false;
            ShowPlayerSurface(true);
            PlayVideo(filePath);

            Task.Run(() =>
            {
                try
                {
                    string ffmpegPath = _videoService.ExtractFfmpegTool("ffmpeg.exe");
                    string ffprobePath = _videoService.ExtractFfmpegTool("ffprobe.exe");
                    string thumbPath = Path.Combine(Path.GetTempPath(), $"vpt_thumb_{Guid.NewGuid()}.jpg");

                    var probeInfo = new ProcessStartInfo
                    {
                        FileName = ffprobePath,
                        Arguments = $"-v error -show_entries format=duration -of default=noprint_wrappers=1:nokey=1 \"{filePath}\"",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        CreateNoWindow = true
                    };

                    double duration = 0;
                    using (var probe = Process.Start(probeInfo))
                    {
                        if (probe != null)
                        {
                            string output = probe.StandardOutput.ReadToEnd().Trim();
                            probe.WaitForExit();
                            double.TryParse(output, NumberStyles.Float, CultureInfo.InvariantCulture, out duration);
                        }
                    }

                    double thumbTime = Math.Min(1, duration * 0.1);
                    var thumbInfo = new ProcessStartInfo
                    {
                        FileName = ffmpegPath,
                        Arguments = $"-y -ss {thumbTime.ToString("0.###", CultureInfo.InvariantCulture)} -i \"{filePath}\" -vframes 1 -q:v 2 \"{thumbPath}\"",
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };

                    using (var p = Process.Start(thumbInfo)) p?.WaitForExit();

                    this.Invoke((Action)(() =>
                    {
                        if (File.Exists(thumbPath))
                        {
                            using var fs = new FileStream(thumbPath, FileMode.Open, FileAccess.Read);
                            using var tempImage = Image.FromStream(fs);
                            previewBox.Image?.Dispose();
                            _originalThumbnail?.Dispose();
                            _originalThumbnail = new Bitmap(tempImage);
                            previewBox.Image = new Bitmap(_originalThumbnail);
                            try { File.Delete(thumbPath); } catch (Exception ex) { Logger.Error("Failed to delete generated thumbnail", ex); }
                        }

                        _videoDuration = duration;
                        string fileName = Path.GetFileName(filePath);
                        string durationStr = UiUtils.FormatDuration(duration);
                        fileInfoLabel.Text = $"📄 {fileName}  •  ⏱ {durationStr}";

                        timelineStartLabel.Text = "0:00";
                        timelineEndLabel.Text = durationStr;
                        timelineProgress.Width = 0;
                        _isPlaying = true;
                        if (timelinePlayBtn != null) timelinePlayBtn.Text = "\u23F8";
                    }));
                }
                catch (Exception)
                {
                    this.Invoke((Action)(() => fileInfoLabel.Text = $"📄 {Path.GetFileName(filePath)}"));
                }
            });
        }

        private void ConfigureVideoPlayer()
        {
            if (_videoPlayer?.Ocx is not object ocx) return;

            try
            {
                SetComProperty(ocx, "uiMode", "none");
                SetComProperty(ocx, "stretchToFit", true);

                var settings = GetComProperty(ocx, "settings");
                if (settings != null)
                {
                    SetComProperty(settings, "autoStart", false);
                    SetComProperty(settings, "volume", 100);
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to initialize embedded video player", ex);
            }
        }

        private void ShowPlayerSurface(bool show)
        {
            previewBox.Visible = !show;
            if (_videoPlayer != null)
            {
                _videoPlayer.Visible = show;
                if (show) _videoPlayer.BringToFront();
            }
        }

        private void PlayVideo(string filePath)
        {
            if (_videoPlayer?.Ocx is not object ocx) return;

            try
            {
                SetComProperty(ocx, "uiMode", "none");
                SetComProperty(ocx, "URL", filePath);
                var controls = GetComProperty(ocx, "controls");
                InvokeComMethod(controls, "play");
                _isPlaying = true;
                if (timelinePlayBtn != null) timelinePlayBtn.Text = "\u23F8";
                _playbackTimer.Start();
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to play uploaded video", ex);
            }
        }

        private void TogglePlayback()
        {
            if (_videoPlayer?.Ocx is not object ocx || string.IsNullOrWhiteSpace(_pendingInputFile)) return;

            try
            {
                var controls = GetComProperty(ocx, "controls");
                if (_isPlaying)
                {
                    InvokeComMethod(controls, "pause");
                    _isPlaying = false;
                    if (timelinePlayBtn != null) timelinePlayBtn.Text = "\u25B6";
                }
                else
                {
                    InvokeComMethod(controls, "play");
                    _isPlaying = true;
                    if (timelinePlayBtn != null) timelinePlayBtn.Text = "\u23F8";
                    _playbackTimer.Start();
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to toggle video playback", ex);
            }
        }

        private void UpdatePlaybackUi()
        {
            if (!_isPlaying)
            {
                _playbackTimer.Stop();
                return;
            }

            if (timelineTrack.Width <= 0 || _videoDuration <= 0) return;
            double current = GetCurrentPositionSeconds();
            current = Math.Max(0, Math.Min(_videoDuration, current));

            timelineStartLabel.Text = UiUtils.FormatDuration(current);
            timelineProgress.Width = (int)Math.Round((current / _videoDuration) * timelineTrack.Width);
        }

        private void SeekVideo(double percentage)
        {
            if (_videoPlayer?.Ocx is not object ocx || _videoDuration <= 0) return;
            try
            {
                var controls = GetComProperty(ocx, "controls");
                if (controls != null)
                {
                    double newTime = Math.Max(0, Math.Min(_videoDuration, _videoDuration * percentage));
                    SetComProperty(controls, "currentPosition", newTime);
                    UpdatePlaybackUi();
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to seek video", ex);
            }
        }

        private double GetCurrentPositionSeconds()
        {
            if (_videoPlayer?.Ocx is not object ocx) return 0;
            try
            {
                var controls = GetComProperty(ocx, "controls");
                object? pos = GetComProperty(controls, "currentPosition");
                if (pos == null) return 0;
                return Convert.ToDouble(pos, CultureInfo.InvariantCulture);
            }
            catch
            {
                return 0;
            }
        }

        private static object? GetComProperty(object? target, string propertyName)
        {
            if (target == null) return null;
            return target.GetType().InvokeMember(propertyName, BindingFlags.GetProperty, null, target, null);
        }

        private static void SetComProperty(object target, string propertyName, object? value)
        {
            target.GetType().InvokeMember(propertyName, BindingFlags.SetProperty, null, target, new[] { value });
        }

        private static void InvokeComMethod(object? target, string methodName)
        {
            if (target == null) return;
            target.GetType().InvokeMember(methodName, BindingFlags.InvokeMethod, null, target, null);
        }

        private void UpdateBatchInfo()
        {
            if (_batchFiles.Count > 1) fileInfoLabel.Text = $"📁 {_batchFiles.Count} files queued for batch processing";
            else if (_batchFiles.Count == 1) fileInfoLabel.Text = $"📄 {Path.GetFileName(_batchFiles[0])}";
        }

        private void ClearBatchQueue()
        {
            _batchFiles.Clear();
            _pendingInputFile = null;
            previewBox.Image?.Dispose();
            previewBox.Image = null;
            _originalThumbnail?.Dispose();
            _originalThumbnail = null;
            uploadHintLabel.Visible = true;
            ShowPlayerSurface(false);
            _isPlaying = false;
            if (timelinePlayBtn != null) timelinePlayBtn.Text = "\u25B6";
            _playbackTimer.Stop();
            try
            {
                var controls = GetComProperty(_videoPlayer?.Ocx, "controls");
                InvokeComMethod(controls, "stop");
                if (_videoPlayer?.Ocx is object ocx) SetComProperty(ocx, "URL", string.Empty);
            }
            catch { }
            fileInfoLabel.Text = "No file loaded (supports multiple files)";
            timelineProgress.Width = 0;
            timelineStartLabel.Text = "0:00";
            timelineEndLabel.Text = "0:00";
        }

        private void UpdatePreviewTransform()
        {
            if (_originalThumbnail == null) return;
            var transformed = new Bitmap(_originalThumbnail);

            float rotationAngle = 0f;
            foreach (var btn in rotationButtons)
            {
                var tag = (PngIconTag)btn.Tag!;
                if (tag.Active)
                {
                    if (tag.FileName.Contains("90")) rotationAngle = 90f;
                    else if (tag.FileName.Contains("180")) rotationAngle = 180f;
                    else if (tag.FileName.Contains("270")) rotationAngle = 270f;
                    else if (tag.FileName.Contains("Custom")) rotationAngle = _customRotateDeg;
                    break;
                }
            }

            if (rotationAngle == 90f) transformed.RotateFlip(RotateFlipType.Rotate90FlipNone);
            else if (rotationAngle == 180f) transformed.RotateFlip(RotateFlipType.Rotate180FlipNone);
            else if (rotationAngle == 270f) transformed.RotateFlip(RotateFlipType.Rotate270FlipNone);
            else if (rotationAngle != 0f)
            {
                var oldBmp = transformed;
                transformed = RotateBitmap(oldBmp, rotationAngle);
                oldBmp.Dispose();
            }

            foreach (Control c in grid.Controls)
            {
                if (c is Button b && b.Tag is PngIconTag t && t.Active)
                {
                    if (t.FileName.Contains("Flip_Horizontal")) transformed.RotateFlip(RotateFlipType.RotateNoneFlipX);
                    else if (t.FileName.Contains("Flip_Vertical")) transformed.RotateFlip(RotateFlipType.RotateNoneFlipY);
                }
            }

            previewBox.Image?.Dispose();
            previewBox.Image = transformed;
        }

        private static Bitmap RotateBitmap(Bitmap bmp, float angle)
        {
            double radians = angle * Math.PI / 180.0;
            double cos = Math.Abs(Math.Cos(radians));
            double sin = Math.Abs(Math.Sin(radians));
            int newWidth = (int)(bmp.Width * cos + bmp.Height * sin);
            int newHeight = (int)(bmp.Width * sin + bmp.Height * cos);

            var rotated = new Bitmap(newWidth, newHeight);
            rotated.SetResolution(bmp.HorizontalResolution, bmp.VerticalResolution);

            using (var g = Graphics.FromImage(rotated))
            {
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.SmoothingMode = SmoothingMode.HighQuality;
                g.TranslateTransform(newWidth / 2f, newHeight / 2f);
                g.RotateTransform(angle);
                g.TranslateTransform(-bmp.Width / 2f, -bmp.Height / 2f);
                g.DrawImage(bmp, 0, 0);
            }
            return rotated;
        }

        private void UpdateSquareGrid()
        {
            if (grid.Width <= 0) return;
            int pad = grid.Padding.Left + grid.Padding.Right;
            float unit = (grid.ClientSize.Width - pad) / 4.0f;
            unit = Math.Max(unit, 32f);
            unit = Math.Min(unit, 100f); // Cap max size

            for (int r = 0; r < grid.RowCount; r++)
            {
                grid.RowStyles[r].Height = unit;
                grid.RowStyles[r].SizeType = SizeType.Absolute;
            }

            foreach (Control ctl in grid.Controls)
            {
                if (ctl is Button b && b.Tag is PngIconTag t)
                {
                    int target = t.Big ? (int)(unit * 2) - 28 : (int)unit - 18;
                    if (target < 24) target = 24;
                    b.Image = PngIconService.Render(t.FileName, t.Active ? Color.White : Color.Gainsboro, target, target, padding: 8);
                    UiUtils.ApplyRounded(b, 14);
                }
            }
        }

        private void AddSpeedBtn(string label, float multiplier, string tooltip, int col, int row)
        {
            var btn = new Button
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(6),
                FlatStyle = FlatStyle.Flat,
                BackColor = Theme.CardBg,
                ForeColor = Theme.Fg,
                Text = label,
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter,
                Cursor = Cursors.Hand,
                Tag = multiplier
            };
            btn.FlatAppearance.BorderSize = 0;
            btn.FlatAppearance.MouseOverBackColor = Theme.CardBgHover;
            btn.FlatAppearance.MouseDownBackColor = Theme.CardActive;

            btn.Click += (s, e) =>
            {
                float speed = (float)btn.Tag!;
                bool wasActive = Math.Abs(_speedMultiplier - speed) < 0.01f;

                foreach (var sb in speedButtons)
                {
                    sb.BackColor = Theme.CardBg;
                    sb.ForeColor = Theme.Fg;
                    sb.FlatAppearance.MouseOverBackColor = Theme.CardBgHover;
                    sb.FlatAppearance.MouseDownBackColor = Theme.CardActive;
                }

                if (wasActive) _speedMultiplier = 1.0f;
                else
                {
                    _speedMultiplier = speed;
                    btn.BackColor = Theme.Accent;
                    btn.ForeColor = Color.White;
                    btn.FlatAppearance.MouseOverBackColor = Theme.Accent;
                    btn.FlatAppearance.MouseDownBackColor = Theme.Accent;
                }
            };

            btn.Resize += (s, e) => UiUtils.ApplyRounded(btn, 12);
            tips.SetToolTip(btn, tooltip);
            speedButtons.Add(btn);
            grid.Controls.Add(btn, col, row);
        }

        private void AddBtn(string fileName, string help, int col, int row, int colSpan = 1, int rowSpan = 1, bool big = false)
        {
            var btn = new Button
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(6),
                FlatStyle = FlatStyle.Flat,
                BackColor = Theme.CardBg,
                ForeColor = Theme.Fg,
                Tag = new PngIconTag { FileName = fileName, Big = big, Active = false },
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderSize = 0;
            btn.FlatAppearance.MouseOverBackColor = Theme.CardBgHover;
            btn.FlatAppearance.MouseDownBackColor = Theme.CardActive;

            btn.Resize += (s, e) => UiUtils.ApplyRounded(btn, 12);

            if (fileName.StartsWith("VPU_Icon_Rotation_")) rotationButtons.Add(btn);
            if (fileName.StartsWith("VPU_Icon_Volume_") && !fileName.Contains("Mute")) volumeButtons.Add(btn);
            if (fileName.Contains("Stereot2mono") || fileName.Contains("Mute")) bigAudioButtons.Add(btn);

            btn.Click += (s, e) => HandleButtonClick(btn);

            tips.SetToolTip(btn, help);
            grid.Controls.Add(btn, col, row);
            if (colSpan > 1) grid.SetColumnSpan(btn, colSpan);
            if (rowSpan > 1) grid.SetRowSpan(btn, rowSpan);
        }

        private void HandleButtonClick(Button btn)
        {
            var tag = (PngIconTag)btn.Tag!;

            if (tag.FileName.Contains("Custom"))
            {
                using (var dlg = new CustomRotationDialog(_parentForm))
                {
                    if (dlg.ShowDialog(_parentForm) == DialogResult.OK && float.TryParse(dlg.AngleDeg, NumberStyles.Float, CultureInfo.InvariantCulture, out var deg))
                    {
                        _customRotateDeg = deg;
                        tips.SetToolTip(btn, $"Rotate custom ({deg:0.#}°)");
                        foreach (var rb in rotationButtons) DeactivateButton(rb);
                        tag.Active = true;
                    }
                    else tag.Active = false;
                }
                UpdateButtonState(btn, tag);
                UpdatePreviewTransform();
                return;
            }

            if (rotationButtons.Contains(btn))
            {
                if (tag.Active) tag.Active = false;
                else
                {
                    foreach (var b in rotationButtons) DeactivateButton(b);
                    tag.Active = true;
                }
                UpdatePreviewTransform();
            }
            else if (tag.FileName.Contains("Flip"))
            {
                tag.Active = !tag.Active;
                UpdatePreviewTransform();
            }
            else if (bigAudioButtons.Contains(btn))
            {
                if (tag.Active) tag.Active = false;
                else
                {
                    foreach (var b in bigAudioButtons) DeactivateButton(b);
                    tag.Active = true;
                }
                if (tag.FileName.Contains("Mute") && tag.Active)
                    foreach (var b in volumeButtons) DeactivateButton(b);
            }
            else if (volumeButtons.Contains(btn))
            {
                if (bigAudioButtons.Any(b => ((PngIconTag)b.Tag!).Active && ((PngIconTag)b.Tag!).FileName.Contains("Mute"))) return;

                if (tag.Active) tag.Active = false;
                else
                {
                    bool isUp = tag.FileName.Contains("Up");
                    bool oppositeActive = volumeButtons.Any(b => ((PngIconTag)b.Tag!).Active && ((PngIconTag)b.Tag!).FileName.Contains(isUp ? "Down" : "Up"));
                    if (oppositeActive)
                    {
                        foreach (var b in volumeButtons) DeactivateButton(b);
                        tag.Active = true;
                    }
                    else tag.Active = true;
                }
            }

            UpdateButtonState(btn, tag);
        }

        private void DeactivateButton(Button b)
        {
            var t = (PngIconTag)b.Tag!;
            t.Active = false;
            UpdateButtonState(b, t);
        }

        private void UpdateButtonState(Button b, PngIconTag t)
        {
            b.Tag = t;
            b.BackColor = t.Active ? Theme.Accent : Theme.CardBg;
            b.FlatAppearance.BorderColor = t.Active ? Theme.Accent : Theme.CardBgHover;
            b.FlatAppearance.MouseOverBackColor = t.Active ? Theme.Accent : Theme.CardBgHover;
            b.FlatAppearance.MouseDownBackColor = t.Active ? Theme.Accent : Theme.CardActive;

            float unit = grid.RowStyles[0].Height;
            int target = t.Big ? (int)(unit * 2) - 28 : (int)unit - 18;
            if (target < 24) target = 24;
            b.Image = PngIconService.Render(t.FileName, t.Active ? Color.White : Color.Gainsboro, target, target, padding: 8);
        }

        private async Task<bool> ProcessAllActionsAsync(string inputPath, string scaleFilter = "", RenderProgressDialog? progressDlg = null)
        {
            var options = new VideoProcessingOptions
            {
                InputPath = inputPath,
                ScaleFilter = scaleFilter,
                Quality = _selectedQuality,
                CustomRotationDegrees = _customRotateDeg,
                SpeedMultiplier = _speedMultiplier,
                TotalDuration = _videoDuration
            };

            if (rotationButtons.Any(b => ((PngIconTag)b.Tag!).Active))
            {
                var active = rotationButtons.First(b => ((PngIconTag)b.Tag!).Active);
                var tag = (PngIconTag)active.Tag!;
                if (tag.FileName.Contains("90")) options.Rotate90 = true;
                else if (tag.FileName.Contains("180")) options.Rotate180 = true;
                else if (tag.FileName.Contains("270")) options.Rotate270 = true;
                else if (tag.FileName.Contains("Custom")) options.RotateCustom = true;
            }

            foreach (var b in grid.Controls.OfType<Button>().Where(b => b.Tag is PngIconTag t && t.Active))
            {
                var t = (PngIconTag)b.Tag!;
                if (t.FileName.Contains("Flip_Horizontal")) options.FlipHorizontal = true;
                else if (t.FileName.Contains("Flip_Vertical")) options.FlipVertical = true;
            }

            bool muteActive = bigAudioButtons.Any(b => ((PngIconTag)b.Tag!).Active && ((PngIconTag)b.Tag!).FileName.Contains("Mute"));
            options.Mute = muteActive;
            options.StereoToMono = bigAudioButtons.Any(b => ((PngIconTag)b.Tag!).Active && ((PngIconTag)b.Tag!).FileName.Contains("Stereot2mono"));

            if (!muteActive)
            {
                foreach (var b in volumeButtons.Where(b => ((PngIconTag)b.Tag!).Active))
                {
                    var t = (PngIconTag)b.Tag!;
                    if (t.FileName.Contains("50_Up")) options.VolumeAdjustmentsDb.Add(12f);
                    if (t.FileName.Contains("25_Up")) options.VolumeAdjustmentsDb.Add(6f);
                    if (t.FileName.Contains("25_Down")) options.VolumeAdjustmentsDb.Add(-6f);
                    if (t.FileName.Contains("50_Down")) options.VolumeAdjustmentsDb.Add(-12f);
                }
            }

            return await _videoService.ProcessVideoAsync(options, progressDlg);
        }
        public void LoadVideo(string filePath, bool silent = false)
        {
            LoadVideoFile(filePath, notifyVideoLoaded: !silent);
        }
    }
}
