using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using VPT.Controls;
using VPT.Core;
using VPT.Forms.Controls;
using VPT.Services;

namespace VPT.Forms
{
    public partial class Form1 : Form
    {
        private readonly VideoProcessingService _videoService = new();
        private DarkTabControl tabs = null!;

        // Controls
        private SingleClicksControl singleClicks = null!;
        private CropTrimControl cropTrim = null!;
        private TranscodeControl transcode = null!;
        private WatermarkControl watermark = null!;
        private BatchQueueControl batchQueue = null!;
        private readonly Queue<(string InputPath, VideoProcessingOptions Options)> _renderQueue = new();
        private bool _isProcessingQueue;

        public Form1()
        {
            InitializeComponent();

            this.Text = "Video Processing Tool";
            this.MinimumSize = new Size(1000, 800); // Increased height for queue
            this.StartPosition = FormStartPosition.CenterScreen;
            this.KeyPreview = true;

            // Set App Icon
            LoadAppIcon();

            // Enable resizing and custom title bar
            this.FormBorderStyle = FormBorderStyle.None;
            this.DoubleBuffered = true;
            this.SetStyle(ControlStyles.ResizeRedraw, true);

            EnableDarkTitleBar();

            // Setup UI
            BuildTabs();

            // Debug probe for icons (dev only)
            DebugIconProbe();
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            _videoService.EnsureFfmpegInstalled(this);
        }

        private void LoadAppIcon()
        {
            try
            {
                string iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico");
                if (File.Exists(iconPath))
                {
                    this.Icon = new System.Drawing.Icon(iconPath);
                    return;
                }
                string devPath = Path.Combine(Directory.GetCurrentDirectory(), "Assets", "AppIcon.ico");
                if (File.Exists(devPath)) this.Icon = new System.Drawing.Icon(devPath);
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to load application icon", ex);
            }
        }

        private void BuildTabs()
        {
            // Custom Title Bar Area
            var titleBar = new Panel { Dock = DockStyle.Top, Height = 40, BackColor = Theme.Bg };

            var iconBox = new PictureBox
            {
                Image = this.Icon?.ToBitmap(),
                SizeMode = PictureBoxSizeMode.Zoom,
                Size = new Size(24, 24),
                Location = new Point(12, 8)
            };
            titleBar.Controls.Add(iconBox);

            var titleLabel = new Label
            {
                Text = "Video Processing Tool",
                ForeColor = Theme.Fg,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(44, 10)
            };
            titleBar.Controls.Add(titleLabel);

            // Window Controls (left-to-right: Minimize, Maximize, Close)
            var minBtn = new Button
            {
                Text = "─",
                ForeColor = Theme.Muted,
                FlatStyle = FlatStyle.Flat,
                FlatAppearance = { BorderSize = 0, MouseOverBackColor = Theme.CardBgHover },
                Size = new Size(46, 40),
                Dock = DockStyle.Right,
                Cursor = Cursors.Hand
            };
            minBtn.Click += (s, e) => this.WindowState = FormWindowState.Minimized;
            titleBar.Controls.Add(minBtn);

            var maxBtn = new Button
            {
                Text = "☐",
                ForeColor = Theme.Muted,
                FlatStyle = FlatStyle.Flat,
                FlatAppearance = { BorderSize = 0, MouseOverBackColor = Theme.CardBgHover },
                Size = new Size(46, 40),
                Dock = DockStyle.Right,
                Cursor = Cursors.Hand
            };
            maxBtn.Click += (s, e) => ToggleMaximize(maxBtn);
            titleBar.Controls.Add(maxBtn);

            var closeBtn = new Button
            {
                Text = "✕",
                ForeColor = Theme.Muted,
                FlatStyle = FlatStyle.Flat,
                FlatAppearance = { BorderSize = 0, MouseOverBackColor = Color.FromArgb(232, 17, 35) },
                Size = new Size(46, 40),
                Dock = DockStyle.Right,
                Cursor = Cursors.Hand
            };
            closeBtn.Click += (s, e) => Application.Exit();
            titleBar.Controls.Add(closeBtn);

            // Drag Move Logic
            bool dragging = false;
            Point dragCursorPoint = Point.Empty;
            Point dragFormPoint = Point.Empty;

            titleBar.MouseDown += (s, e) =>
            {
                dragging = true;
                dragCursorPoint = Cursor.Position;
                dragFormPoint = this.Location;
            };
            titleBar.MouseMove += (s, e) =>
            {
                if (dragging)
                {
                    Point dif = Point.Subtract(Cursor.Position, new Size(dragCursorPoint));
                    this.Location = Point.Add(dragFormPoint, new Size(dif));
                }
            };
            titleBar.MouseUp += (s, e) => dragging = false;
            titleLabel.MouseDown += (s, e) =>
            {
                dragging = true;
                dragCursorPoint = Cursor.Position;
                dragFormPoint = this.Location;
            };
            titleLabel.MouseMove += (s, e) => { if (dragging) { Point dif = Point.Subtract(Cursor.Position, new Size(dragCursorPoint)); this.Location = Point.Add(dragFormPoint, new Size(dif)); } };
            titleLabel.MouseUp += (s, e) => dragging = false;

            this.Controls.Add(titleBar);

            // --- Layout ---
            // Direct Tabs (No Batch Queue Split)
            tabs = new DarkTabControl(Theme.PanelBg, Theme.Bg, Theme.Fg)
            {
                Dock = DockStyle.Fill,
                SizeMode = TabSizeMode.Fixed,
                Padding = new Point(18, 12),
                ItemSize = new Size(160, 40)
            };

            var tabSingleClicks = new TabPage("Single Clicks") { BackColor = Theme.Bg, ForeColor = Theme.Fg };
            var tabCropTrim = new TabPage("Crop/Trim") { BackColor = Theme.Bg, ForeColor = Theme.Fg };
            var tabWatermark = new TabPage("Watermark") { BackColor = Theme.Bg, ForeColor = Theme.Fg };
            var tabTranscode = new TabPage("Transcode") { BackColor = Theme.Bg, ForeColor = Theme.Fg };

            // Initialize Controls
            singleClicks = new SingleClicksControl(this, _videoService);
            tabSingleClicks.Controls.Add(singleClicks);

            cropTrim = new CropTrimControl(this, _videoService);
            tabCropTrim.Controls.Add(cropTrim);

            watermark = new WatermarkControl(this, _videoService);
            tabWatermark.Controls.Add(watermark);

            transcode = new TranscodeControl(this, _videoService);
            tabTranscode.Controls.Add(transcode);

            tabs.TabPages.Add(tabSingleClicks);
            tabs.TabPages.Add(tabCropTrim);
            tabs.TabPages.Add(tabWatermark);
            tabs.TabPages.Add(tabTranscode);

            this.Controls.Add(tabs);
            tabs.BringToFront();

            // Sync Logic
            singleClicks.VideoLoaded += (path) => SyncLoad(path, singleClicks);
            cropTrim.VideoLoaded += (path) => SyncLoad(path, cropTrim);
            watermark.VideoLoaded += (path) => SyncLoad(path, watermark);
            transcode.VideoLoaded += (path) => SyncLoad(path, transcode);

            // Remove Batch Queue initialization
            // batchQueue = new BatchQueueControl(this, _videoService);
            // batchQueue is removed from UI
        }

