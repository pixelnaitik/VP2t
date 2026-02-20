using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using VPT.Core;
using VPT.Services;
using VPT.Forms;

namespace VPT.Forms.Controls
{
    public partial class WatermarkControl : UserControl
    {
        private string? _watermarkPath;
        private Image? _watermarkImage;
        private Label watermarkFileLabel = null!;
        private ComboBox watermarkPosCombo = null!;
        // Settings
        private CustomSlider opacitySlider = null!;
        private Label opacityLabel = null!;
        private CustomSlider scaleSlider = null!;
        private Label scaleLabel = null!;
        private CustomSlider fontSizeSlider = null!;
        private Label fontSizeLabel = null!;
        private TextBox watermarkTextBox = null!;

        private PictureBox previewBox = null!;
        private Label dropOverlay = null!;
        private Label fileLabel = null!;
        private Button applyBtn = null!;

        private string? _inputFile;
        private double _videoDuration = 0;
        private int _videoWidth = 1920, _videoHeight = 1080;
        private Image? _thumbnailImage;

        private enum DragMode { None, Move, ResizeTL, ResizeTR, ResizeBL, ResizeBR }
        private DragMode _dragMode = DragMode.None;
        private Point _dragOffset;
        private PointF _wmPositionFraction = new PointF(0.85f, 0.85f);
        private bool _useCustomPosition = false;
        private const int HandleSize = 10;

        private readonly VideoProcessingService _videoService;
        private readonly Form _parentForm;
        public event Action<string>? VideoLoaded;

        public WatermarkControl(Form parentForm, VideoProcessingService videoService)
        {
            _parentForm = parentForm;
            _videoService = videoService;
            Dock = DockStyle.Fill;
            BackColor = Theme.Bg;
            ForeColor = Theme.Fg;
            BuildUI();
        }

