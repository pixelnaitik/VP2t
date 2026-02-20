using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using VPT.Core;
using VPT.Services;

namespace VPT.Forms.Controls
{
    public class BatchItem
    {
        public string InputPath { get; set; } = "";
        public VideoProcessingOptions Options { get; set; } = new();
        public string Status { get; set; } = "Pending"; // Pending, Processing, Done, Error, Cancelled
        public string OutputPath { get; set; } = "";
        public string Progress { get; set; } = "0%";
    }

    public partial class BatchQueueControl : UserControl
    {
        private DataGridView grid = null!;
        private Button btnStart = null!;
        private Button btnClear = null!;
        private Button btnRemove = null!;
        private Label statusLabel = null!;

        public List<BatchItem> Queue { get; private set; } = new();
        private readonly VideoProcessingService _videoService;
        private readonly Form _parentForm;

        private bool _isProcessing = false;

        public BatchQueueControl(Form parentForm, VideoProcessingService videoService)
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
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, BackColor = Theme.Bg };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40)); // Header/Tools
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // Grid
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50)); // Bottom Actions
            Controls.Add(layout);

            // Top Toolbar
            var topPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, Padding = new Padding(5) };

            var label = new Label { Text = "Batch Queue", Font = new Font("Segoe UI", 12, FontStyle.Bold), AutoSize = true, ForeColor = Theme.Fg, Margin = new Padding(0, 5, 20, 0) };
            topPanel.Controls.Add(label);

            btnRemove = CreateLinkButton("Remove Selected", (s, e) => RemoveSelected());
            topPanel.Controls.Add(btnRemove);

            btnClear = CreateLinkButton("Clear All", (s, e) => ClearQueue());
            topPanel.Controls.Add(btnClear);

            layout.Controls.Add(topPanel, 0, 0);

            // Grid
            grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                BackgroundColor = Theme.PanelBg,
                ForeColor = Color.Black, // Cell text default
                BorderStyle = BorderStyle.None,
                ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single,
                EnableHeadersVisualStyles = false,
                AllowUserToAddRows = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = true,
                RowHeadersVisible = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
            };

            // Grid Styling
            grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(45, 52, 68);
            grid.ColumnHeadersDefaultCellStyle.ForeColor = Theme.Fg;
            grid.ColumnHeadersDefaultCellStyle.Padding = new Padding(4);
            grid.DefaultCellStyle.BackColor = Theme.CardBg;
            grid.DefaultCellStyle.ForeColor = Theme.Fg;
            grid.DefaultCellStyle.SelectionBackColor = Theme.Accent;
            grid.DefaultCellStyle.SelectionForeColor = Color.White;
            grid.GridColor = Theme.BorderColor;

            // Columns
            grid.Columns.Add("File", "File");
            grid.Columns.Add("Status", "Status");
            grid.Columns.Add("Progress", "Progress");
            grid.Columns.Add("Output", "Output");

            layout.Controls.Add(grid, 0, 1);

            // Bottom Panel
            var bottomPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(10) };

            btnStart = new Button
            {
                Text = "Process Queue",
                AutoSize = true,
                BackColor = Theme.Accent,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnStart.FlatAppearance.BorderSize = 0;
            btnStart.Click += async (s, e) => await ProcessQueueAsync();
            UiUtils.ApplyRounded(btnStart, 6);
            bottomPanel.Controls.Add(btnStart);

            statusLabel = new Label { Text = "Ready", AutoSize = true, ForeColor = Theme.Muted, Margin = new Padding(0, 8, 20, 0) };
            bottomPanel.Controls.Add(statusLabel);

            layout.Controls.Add(bottomPanel, 0, 2);
        }

        private Button CreateLinkButton(string text, EventHandler onClick)
        {
            var btn = new Button
            {
                Text = text,
                AutoSize = true,
                FlatStyle = FlatStyle.Flat,
                ForeColor = Theme.Accent,
                BackColor = Color.Transparent,
                Cursor = Cursors.Hand,
                Margin = new Padding(0, 3, 5, 0)
            };
            btn.FlatAppearance.BorderSize = 0;
            btn.FlatAppearance.MouseOverBackColor = Theme.CardBgHover;
            btn.Click += onClick;
            return btn;
        }

        public void AddToQueue(string inputPath, VideoProcessingOptions options)
        {
            var item = new BatchItem
            {
                InputPath = inputPath,
                Options = options,
                Status = "Pending"
            };
            Queue.Add(item);
            RefreshGrid();
        }

        private void RemoveSelected()
        {
            if (grid.SelectedRows.Count == 0) return;
            var itemsToRemove = new List<BatchItem>();
            foreach (DataGridViewRow row in grid.SelectedRows)
            {
                if (row.Index < Queue.Count) itemsToRemove.Add(Queue[row.Index]);
            }
            foreach (var item in itemsToRemove) Queue.Remove(item);
            RefreshGrid();
        }

        private void ClearQueue()
        {
            Queue.Clear();
            RefreshGrid();
        }

        private void RefreshGrid()
        {
            grid.Rows.Clear();
            foreach (var item in Queue)
            {
                grid.Rows.Add(System.IO.Path.GetFileName(item.InputPath), item.Status, item.Progress, System.IO.Path.GetFileName(item.OutputPath));
            }
        }

        private async System.Threading.Tasks.Task ProcessQueueAsync()
        {
            if (_isProcessing) return;
            if (Queue.Count == 0)
            {
                MessageBox.Show("Queue is empty!", "Batch", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            _isProcessing = true;
            btnStart.Enabled = false;
            btnRemove.Enabled = false;
            btnClear.Enabled = false;

            // Simple progress dialog for batch (or integrated)
            // For now, let's use the row progress

            // We need a way to stop if canceled.
            // Let's iterate
            for (int i = 0; i < Queue.Count; i++)
            {
                var item = Queue[i];
                if (item.Status == "Done") continue;

                item.Status = "Processing";
                grid.Rows[i].Cells[1].Value = "Processing";

                statusLabel.Text = $"Processing {i + 1}/{Queue.Count}: {System.IO.Path.GetFileName(item.InputPath)}";

                // Create a progress reporter that updates this row
                var progress = new Progress<double>(p =>
                {
                    item.Progress = $"{p:F1}%";
                    if (i < grid.Rows.Count) grid.Rows[i].Cells[2].Value = item.Progress;
                });

                // Not using RenderProgressDialog here to avoid popping up for every file.
                // We'll trust the service update later to handle 'quiet' mode or custom progress callback.
                // For now, let's assume ProcessVideoAsync needs a Dialog or we overload it.
                // We will overload VideoProcessingService in next step to accept IProgress or similar.

                // Temporary: Just calling the existing method with a hidden/dummy dialog for now 
                // BUT we plan to update the service. Let's wait for service update.
                // For this step, I'll pass null or mock if possible.
                // Actually, I'll update the service to support a callback or event.

                bool success = await _videoService.ProcessVideoBatchAsync(item.Options, (p, msg) =>
                {
                    // Update grid row safely on UI thread
                    this.Invoke((MethodInvoker)delegate
                    {
                        item.Progress = $"{p:0}%";
                        if (i < grid.Rows.Count)
                        {
                            grid.Rows[i].Cells[2].Value = item.Progress;
                            // We could also update status column with 'msg' but let's keep it simple
                        }
                    });
                });

                item.Status = success ? "Done" : "Error";
                grid.Rows[i].Cells[1].Value = item.Status;

                // Update Output Path if not set (it's generated in service)
                // We might need to capture the output path from service.
                // The service currently generates output path internally. 
                // It should probably return result info or take output path.
            }

            _isProcessing = false;
            btnStart.Enabled = true;
            btnRemove.Enabled = true;
            btnClear.Enabled = true;
            statusLabel.Text = "Batch Completed";
            MessageBox.Show("Batch Processing Completed!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }
}