        private void SyncLoad(string path, Control source)
        {
            if (source != singleClicks) singleClicks.LoadVideo(path, true);
            if (source != cropTrim) cropTrim.LoadVideo(path, true);
            if (source != watermark) watermark.LoadVideo(path, true);
            if (source != transcode) transcode.LoadVideo(path, true);
        }

        // Public entry point used by tab controls
        public void AddToBatch(string inputPath, VideoProcessingOptions options)
        {
            options.OutputPathOverride = _videoService.GetPlannedOutputPath(options);

            using var summary = BuildRenderSummaryDialog(options);
            if (summary.ShowDialog(this) != DialogResult.OK) return;

            if (summary.SelectedAction == RenderSummaryAction.AddToQueue)
            {
                _renderQueue.Enqueue((inputPath, options));
                MessageBox.Show($"Added to queue. Pending jobs: {_renderQueue.Count}", "Queued", MessageBoxButtons.OK, MessageBoxIcon.Information);
                if (!_isProcessingQueue) _ = ProcessQueueAsync();
                return;
            }

            _ = ProcessSingleAsync(inputPath, options);
        }

        private RenderSummaryDialog BuildRenderSummaryDialog(VideoProcessingOptions options)
        {
            double durationSeconds = options.TotalDuration > 0
                ? options.TotalDuration
                : Math.Max(0, ((options.TrimEnd ?? TimeSpan.Zero) - (options.TrimStart ?? TimeSpan.Zero)).TotalSeconds);

            if (durationSeconds <= 0 && File.Exists(options.InputPath))
            {
                try
                {
                    durationSeconds = new FileInfo(options.InputPath).Length / (1024d * 1024d * 2d);
                }
                catch (Exception ex) { Logger.Error("Failed to estimate duration from file size fallback", ex); durationSeconds = 0; }
            }

            string videoCodec = VideoProcessingService.GetEffectiveVideoCodec(options);
            string audioCodec = VideoProcessingService.GetEffectiveAudioCodec(options);
            string durationText = durationSeconds > 0 ? TimeSpan.FromSeconds(durationSeconds).ToString(@"hh\:mm\:ss") : "Unknown";
            string estimate = EstimateOutputSize(durationSeconds, options);
            string outputPath = string.IsNullOrWhiteSpace(options.OutputPathOverride)
                ? _videoService.GetPlannedOutputPath(options)
                : options.OutputPathOverride;

            return new RenderSummaryDialog(videoCodec, audioCodec, estimate, durationText, outputPath);
        }