        private void BuildUI()
        {
            // Root
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, BackColor = Theme.Bg };
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 64f));
            Controls.Add(root);

            // Top
            var top = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, BackColor = Theme.Bg, Padding = new Padding(8) };
            top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55f));
            top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45f));
            root.Controls.Add(top, 0, 0);

            // Left Preview
            var previewPanel = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(12, 14, 18), Margin = new Padding(0, 0, 6, 0) };
            top.Controls.Add(previewPanel, 0, 0);

            previewBox = new PictureBox { Dock = DockStyle.Fill, BackColor = Color.FromArgb(18, 20, 26), SizeMode = PictureBoxSizeMode.Normal };
            previewPanel.Controls.Add(previewBox);
            previewBox.Paint += PreviewBox_Paint;
            previewBox.MouseDown += PreviewBox_MouseDown;
            previewBox.MouseMove += PreviewBox_MouseMove;
            previewBox.MouseUp += PreviewBox_MouseUp;

            dropOverlay = new Label
            {
                Dock = DockStyle.Fill,
                Text = "📂  Drop video here\nor click to browse",
                ForeColor = Theme.Muted,
                BackColor = Color.Transparent,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 14),
                Cursor = Cursors.Hand
            };
            previewBox.Controls.Add(dropOverlay);
            dropOverlay.Click += (s, e) =>
            {
                var ofd = new OpenFileDialog { Title = "Select video", Filter = "Video Files|*.mp4;*.mov;*.avi;*.mkv;*.webm;*.wmv|All Files|*.*" };
                if (ofd.ShowDialog(this) == DialogResult.OK) LoadVideo(ofd.FileName);
            };
            previewPanel.AllowDrop = true;
            previewPanel.DragEnter += (s, e) => { if (e.Data?.GetDataPresent(DataFormats.FileDrop) == true) e.Effect = DragDropEffects.Copy; };
            previewPanel.DragDrop += (s, e) => { if (e.Data?.GetData(DataFormats.FileDrop) is string[] f && f.Length > 0) LoadVideo(f[0]); };

            // Right Scroll Panel (Container for FlowLayout)
            var rightContainer = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Theme.Bg,
                AutoScroll = true
            };
            top.Controls.Add(rightContainer, 1, 0);

            // Flow Layout Panel
            var flowPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                Padding = new Padding(8, 4, 14, 8)
            };
            rightContainer.Controls.Add(flowPanel);

            // Allow flow panel to be resized to container width
            rightContainer.Resize += (s, e) =>
            {
                flowPanel.Width = rightContainer.ClientSize.Width;
                foreach (Control c in flowPanel.Controls) c.Width = flowPanel.ClientSize.Width - flowPanel.Padding.Horizontal - c.Margin.Horizontal;
            };

            // ── Title ──
            var titleLbl = new Label
            {
                Text = "💧  Watermark",
                Font = new Font("Segoe UI", 14, FontStyle.Bold),
                ForeColor = Theme.Fg,
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 8)
            };
            flowPanel.Controls.Add(titleLbl);

            var descLbl = new Label
            {
                Text = "Add image or text watermark.\nDrag on preview to reposition.",
                Font = new Font("Segoe UI", 9f),
                ForeColor = Theme.Muted,
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 16)
            };
            flowPanel.Controls.Add(descLbl);

            fileLabel = new Label
            {
                Text = "",
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                ForeColor = Theme.Accent,
                AutoSize = true,
                Visible = false,
                Margin = new Padding(0, 0, 0, 8)
            };
            flowPanel.Controls.Add(fileLabel);

            // ── IMAGE WATERMARK ──
            AddHeader(flowPanel, "IMAGE WATERMARK");

            var wmSelectBtn = new Button
            {
                Text = "  📁  Select Image",
                Height = 36,
                FlatStyle = FlatStyle.Flat,
                BackColor = Theme.CardBg,
                ForeColor = Theme.Fg,
                Font = new Font("Segoe UI", 9),
                Cursor = Cursors.Hand,
                TextAlign = ContentAlignment.MiddleLeft,
                Margin = new Padding(0, 0, 0, 8)
            };
            wmSelectBtn.FlatAppearance.BorderColor = Theme.BorderColor;
            wmSelectBtn.FlatAppearance.BorderSize = 1;
            wmSelectBtn.FlatAppearance.MouseOverBackColor = Theme.CardBgHover;
            wmSelectBtn.Click += BtnSelectImage_Click;
            flowPanel.Controls.Add(wmSelectBtn);

            watermarkFileLabel = new Label
            {
                Text = "None",
                AutoSize = true,
                ForeColor = Theme.Muted,
                Font = new Font("Segoe UI", 8),
                Margin = new Padding(4, 0, 0, 24)
            };
            flowPanel.Controls.Add(watermarkFileLabel);

            // ── TEXT WATERMARK ──
            AddHeader(flowPanel, "TEXT WATERMARK");

            watermarkTextBox = new TextBox
            {
                Height = 30, // TextBox autosize usually ignores this but good for placeholder
                BackColor = Theme.CardBg,
                ForeColor = Theme.Fg,
                Font = new Font("Segoe UI", 10),
                BorderStyle = BorderStyle.FixedSingle,
                PlaceholderText = "Enter watermark text...",
                Margin = new Padding(0, 0, 0, 12)
            };
            watermarkTextBox.TextChanged += (s, e) =>
            {
                if (!string.IsNullOrEmpty(watermarkTextBox.Text))
                {
                    _watermarkPath = null; watermarkFileLabel.Text = "None";
                    GenerateTextWatermarkPreview();
                }
                else if (_watermarkPath == null) { _watermarkImage?.Dispose(); _watermarkImage = null; }
                RefreshPreview();
            };
            flowPanel.Controls.Add(watermarkTextBox);

            // Font size
            var fontSizePanel = new Panel { Height = 40, Margin = new Padding(0, 0, 0, 24) };

            fontSizeLabel = new Label
            {
                Text = "36px",
                AutoSize = true,
                ForeColor = Theme.Muted,
                Font = new Font("Segoe UI", 9),
                Location = new Point(0, 8)
            };
            fontSizePanel.Controls.Add(fontSizeLabel);

            // Font size slider
            fontSizeSlider = new CustomSlider
            {
                Width = 300,
                Margin = new Padding(0, 10, 0, 5),
                Min = 12,
                Max = 120,
                Value = 36
            };
            fontSizeSlider.ValueChanged += (s, e) =>
            {
                fontSizeLabel.Text = $"{fontSizeSlider.Value}px";
                if (!string.IsNullOrEmpty(watermarkTextBox.Text)) { GenerateTextWatermarkPreview(); RefreshPreview(); }
            };

            // Update width manually on resize in parent
            fontSizePanel.Resize += (s, e) => fontSizeSlider.Width = fontSizePanel.Width - fontSizeLabel.Width - 10; // Adjusted width calculation
            fontSizePanel.Controls.Add(fontSizeSlider);

            flowPanel.Controls.Add(fontSizePanel);

            var advancedToggle = new CustomCheckBox
            {
                Text = "Show advanced controls",
                Checked = false,
                Width = 220,
                Margin = new Padding(0, 0, 0, 12)
            };
            flowPanel.Controls.Add(advancedToggle);

            // ── POSITION ──
            AddHeader(flowPanel, "POSITION");

            watermarkPosCombo = new ComboBox
            {
                Height = 32,
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Theme.CardBg,
                ForeColor = Theme.Fg,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9),
                Margin = new Padding(0, 0, 0, 24)
            };
            watermarkPosCombo.Items.AddRange(new object[] { "TopLeft", "TopRight", "Center", "BottomLeft", "BottomRight", "Custom (Drag)" });
            watermarkPosCombo.SelectedIndex = 4;
            watermarkPosCombo.SelectedIndexChanged += PosCombo_Changed;
            flowPanel.Controls.Add(watermarkPosCombo);
            watermarkPosCombo.Visible = false;

            // ── OPACITY ──
            AddHeader(flowPanel, "OPACITY");
            AddSlider(flowPanel, "80%", 5, 100, 80, out opacitySlider, out opacityLabel, (v) => $"{v}%");

            // ── SCALE ──
            AddHeader(flowPanel, "SCALE");
            AddSlider(flowPanel, "15%", 5, 60, 15, out scaleSlider, out scaleLabel, (v) => $"{v}%");

            // Tip
            var tipLbl = new Label
            {
                Text = "💡 Drag watermark to move.\n    Drag corners to resize.",
                Font = new Font("Segoe UI", 8),
                ForeColor = Color.FromArgb(80, 110, 150),
                AutoSize = true,
                Margin = new Padding(0, 16, 0, 16)
            };
            flowPanel.Controls.Add(tipLbl);

            void SetAdvancedVisibility(bool show)
            {
                bool start = false;
                foreach (Control c in flowPanel.Controls)
                {
                    if (ReferenceEquals(c, watermarkPosCombo)) start = true;
                    if (start) c.Visible = show;
                }
            }

            advancedToggle.CheckedChanged += (s, e) => SetAdvancedVisibility(advancedToggle.Checked);
            SetAdvancedVisibility(advancedToggle.Checked);

            // Bottom Bar
            var bottomBar = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Bg, Padding = new Padding(16, 8, 16, 8) };
            root.Controls.Add(bottomBar, 0, 1);

            applyBtn = new Button
            {
                Text = "  🎬  Render",
                Dock = DockStyle.Right,
                Width = 220,
                Height = 44,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI Semibold", 12, FontStyle.Bold),
                BackColor = Theme.Accent,
                ForeColor = Color.White,
                Cursor = Cursors.Hand
            };
            applyBtn.FlatAppearance.BorderSize = 0;
            applyBtn.FlatAppearance.MouseOverBackColor = Theme.AccentHover;
            applyBtn.Click += (s, e) => AddToQueueAction();
            applyBtn.Resize += (s, e) => UiUtils.ApplyRounded(applyBtn, 8);
            bottomBar.Controls.Add(applyBtn);
        }

        private void AddHeader(FlowLayoutPanel p, string text)
        {
            p.Controls.Add(new Label
            {
                Text = text,
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(100, 130, 170),
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 8)
            });
        }

        private void AddSlider(FlowLayoutPanel p, string initLabel, int min, int max, int val, out CustomSlider slider, out Label valLabel, Func<int, string> formatter)
        {
            var container = new Panel { Height = 40, Margin = new Padding(0, 0, 0, 24) };

            var lbl = new Label
            {
                Text = initLabel,
                AutoSize = true,
                ForeColor = Theme.Fg,
                Font = new Font("Segoe UI", 9),
                TextAlign = ContentAlignment.TopRight,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            valLabel = lbl; // assign out param

            var customSlider = new CustomSlider
            {
                Height = 30,
                Min = min,
                Max = max,
                Value = val,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            customSlider.ValueChanged += (s, e) =>
            {
                lbl.Text = formatter(customSlider.Value);
                RefreshPreview();
            };
            slider = customSlider; // assign out param

            // Layout in container
            container.Resize += (s, e) =>
            {
                lbl.Location = new Point(container.Width - lbl.Width, 5);
                customSlider.Width = container.Width - lbl.Width - 10;
            };

            container.Controls.Add(customSlider);
            container.Controls.Add(lbl);
            p.Controls.Add(container);
        }

        // ─── Event Handlers (extracted to avoid lambda focus issues) ───

        private void BtnSelectImage_Click(object? sender, EventArgs e)
        {
            var ofd = new OpenFileDialog { Filter = "Images|*.png;*.jpg;*.jpeg;*.bmp;*.gif" };
            if (ofd.ShowDialog(this) == DialogResult.OK)
            {
                _watermarkPath = ofd.FileName;
                watermarkFileLabel.Text = Path.GetFileName(_watermarkPath);
                watermarkTextBox.Text = "";
                try { _watermarkImage?.Dispose(); _watermarkImage = Image.FromFile(_watermarkPath); } catch (Exception ex) { Logger.Error("Failed to load watermark image", ex); _watermarkImage = null; }
                RefreshPreview();
            }
        }

        private void GenerateTextWatermarkPreview()
        {
            if (string.IsNullOrEmpty(watermarkTextBox.Text))
            {
                _watermarkImage = null;
                return;
            }

            try
            {
                int fontSize = fontSizeSlider.Value * 3;
                Font font = new Font("Arial", fontSize, FontStyle.Bold);

                // Measure string
                using Bitmap tempBmp = new Bitmap(1, 1);
                using Graphics tg = Graphics.FromImage(tempBmp);
                SizeF size = tg.MeasureString(watermarkTextBox.Text, font);

                int w = (int)Math.Ceiling(size.Width);
                int h = (int)Math.Ceiling(size.Height);
                if (w == 0 || h == 0) return;

                Bitmap bmp = new Bitmap(w, h);
                using Graphics g = Graphics.FromImage(bmp);
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;

                // Draw Text
                using SolidBrush textBrush = new SolidBrush(Color.White);
                using Pen outlinePen = new Pen(Color.Black, Math.Max(1, fontSize / 15f)) { LineJoin = LineJoin.Round };
                using GraphicsPath path = new GraphicsPath();

                path.AddString(watermarkTextBox.Text, font.FontFamily, (int)font.Style, g.DpiY * font.Size / 72, new Point(0, 0), StringFormat.GenericDefault);
                g.DrawPath(outlinePen, path);
                g.FillPath(textBrush, path);

                _watermarkImage?.Dispose();
                _watermarkImage = bmp;
            }
            catch (Exception ex)
            {
                Logger.Error("Error generating text watermark", ex);
            }
        }

        private void RefreshPreview()
        {
            previewBox.Invalidate();
        }

        private void PosCombo_Changed(object? sender, EventArgs e)
        {
            string sel = watermarkPosCombo.SelectedItem?.ToString() ?? "";
            _useCustomPosition = sel == "Custom (Drag)";
            if (!_useCustomPosition)
            {
                _wmPositionFraction = sel switch
                {
                    "TopLeft" => new PointF(0.02f, 0.02f),
                    "TopRight" => new PointF(0.85f, 0.02f),
                    "Center" => new PointF(0.42f, 0.42f),
                    "BottomLeft" => new PointF(0.02f, 0.85f),
                    _ => new PointF(0.85f, 0.85f)
                };
            }
            RefreshPreview();
        }

        // ─── Section header helper ───


        // ─── Text Watermark Preview ───
        // This method is now replaced by the new GenerateTextWatermarkPreview above.

        // ─── Preview Painting ───
        private Rectangle GetVideoRect()
        {
            if (_thumbnailImage == null) return previewBox.ClientRectangle;
            float s = Math.Min((float)previewBox.ClientSize.Width / _thumbnailImage.Width,
                               (float)previewBox.ClientSize.Height / _thumbnailImage.Height);
            int dw = (int)(_thumbnailImage.Width * s), dh = (int)(_thumbnailImage.Height * s);
            return new Rectangle((previewBox.ClientSize.Width - dw) / 2, (previewBox.ClientSize.Height - dh) / 2, dw, dh);
        }

        private Rectangle GetWatermarkRect()
        {
            if (_watermarkImage == null) return Rectangle.Empty;
            var vr = GetVideoRect();
            float sp = scaleSlider.Value / 100f;
            int wmW = (int)(vr.Width * sp);
            int wmH = _watermarkImage.Width > 0 ? (int)(wmW * ((float)_watermarkImage.Height / _watermarkImage.Width)) : wmW;
            int wmX = vr.X + (int)(_wmPositionFraction.X * (vr.Width - wmW));
            int wmY = vr.Y + (int)(_wmPositionFraction.Y * (vr.Height - wmH));
            wmX = Math.Max(vr.X, Math.Min(wmX, vr.Right - wmW));
            wmY = Math.Max(vr.Y, Math.Min(wmY, vr.Bottom - wmH));
            return new Rectangle(wmX, wmY, Math.Max(1, wmW), Math.Max(1, wmH));
        }

        private Rectangle[] GetCornerHandles(Rectangle r)
        {
            int s = HandleSize;
            return new[] {
                new Rectangle(r.Left - s/2, r.Top - s/2, s, s),
                new Rectangle(r.Right - s/2, r.Top - s/2, s, s),
                new Rectangle(r.Left - s/2, r.Bottom - s/2, s, s),
                new Rectangle(r.Right - s/2, r.Bottom - s/2, s, s)
            };
        }

        private void PreviewBox_Paint(object? sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.HighQuality;
            g.InterpolationMode = InterpolationMode.HighQualityBilinear;
            g.Clear(Color.FromArgb(18, 20, 26));
            if (_thumbnailImage == null) return;

            var vr = GetVideoRect();
            g.DrawImage(_thumbnailImage, vr);
            if (_watermarkImage == null) return;

            var wmr = GetWatermarkRect();
            using var ia = new System.Drawing.Imaging.ImageAttributes();
            var cm = new System.Drawing.Imaging.ColorMatrix { Matrix33 = opacitySlider.Value / 100f };
            ia.SetColorMatrix(cm);
            g.DrawImage(_watermarkImage, wmr, 0, 0, _watermarkImage.Width, _watermarkImage.Height, GraphicsUnit.Pixel, ia);

            // Corner handles
            var handles = GetCornerHandles(wmr);
            using var hb = new SolidBrush(Color.White);
            using var hp = new Pen(Theme.Accent, 1.5f);
            foreach (var h in handles) { g.FillRectangle(hb, h); g.DrawRectangle(hp, h); }

            if (_useCustomPosition || _dragMode != DragMode.None)
            {
                using var bp = new Pen(Color.FromArgb(120, 56, 189, 126), 1.2f) { DashStyle = DashStyle.Dash };
                g.DrawRectangle(bp, wmr);
            }
        }

        // ─── Mouse Handling ───
        private void PreviewBox_MouseDown(object? sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left || _watermarkImage == null) return;
            var wmr = GetWatermarkRect();
            var h = GetCornerHandles(wmr);
            if (h[0].Contains(e.Location)) { BeginDrag(DragMode.ResizeTL, e); return; }
            if (h[1].Contains(e.Location)) { BeginDrag(DragMode.ResizeTR, e); return; }
            if (h[2].Contains(e.Location)) { BeginDrag(DragMode.ResizeBL, e); return; }
            if (h[3].Contains(e.Location)) { BeginDrag(DragMode.ResizeBR, e); return; }
            if (wmr.Contains(e.Location))
            {
                EnsureCustomMode();
                _dragMode = DragMode.Move;
                _dragOffset = new Point(e.X - wmr.X, e.Y - wmr.Y);
                previewBox.Cursor = Cursors.SizeAll;
            }
        }

        private void BeginDrag(DragMode mode, MouseEventArgs e)
        {
            EnsureCustomMode();
            _dragMode = mode;
            _dragOffset = e.Location;
            previewBox.Cursor = mode is DragMode.ResizeTL or DragMode.ResizeBR ? Cursors.SizeNWSE : Cursors.SizeNESW;
        }

        private void EnsureCustomMode()
        {
            if (!_useCustomPosition) { _useCustomPosition = true; watermarkPosCombo.SelectedIndex = watermarkPosCombo.Items.Count - 1; }
        }

        private void PreviewBox_MouseMove(object? sender, MouseEventArgs e)
        {
            if (_dragMode == DragMode.None)
            {
                if (_watermarkImage != null)
                {
                    var wmr = GetWatermarkRect();
                    var h = GetCornerHandles(wmr);
                    if (h[0].Contains(e.Location) || h[3].Contains(e.Location)) previewBox.Cursor = Cursors.SizeNWSE;
                    else if (h[1].Contains(e.Location) || h[2].Contains(e.Location)) previewBox.Cursor = Cursors.SizeNESW;
                    else if (wmr.Contains(e.Location)) previewBox.Cursor = Cursors.SizeAll;
                    else previewBox.Cursor = Cursors.Default;
                }
                return;
            }
            if (_watermarkImage == null) return;

            if (_dragMode == DragMode.Move)
            {
                var vr = GetVideoRect(); var wmr = GetWatermarkRect();
                int maxX = vr.Width - wmr.Width, maxY = vr.Height - wmr.Height;
                float fx = maxX > 0 ? (float)(e.X - _dragOffset.X - vr.X) / maxX : 0f;
                float fy = maxY > 0 ? (float)(e.Y - _dragOffset.Y - vr.Y) / maxY : 0f;
                _wmPositionFraction = new PointF(Math.Clamp(fx, 0f, 1f), Math.Clamp(fy, 0f, 1f));
            }
            else
            {
                var vr = GetVideoRect();
                int dx = e.X - _dragOffset.X, dy = e.Y - _dragOffset.Y;
                _dragOffset = e.Location;
                int delta = _dragMode switch
                {
                    DragMode.ResizeBR => Math.Max(dx, dy),
                    DragMode.ResizeTL => -Math.Max(dx, dy),
                    DragMode.ResizeTR => Math.Max(dx, -dy),
                    DragMode.ResizeBL => Math.Max(-dx, dy),
                    _ => 0
                };
                if (vr.Width > 0)
                {
                    float sd = (float)delta / vr.Width * 100f;
                    scaleSlider.Value = Math.Clamp(scaleSlider.Value + (int)sd, scaleSlider.Min, scaleSlider.Max);
                }
            }
            previewBox.Invalidate();
        }

        private void PreviewBox_MouseUp(object? sender, MouseEventArgs e)
        {
            if (_dragMode != DragMode.None) { _dragMode = DragMode.None; previewBox.Cursor = Cursors.Default; }
        }

        // ─── Public Methods ───
        public void LoadVideo(string filePath, bool silent = false)
        {
            _inputFile = filePath;
            if (!silent) VideoLoaded?.Invoke(filePath);
            dropOverlay.Visible = false;
            fileLabel.Text = $"📄 {Path.GetFileName(filePath)}";
            fileLabel.ForeColor = Theme.Accent;
            fileLabel.Visible = true;

            Task.Run(() =>
            {
                try
                {
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
                    if (lines.Length >= 1) int.TryParse(lines[0], out _videoWidth);
                    if (lines.Length >= 2) int.TryParse(lines[1], out _videoHeight);
                    if (lines.Length >= 3) double.TryParse(lines[2], NumberStyles.Float, CultureInfo.InvariantCulture, out _videoDuration);

                    string ffmpeg = _videoService.ExtractFfmpegTool("ffmpeg.exe");
                    string tempThumb = Path.Combine(Path.GetTempPath(), $"vpt_wm_{Guid.NewGuid()}.jpg");
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
                            _thumbnailImage?.Dispose();
                            using var fs = new FileStream(tempThumb, FileMode.Open, FileAccess.Read);
                            _thumbnailImage = Image.FromStream(fs);
                            previewBox.Invalidate();
                        }
                    }));
                }
                catch (Exception ex) { Logger.Error("Failed to load video info for watermark", ex); }
            });
        }

        private void AddToQueueAction()
        {
            if (string.IsNullOrEmpty(_inputFile))
            {
                MessageBox.Show("Please select a video file first.", "No File", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            bool hasImage = !string.IsNullOrEmpty(_watermarkPath) && File.Exists(_watermarkPath);
            bool hasText = !string.IsNullOrEmpty(watermarkTextBox.Text);
            if (!hasImage && !hasText)
            {
                MessageBox.Show("Please select a watermark image or enter watermark text.", "No Watermark", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            using var qualityDlg = new RenderQualityDialog(_parentForm);
            if (qualityDlg.ShowDialog(_parentForm) != DialogResult.OK) return;

            string posName; float wmXpx = 0, wmYpx = 0;
            if (_useCustomPosition)
            {
                posName = "Custom";
                float sp = scaleSlider.Value / 100f;
                int wmW = (int)(_videoWidth * sp);
                int wmH = _watermarkImage != null && _watermarkImage.Width > 0
                    ? (int)(wmW * ((float)_watermarkImage.Height / _watermarkImage.Width)) : wmW;
                wmXpx = _wmPositionFraction.X * (_videoWidth - wmW);
                wmYpx = _wmPositionFraction.Y * (_videoHeight - wmH);
            }
            else
            {
                posName = watermarkPosCombo.SelectedItem?.ToString() ?? "BottomRight";
                if (posName == "Custom (Drag)") posName = "BottomRight";
            }

            var options = new VideoProcessingOptions
            {
                InputPath = _inputFile,
                Quality = qualityDlg.SelectedQuality,
                ScaleFilter = qualityDlg.GetScaleFilter(),
                TrimStart = TimeSpan.Zero,
                TrimEnd = TimeSpan.FromSeconds(_videoDuration),
                TotalDuration = _videoDuration,
                WatermarkPath = hasImage ? _watermarkPath! : "",
                WatermarkText = hasText ? watermarkTextBox.Text : "",
                WatermarkFontSize = fontSizeSlider.Value,
                WatermarkPosition = posName,
                WatermarkOpacity = opacitySlider.Value / 100f,
                WatermarkScale = scaleSlider.Value / 100f,
                WatermarkX = wmXpx,
                WatermarkY = wmYpx
            };
            if (_parentForm is Form1 f1) f1.AddToBatch(_inputFile, options);
        }
    }
}
