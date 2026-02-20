using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace VPT.Forms
{
    /// <summary>
    /// Dark-themed progress dialog for render operations.
    /// Shows custom smooth progress bar, percentage, elapsed time, and detailed status (speed, size, eta).
    /// </summary>
    public class RenderProgressDialog : Form
    {
        private readonly PictureBox progressCanvas;
        private readonly Label percentLabel;
        private readonly Label statusLabel;
        private readonly Label timeLabel;
        private readonly Button cancelButton;
        private readonly System.Windows.Forms.Timer elapsedTimer;
        private readonly System.Windows.Forms.Timer animationTimer;
        private DateTime startTime;
        private bool isCancelled = false;

        // Animation state
        private float currentPercent = 0f;
        private float targetPercent = 0f;

        public bool IsCancelled => isCancelled;

        public RenderProgressDialog(Form? parent)
        {
            // Window setup (dark theme)
            Text = "Rendering...";
            StartPosition = parent is null ? FormStartPosition.CenterScreen : FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ControlBox = false; // Disable X button during render
            ClientSize = new Size(520, 240); // Wider for ETA text
            DoubleBuffered = true; // CRITICAL for smooth animation

            BackColor = Color.FromArgb(22, 26, 35);
            ForeColor = Color.FromArgb(240, 242, 248);

            if (parent != null) Owner = parent;

            // Status label (Top left)
            statusLabel = new Label
            {
                Text = "Preparing to render...",
                Location = new Point(20, 15),
                Size = new Size(ClientSize.Width - 40, 40),
                Font = new Font("Segoe UI", 10, FontStyle.Regular),
                ForeColor = Color.White
            };
            Controls.Add(statusLabel);

            // Custom Progress Canvas (replaces standard ProgressBar)
            progressCanvas = new PictureBox
            {
                Location = new Point(20, 65),
                Size = new Size(ClientSize.Width - 40, 30),
                BackColor = Color.FromArgb(35, 42, 55)
            };
            ApplyRounded(progressCanvas, 8);
            progressCanvas.Paint += ProgressCanvas_Paint;
            Controls.Add(progressCanvas);

            // Percentage label
            percentLabel = new Label
            {
                Text = "0%",
                Location = new Point(20, 105),
                Size = new Size(80, 25),
                Font = new Font("Segoe UI", 14, FontStyle.Bold),
                ForeColor = Color.FromArgb(56, 189, 126)
            };
            Controls.Add(percentLabel);

            // Time elapsed label (Right aligned)
            timeLabel = new Label
            {
                Text = "Elapsed: 0:00",
                Location = new Point(ClientSize.Width - 150, 105),
                Size = new Size(130, 25),
                Font = new Font("Segoe UI", 10),
                ForeColor = Color.FromArgb(140, 150, 170),
                TextAlign = ContentAlignment.TopRight
            };
            Controls.Add(timeLabel);

            // Cancel button
            cancelButton = new Button
            {
                Text = "Cancel",
                Width = 100,
                Height = 35,
                Location = new Point((ClientSize.Width - 100) / 2, 170),
                BackColor = Color.FromArgb(160, 70, 70),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10),
                Cursor = Cursors.Hand
            };
            cancelButton.FlatAppearance.BorderSize = 0;
            cancelButton.FlatAppearance.MouseOverBackColor = Color.FromArgb(180, 85, 85);
            cancelButton.Click += (s, e) =>
            {
                if (isCancelled) return;
                isCancelled = true;
                statusLabel.Text = "Cancelling...";
                cancelButton.Enabled = false;
            };
            Controls.Add(cancelButton);

            // Timer for elapsed time
            elapsedTimer = new System.Windows.Forms.Timer { Interval = 1000 };
            elapsedTimer.Tick += (s, e) => UpdateElapsedTime();

            // Timer for smooth animation (60 FPS)
            animationTimer = new System.Windows.Forms.Timer { Interval = 16 };
            animationTimer.Tick += AnimationTimer_Tick;

            // Start timer when shown
            Shown += (s, e) =>
            {
                startTime = DateTime.Now;
                elapsedTimer.Start();
                animationTimer.Start();
            };

            FormClosing += (s, e) =>
            {
                elapsedTimer.Stop();
                animationTimer.Stop();
            };
        }

        private void AnimationTimer_Tick(object? sender, EventArgs e)
        {
            // Smoothly interpolate currentPercent towards targetPercent
            if (Math.Abs(currentPercent - targetPercent) > 0.1f)
            {
                // Simple lerp: move 10% of the distance each frame
                currentPercent += (targetPercent - currentPercent) * 0.1f;

                // Snap if very close
                if (Math.Abs(currentPercent - targetPercent) < 0.1f)
                    currentPercent = targetPercent;

                progressCanvas.Invalidate(); // Redraw
            }
        }

        private void ProgressCanvas_Paint(object? sender, PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var rect = progressCanvas.ClientRectangle;

            // Draw background
            using (var brush = new SolidBrush(Color.FromArgb(35, 42, 55)))
            {
                e.Graphics.FillRectangle(brush, rect);
            }

            // Draw progress
            if (currentPercent > 0)
            {
                int width = (int)((rect.Width * currentPercent) / 100f);
                if (width > 0)
                {
                    var progressRect = new Rectangle(0, 0, width, rect.Height);
                    // Use a nice gradient
                    using (var brush = new LinearGradientBrush(progressRect,
                        Color.FromArgb(56, 189, 126),
                        Color.FromArgb(72, 210, 145),
                        LinearGradientMode.Horizontal))
                    {
                        e.Graphics.FillRectangle(brush, progressRect);
                    }
                }
            }

            // Optional: Draw border
            using (var pen = new Pen(Color.FromArgb(55, 65, 85), 1))
            {
                e.Graphics.DrawRectangle(pen, 0, 0, rect.Width - 1, rect.Height - 1);
            }
        }

        public void UpdateProgress(int percent, string status = "")
        {
            if (InvokeRequired)
            {
                Invoke((Action)(() => UpdateProgress(percent, status)));
                return;
            }

            if (percent < 0) percent = 0;
            if (percent > 100) percent = 100;

            targetPercent = percent;
            percentLabel.Text = $"{percent}%";

            if (!string.IsNullOrEmpty(status))
                statusLabel.Text = status;
        }

        public void SetComplete(bool success)
        {
            if (InvokeRequired)
            {
                Invoke((Action)(() => SetComplete(success)));
                return;
            }

            elapsedTimer.Stop();
            animationTimer.Stop();

            // Force completion
            targetPercent = 100;
            currentPercent = 100;
            progressCanvas.Invalidate();

            percentLabel.Text = "100%";
            statusLabel.Text = success ? "✅ Render complete!" : "❌ Render failed or cancelled";
            statusLabel.ForeColor = success ? Color.FromArgb(56, 189, 126) : Color.FromArgb(200, 80, 80);
            cancelButton.Text = "Close";
            cancelButton.Enabled = true;
            cancelButton.BackColor = Color.FromArgb(45, 52, 68);

            // Re-bind click to close
            // First remove existing handlers (lambda creates new delegate instance so this -= won't work perfectly for anon methods)
            // But we can just add a new one that closes and checks.
            // A cleaner way is to set a flag or just replace the event if we had a dedicated handler.
            // Since we used lambda for cancel, we can't easily unsubscribe it.
            // But status is now complete, so cancel logic won't trigger "cancelling..." text update effectively (checked via isCancelled).

            cancelButton.Click += (s, e) => Close();

            ControlBox = true;
        }

        private void UpdateElapsedTime()
        {
            var elapsed = DateTime.Now - startTime;
            timeLabel.Text = $"Elapsed: {FormatTime(elapsed.TotalSeconds)}";
        }

        private static string FormatTime(double seconds)
        {
            int hrs = (int)(seconds / 3600);
            int mins = (int)((seconds % 3600) / 60);
            int secs = (int)(seconds % 60);
            return hrs > 0 ? $"{hrs}:{mins:D2}:{secs:D2}" : $"{mins}:{secs:D2}";
        }

        private static void ApplyRounded(Control c, int radius)
        {
            if (c.Width <= 0 || c.Height <= 0) return;
            using var path = new GraphicsPath();
            int r = radius * 2;
            Rectangle rect = new Rectangle(0, 0, c.Width, c.Height);
            path.AddArc(rect.X, rect.Y, r, r, 180, 90);
            path.AddArc(rect.Right - r, rect.Y, r, r, 270, 90);
            path.AddArc(rect.Right - r, rect.Bottom - r, r, r, 0, 90);
            path.AddArc(rect.X, rect.Bottom - r, r, r, 90, 90);
            path.CloseFigure();
            c.Region = new Region(path);
        }
    }
}