        private static string EstimateOutputSize(double durationSeconds, VideoProcessingOptions options)
        {
            if (durationSeconds <= 0) return "Unknown";

            double videoMbps = options.Quality switch
            {
                "4K" => 20,
                "Original" => 8,
                "High Quality" => 10,
                "Social Media" => 5,
                "Web Optimized" => 2.5,
                _ => 6
            };

            if (!string.IsNullOrWhiteSpace(options.CustomArgs))
            {
                var m = System.Text.RegularExpressions.Regex.Match(options.CustomArgs, @"-b:v\s+(\d+(?:\.\d+)?)([kKmM])");
                if (m.Success)
                {
                    double val = double.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
                    videoMbps = m.Groups[2].Value.Equals("k", StringComparison.OrdinalIgnoreCase) ? val / 1000d : val;
                }
            }

            double audioMbps = options.Mute ? 0 : 0.192;
            double totalMB = durationSeconds * (videoMbps + audioMbps) / 8d;
            return totalMB >= 1024 ? $"~{totalMB / 1024d:0.##} GB" : $"~{totalMB:0} MB";
        }

        private async Task ProcessQueueAsync()
        {
            if (_isProcessingQueue) return;
            _isProcessingQueue = true;
            try
            {
                while (_renderQueue.Count > 0)
                {
                    var next = _renderQueue.Dequeue();
                    await ProcessSingleAsync(next.InputPath, next.Options);
                }
            }
            finally
            {
                _isProcessingQueue = false;
            }
        }

        private async Task ProcessSingleAsync(string inputPath, VideoProcessingOptions options)
        {
            var dlg = new RenderProgressDialog(this) { Text = $"Processing {Path.GetFileName(inputPath)}" };
            dlg.Show(this);

            try
            {
                await _videoService.ProcessVideoAsync(options, dlg);
                MessageBox.Show("Processing Complete!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                dlg.Close();
            }
        }

        // --- WinUI Dark Mode Integration (Windows 11/10) ---------------------
        [System.Runtime.InteropServices.DllImport("DwmApi")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, int[] attrValue, int attrSize);

        private void EnableDarkTitleBar()
        {
            if (Environment.OSVersion.Version.Major >= 10)
            {
                int[] darkMode = { 1 }; // 1 = True
                DwmSetWindowAttribute(Handle, 20, darkMode, 4); // DWMWA_USE_IMMERSIVE_DARK_MODE
            }
        }

        // --- Resize Logic (Simulated) ----------------------------------------
        protected override void WndProc(ref Message m)
        {
            const int WM_NCHITTEST = 0x84;
            const int HTCLIENT = 1;
            const int HTCAPTION = 2;
            const int HTBOTTOMRIGHT = 17;

            if (m.Msg == WM_NCHITTEST)
            {
                base.WndProc(ref m);
                if (m.Result.ToInt32() == HTCLIENT)
                {
                    Point screenPoint = new Point(m.LParam.ToInt32());
                    Point clientPoint = this.PointToClient(screenPoint);
                    if (clientPoint.Y <= 40) m.Result = (IntPtr)HTCAPTION;
                    if (clientPoint.X >= this.ClientSize.Width - 16 && clientPoint.Y >= this.ClientSize.Height - 16)
                        m.Result = (IntPtr)HTBOTTOMRIGHT;
                }
                return;
            }
            base.WndProc(ref m);
        }

        private void ToggleMaximize(Button btn)
        {
            if (WindowState == FormWindowState.Maximized)
            {
                WindowState = FormWindowState.Normal;
                btn.Text = "☐";
            }
            else
            {
                MaximizedBounds = Screen.FromHandle(Handle).WorkingArea;
                WindowState = FormWindowState.Maximized;
                btn.Text = "❐";
            }
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == (Keys.Control | Keys.O))
            {
                using var ofd = new OpenFileDialog
                {
                    Title = "Select video",
                    Filter = "Video Files|*.mp4;*.mov;*.avi;*.mkv;*.webm;*.wmv|All Files|*.*"
                };

                if (ofd.ShowDialog(this) == DialogResult.OK)
                    SyncLoad(ofd.FileName, this);

                return true;
            }

            if (keyData == (Keys.Control | Keys.D1)) { tabs.SelectedIndex = 0; return true; }
            if (keyData == (Keys.Control | Keys.D2)) { tabs.SelectedIndex = 1; return true; }
            if (keyData == (Keys.Control | Keys.D3)) { tabs.SelectedIndex = 2; return true; }
            if (keyData == (Keys.Control | Keys.D4)) { tabs.SelectedIndex = 3; return true; }

            return base.ProcessCmdKey(ref msg, keyData);
        }

        // --- Debug Helpers ---------------------------------------------------
        private void DebugIconProbe()
        {
#if DEBUG
            try
            {
                // Probe logic could go here, omitting for brevity in production refactor
            }
            catch (Exception ex)
            {
                Logger.Error("Debug icon probe failed", ex);
            }
#endif
        }
    }
}
