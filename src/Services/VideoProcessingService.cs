using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;
using VPT.Core;
using VPT.Forms;

namespace VPT.Services
{
    public class VideoProcessingOptions
    {
        public string InputPath { get; set; } = "";
        public string Quality { get; set; } = "1080p HD";
        public string ScaleFilter { get; set; } = "";

        // Rotation
        public bool Rotate90 { get; set; }
        public bool Rotate180 { get; set; }
        public bool Rotate270 { get; set; }
        public bool RotateCustom { get; set; }
        public float CustomRotationDegrees { get; set; }

        // Flips
        public bool FlipHorizontal { get; set; }
        public bool FlipVertical { get; set; }

        // Audio
        public bool Mute { get; set; }
        public bool StereoToMono { get; set; }
        public List<float> VolumeAdjustmentsDb { get; set; } = new();

        // Speed
        public float SpeedMultiplier { get; set; } = 1.0f;

        // Meta
        public double TotalDuration { get; set; }

        // Trim
        public TimeSpan? TrimStart { get; set; }
        public TimeSpan? TrimEnd { get; set; }

        // Crop
        public System.Drawing.Rectangle? CropRectangle { get; set; }

        // Watermark
        public string WatermarkPath { get; set; } = "";
        public string WatermarkPosition { get; set; } = "BottomRight"; // TopLeft, TopRight, BottomLeft, BottomRight, Center, Custom
        public float WatermarkOpacity { get; set; } = 1.0f;
        public float WatermarkScale { get; set; } = 0.15f; // Relative to video width (0.15 = 15%)
        public float WatermarkX { get; set; } = -1f; // Custom X position (fraction 0..1 of video width), -1 = unused
        public float WatermarkY { get; set; } = -1f; // Custom Y position (fraction 0..1 of video height), -1 = unused
        public string WatermarkText { get; set; } = ""; // Text watermark (if set, overrides image)
        public float WatermarkFontSize { get; set; } = 32f; // Font size for text watermark

        // Transcoding
        public string OutputExtension { get; set; } = "";
        public string VideoCodec { get; set; } = "";
        public string AudioCodec { get; set; } = "";
        public string CustomArgs { get; set; } = "";
        public string OutputPathOverride { get; set; } = "";
    }

    public class VideoProcessingService
    {
        public static readonly string FfmpegDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "VPT", "ffmpeg");

        private static readonly string[] RequiredTools = { "ffmpeg.exe", "ffprobe.exe", "ffplay.exe" };
        private const string FfmpegDownloadUrl = "https://www.gyan.dev/ffmpeg/builds/ffmpeg-release-essentials.zip";

        private static bool _ffmpegPromptDeclined = false;

        public void EnsureFfmpegInstalled(Form owner)
        {
            try
            {
                bool allExist = RequiredTools.All(tool =>
                    File.Exists(Path.Combine(FfmpegDir, tool)) ||
                    File.Exists(Path.Combine(AppContext.BaseDirectory, tool)) ||
                    File.Exists(Path.Combine(AppContext.BaseDirectory, "ThirdParty", "bin", tool)));

                if (allExist)
                {
                    Task.Run(() => LogFfmpegVersion());
                    return;
                }

                if (_ffmpegPromptDeclined) return;

                var result = MessageBox.Show(owner,
                    "FFmpeg is required but not found.\n\nWould you like to download it automatically? (~90 MB)\n\n(It will be stored permanently and won't ask again)",
                    "FFmpeg Required",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);

                if (result != DialogResult.Yes)
                {
                    _ffmpegPromptDeclined = true;
                    MessageBox.Show(owner,
                        "FFmpeg is required to process videos.\n\nPlease download it manually and place ffmpeg.exe, ffprobe.exe, ffplay.exe in:\n" + FfmpegDir,
                        "Cannot Continue Without FFmpeg",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    return;
                }

                DownloadAndInstallFfmpeg(owner);
            }
            catch (Exception ex)
            {
                Logger.Error("Error checking FFmpeg installation", ex);
            }
        }

        private void LogFfmpegVersion()
        {
            try
            {
                string ffmpegPath = ExtractFfmpegTool("ffmpeg.exe");
                var psi = new ProcessStartInfo
                {
                    FileName = ffmpegPath,
                    Arguments = "-version",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                };
                using var p = Process.Start(psi);
                string output = p?.StandardOutput.ReadLine() ?? "Unknown";
                p?.WaitForExit();
                Logger.Log($"FFmpeg Version: {output}");
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to probe FFmpeg version", ex);
            }
        }

        private void DownloadAndInstallFfmpeg(Form owner)
        {
            var progressForm = new Form
            {
                Text = "Downloading FFmpeg...",
                Width = 400,
                Height = 140,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                StartPosition = FormStartPosition.CenterParent,
                MaximizeBox = false,
                MinimizeBox = false,
                BackColor = System.Drawing.Color.FromArgb(16, 18, 24),
                ForeColor = System.Drawing.Color.FromArgb(240, 242, 248)
            };

            var statusLabel = new Label
            {
                Text = "Connecting...",
                Dock = DockStyle.Top,
                Height = 30,
                TextAlign = System.Drawing.ContentAlignment.MiddleCenter,
                ForeColor = System.Drawing.Color.FromArgb(240, 242, 248)
            };

            var progressBar = new ProgressBar
            {
                Dock = DockStyle.Top,
                Height = 25,
                Style = ProgressBarStyle.Continuous,
                Minimum = 0,
                Maximum = 100
            };

            progressForm.Controls.Add(progressBar);
            progressForm.Controls.Add(statusLabel);

            Task.Run(async () =>
            {
                try
                {
                    string tempZip = Path.Combine(Path.GetTempPath(), "ffmpeg_download.zip");
                    string tempExtract = Path.Combine(Path.GetTempPath(), "ffmpeg_extract_" + Guid.NewGuid().ToString("N"));

                    using var client = new HttpClient();
                    client.Timeout = TimeSpan.FromMinutes(10);

                    // 1. Download Checksum
                    progressForm.Invoke(() => statusLabel.Text = "Fetching checksum...");
                    string checksumUrl = FfmpegDownloadUrl + ".sha256";
                    string expectedHash = "";
                    try
                    {
                        expectedHash = await client.GetStringAsync(checksumUrl);
                        expectedHash = expectedHash.Trim();
                    }
                    catch (Exception ex)
                    {
                        Logger.Error("Failed to fetch FFmpeg checksum", ex);
                        // Optional: Continue without verification or fail?
                        // For robustness, let's log warning and proceed if download works,
                        // or fail if strict security is required.
                        // Let's fail for "Verify FFmpeg Checksums" task.
                        throw new Exception("Could not fetch checksum for validation.", ex);
                    }

                    // 2. Download Archive
                    progressForm.Invoke(() => statusLabel.Text = "Downloading FFmpeg...");

                    using (var response = await client.GetAsync(FfmpegDownloadUrl, HttpCompletionOption.ResponseHeadersRead))
                    {
                        response.EnsureSuccessStatusCode();
                        var totalBytes = response.Content.Headers.ContentLength ?? -1L;
                        var totalBytesRead = 0L;

                        using var contentStream = await response.Content.ReadAsStreamAsync();
                        using var fileStream = new FileStream(tempZip, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

                        var buffer = new byte[8192];
                        int bytesRead;
                        while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                        {
                            await fileStream.WriteAsync(buffer, 0, bytesRead);
                            totalBytesRead += bytesRead;

                            if (totalBytes > 0)
                            {
                                int percent = (int)((totalBytesRead * 100) / totalBytes);
                                progressForm.Invoke(() =>
                                {
                                    progressBar.Value = Math.Min(percent, 100);
                                    statusLabel.Text = $"Downloading... {totalBytesRead / 1024 / 1024} MB / {totalBytes / 1024 / 1024} MB";
                                });
                            }
                        }
                    }

                    // 3. Verify Checksum
                    progressForm.Invoke(() => statusLabel.Text = "Verifying checksum...");
                    string actualHash = "";
                    using (var sha256 = System.Security.Cryptography.SHA256.Create())
                    {
                        using var stream = File.OpenRead(tempZip);
                        byte[] hashBytes = sha256.ComputeHash(stream);
                        actualHash = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
                    }

                    if (!string.Equals(actualHash, expectedHash, StringComparison.OrdinalIgnoreCase))
                    {
                        throw new Exception($"Checksum mismatch!\nExpected: {expectedHash}\nActual: {actualHash}");
                    }
                    Logger.Log("FFmpeg checksum verified successfully.");

                    progressForm.Invoke(() =>
                    {
                        statusLabel.Text = "Extracting...";
                        progressBar.Style = ProgressBarStyle.Marquee;
                    });

                    if (Directory.Exists(tempExtract)) Directory.Delete(tempExtract, true);
                    ZipFile.ExtractToDirectory(tempZip, tempExtract);

                    var binDir = Directory.GetDirectories(tempExtract, "bin", SearchOption.AllDirectories).FirstOrDefault();
                    if (binDir == null)
                        throw new Exception("Could not find bin folder in extracted FFmpeg archive.");

                    Directory.CreateDirectory(FfmpegDir);

                    foreach (var tool in RequiredTools)
                    {
                        var src = Path.Combine(binDir, tool);
                        var dst = Path.Combine(FfmpegDir, tool);
                        if (File.Exists(src))
                            File.Copy(src, dst, overwrite: true);
                    }

                    try { File.Delete(tempZip); } catch (Exception ex) { Logger.Error("Failed to delete temporary FFmpeg zip", ex); }
                    try { Directory.Delete(tempExtract, true); } catch (Exception ex) { Logger.Error("Failed to delete temporary FFmpeg extract directory", ex); }

                    progressForm.Invoke(() =>
                    {
                        MessageBox.Show(progressForm, "FFmpeg installed successfully!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        progressForm.Close();
                    });
                }
                catch (Exception ex)
                {
                    progressForm.Invoke(() =>
                    {
                        MessageBox.Show(progressForm, $"Failed to download FFmpeg:\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        progressForm.Close();
                    });
                    Logger.Error("Failed to download FFmpeg", ex);
                }
            });



            progressForm.ShowDialog(owner);
        }

        public string ExtractFfmpegTool(string toolName)
        {
            string localPath = Path.Combine(AppContext.BaseDirectory, toolName);
            if (File.Exists(localPath)) return localPath;

            string localSubDir = Path.Combine(AppContext.BaseDirectory, "ThirdParty", "bin", toolName);
            if (File.Exists(localSubDir)) return localSubDir;

            string appDataPath = Path.Combine(FfmpegDir, toolName);
            if (File.Exists(appDataPath)) return appDataPath;

            // Fallback: Extract from Embedded Resource
            string tempDir = Path.Combine(Path.GetTempPath(), "VPT_FFMPEG");
            Directory.CreateDirectory(tempDir);
            string outPath = Path.Combine(tempDir, toolName);

            if (!File.Exists(outPath))
            {
                var asm = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
                string? resourceName = asm.GetManifestResourceNames()
                    .FirstOrDefault(n => n.EndsWith(toolName, StringComparison.OrdinalIgnoreCase));

                if (resourceName == null)
                    throw new FileNotFoundException($"FFmpeg tool '{toolName}' not found.\nPlease place {toolName} in the same folder as the executable.");

                using var s = asm.GetManifestResourceStream(resourceName);
                if (s == null)
                    throw new FileNotFoundException($"Embedded resource stream for {toolName} not found.");
                using var f = File.Create(outPath);
                s.CopyTo(f);
            }
            return outPath;
        }

        public async Task<bool> ProcessVideoAsync(VideoProcessingOptions options, RenderProgressDialog? progressDlg = null)
        {
            return await ProcessVideoBatchAsync(options, (progress, msg) =>
            {
                progressDlg?.UpdateProgress((int)progress, msg);
            }, () => progressDlg?.IsCancelled == true);
        }

        public async Task<bool> ProcessVideoBatchAsync(
            VideoProcessingOptions options,
            Action<double, string> onProgress,
            Func<bool>? checkCancel = null)
        {
            string logPrefix = $"[{Path.GetFileName(options.InputPath)}]";
            Logger.Log($"{logPrefix} Starting processing. Quality: {options.Quality}");

            if (!File.Exists(options.InputPath))
            {
                Logger.Error($"{logPrefix} Input file not found.");
                return false;
            }

            string ffmpegPath = ExtractFfmpegTool("ffmpeg.exe");
            if (!File.Exists(ffmpegPath))
            {
                Logger.Error($"{logPrefix} ffmpeg.exe not found.");
                return false;
            }

            string outPath = string.IsNullOrEmpty(options.OutputPathOverride)
                ? GetPlannedOutputPath(options)
                : options.OutputPathOverride;

            var builder = new FfmpegBuilder()
                 .SetInput(options.InputPath)
                 .SetOutput(outPath)
                 .Overwrite(true);

            // Audio Logic Helpers
            void ConfigureAudio(bool useComplex)
            {
                if (options.Mute) builder.SetAudioCodec("");
                else
                {
                    if (!string.IsNullOrEmpty(options.AudioCodec)) builder.SetAudioCodec(options.AudioCodec);
                    else builder.SetAudioCodec("aac").AddArg("-b:a 192k");

                    float gainDb = options.VolumeAdjustmentsDb.Sum();
                    if (gainDb != 0f) builder.AddAudioFilter($"volume={gainDb.ToString("0.###", CultureInfo.InvariantCulture)}dB");

                    if (options.StereoToMono) builder.AddAudioFilter("pan=mono|c0=.5*c0+.5*c1");

                    // Speed for Audio
                    if (Math.Abs(options.SpeedMultiplier - 1.0f) > 0.01f && !options.Mute)
                    {
                        if (options.SpeedMultiplier >= 0.5f && options.SpeedMultiplier <= 2.0f)
                            builder.AddAudioFilter($"atempo={options.SpeedMultiplier.ToString("0.####", CultureInfo.InvariantCulture)}");
                        else if (options.SpeedMultiplier < 0.5f)
                            builder.AddAudioFilter($"atempo=0.5,atempo={(options.SpeedMultiplier / 0.5f).ToString("0.####", CultureInfo.InvariantCulture)}");
                        else
                            builder.AddAudioFilter($"atempo=2.0,atempo={(options.SpeedMultiplier / 2.0f).ToString("0.####", CultureInfo.InvariantCulture)}");
                    }
                }
            }

            // Standard Filters Helpers
            List<string> GetVideoFilters()
            {
                var filters = new List<string>();

                // Trim (input seeking handled by Builder, but filter trimming not used currently)

                // Crop
                if (options.CropRectangle.HasValue)
                {
                    var r = options.CropRectangle.Value;
                    filters.Add($"crop={r.Width}:{r.Height}:{r.X}:{r.Y}");
                }

                // Scale
                if (!string.IsNullOrEmpty(options.ScaleFilter))
                    filters.Add(options.ScaleFilter);

                // Rotation
                if (options.Rotate90) filters.Add("transpose=clock");
                else if (options.Rotate180) filters.Add("transpose=clock,transpose=clock");
                else if (options.Rotate270) filters.Add("transpose=cclock");
                else if (options.RotateCustom)
                {
                    string rad = (options.CustomRotationDegrees * Math.PI / 180.0)
                        .ToString("0.########", CultureInfo.InvariantCulture);
                    filters.Add($"rotate={rad}:'rotw(iw,ih)':'roth(iw,ih)':0:0:black");
                }

                // Flips
                if (options.FlipHorizontal) filters.Add("hflip");
                if (options.FlipVertical) filters.Add("vflip");

                // Speed Video
                if (Math.Abs(options.SpeedMultiplier - 1.0f) > 0.01f)
                {
                    float ptsFactor = 1.0f / options.SpeedMultiplier;
                    filters.Add($"setpts={ptsFactor.ToString("0.####", CultureInfo.InvariantCulture)}*PTS");
                }

                return filters;
            }

            // Check Watermark — generate text PNG if needed
            if (!string.IsNullOrEmpty(options.WatermarkText) && string.IsNullOrEmpty(options.WatermarkPath))
            {
                // Generate a transparent PNG from the text
                string tempPng = Path.Combine(Path.GetTempPath(), $"vpt_textwm_{Guid.NewGuid()}.png");
                try
                {
                    using var font = new System.Drawing.Font("Segoe UI", options.WatermarkFontSize, System.Drawing.FontStyle.Bold);
                    System.Drawing.SizeF textSize;
                    using (var bmpMeasure = new System.Drawing.Bitmap(1, 1))
                    using (var gMeasure = System.Drawing.Graphics.FromImage(bmpMeasure))
                        textSize = gMeasure.MeasureString(options.WatermarkText, font);

                    int w = (int)Math.Ceiling(textSize.Width) + 20;
                    int h = (int)Math.Ceiling(textSize.Height) + 10;
                    using var bmp = new System.Drawing.Bitmap(w, h, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                    using (var g = System.Drawing.Graphics.FromImage(bmp))
                    {
                        g.Clear(System.Drawing.Color.Transparent);
                        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
                        using var brush = new System.Drawing.SolidBrush(System.Drawing.Color.White);
                        g.DrawString(options.WatermarkText, font, brush, 10, 5);
                    }
                    bmp.Save(tempPng, System.Drawing.Imaging.ImageFormat.Png);
                    options.WatermarkPath = tempPng;
                }
                catch (Exception ex) { Logger.Error("Failed to generate text watermark", ex); }
            }
            bool hasWatermark = !string.IsNullOrEmpty(options.WatermarkPath) && File.Exists(options.WatermarkPath);

            if (options.TrimStart.HasValue)
            {
                string start = options.TrimStart.Value.ToString(@"hh\:mm\:ss\.fff", CultureInfo.InvariantCulture);
                string end = options.TrimEnd.HasValue ? options.TrimEnd.Value.ToString(@"hh\:mm\:ss\.fff", CultureInfo.InvariantCulture) : "";
                builder.Trim(start, end);
            }

            // Transcoding Overrides
            if (!string.IsNullOrEmpty(options.VideoCodec)) builder.SetVideoCodec(options.VideoCodec);

            // Audio Config
            ConfigureAudio(hasWatermark); // Audio filters added to builder._audioFilters regardless

            if (hasWatermark)
            {
                // Complex Filter Construction
                builder.AddInput(options.WatermarkPath);

                var sb = new System.Text.StringBuilder();

                // 1. Process Main Video [0:v] -> [main]
                var standardFilters = GetVideoFilters();
                string mainChain = standardFilters.Count > 0 ? string.Join(",", standardFilters) : "null";

                sb.Append($"[0:v]{mainChain}[main];");

                // 2. Process Watermark [1:v] -> [wm]
                // Scale2Ref: Scale watermark relative to [main]. preserve aspect ratio of watermark.
                // w=iw*scale:h=-1
                string scale = options.WatermarkScale.ToString("0.0#", CultureInfo.InvariantCulture);
                sb.Append($"[1:v][main]scale2ref=w=iw*{scale}:h=-1[wm_sized][main_ref];");

                // Opacity
                string opacity = options.WatermarkOpacity.ToString("0.0#", CultureInfo.InvariantCulture);
                sb.Append($"[wm_sized]format=rgba,colorchannelmixer=aa={opacity}[wm_final];");

                // 3. Overlay
                string pos = options.WatermarkPosition switch
                {
                    "TopLeft" => "x=10:y=10",
                    "TopRight" => "x=W-w-10:y=10",
                    "BottomLeft" => "x=10:y=H-h-10",
                    "BottomRight" => "x=W-w-10:y=H-h-10",
                    "Center" => "x=(W-w)/2:y=(H-h)/2",
                    "Custom" => $"x={((int)(options.WatermarkX)).ToString(CultureInfo.InvariantCulture)}:y={((int)(options.WatermarkY)).ToString(CultureInfo.InvariantCulture)}",
                    _ => "x=W-w-10:y=H-h-10"
                };

                sb.Append($"[main_ref][wm_final]overlay={pos}[outv]");

                builder.SetFilterComplex(sb.ToString());
                builder.AddMap("[outv]");
                if (!options.Mute) builder.AddMap("0:a?");
            }
            else
            {
                // Standard Simple Filters
                foreach (var f in GetVideoFilters()) builder.AddVideoFilter(f);
            }

            // Custom Args
            if (!string.IsNullOrEmpty(options.CustomArgs)) builder.AddArg(options.CustomArgs);

            string cmd = builder.Build();
            Logger.Log($"{logPrefix} Command: ffmpeg {cmd}");

            onProgress(0, "Starting...");

            // Attempt to track duration if not set
            double totalDuration = options.TotalDuration > 0 ? options.TotalDuration : 0;

            int exitCode = -1;
            try
            {
                exitCode = await Task.Run(() =>
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = ffmpegPath,
                        Arguments = cmd,
                        UseShellExecute = false,
                        RedirectStandardError = true,
                        RedirectStandardOutput = true,
                        CreateNoWindow = true
                    };

                    using var p = new Process { StartInfo = psi };

                    p.ErrorDataReceived += (s, e) =>
                    {
                        if (string.IsNullOrWhiteSpace(e.Data)) return;
                        Logger.Log($"[FFMPEG] {e.Data}");

                        // Speed and Size Parsing
                        string speedStr = "";
                        string sizeStr = "";
                        // Regex for speed= 1.23x
                        var speedMatch = System.Text.RegularExpressions.Regex.Match(e.Data, @"speed=\s*(\d+(\.\d+)?)x");
                        if (speedMatch.Success) speedStr = speedMatch.Groups[1].Value + "x";

                        // Regex for size= 1234kB
                        var sizeMatch = System.Text.RegularExpressions.Regex.Match(e.Data, @"size=\s*(\d+)(kB|mB|gB|B)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                        if (sizeMatch.Success) sizeStr = sizeMatch.Value.Replace("size=", "").Trim();

                        if (totalDuration <= 0 && e.Data.Contains("Duration:"))
                        {
                            var match = System.Text.RegularExpressions.Regex.Match(e.Data, @"Duration:\s*(\d+):(\d+):(\d+\.?\d*)");
                            if (match.Success)
                            {
                                int hrs = int.Parse(match.Groups[1].Value);
                                int mins = int.Parse(match.Groups[2].Value);
                                double secs = double.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture);
                                totalDuration = hrs * 3600 + mins * 60 + secs;

                                if (options.TrimEnd.HasValue) totalDuration = options.TrimEnd.Value.TotalSeconds;
                                if (options.TrimStart.HasValue) totalDuration -= options.TrimStart.Value.TotalSeconds;
                            }
                        }

                        if (e.Data.Contains("time="))
                        {
                            var match = System.Text.RegularExpressions.Regex.Match(e.Data, @"time=(\d+):(\d+):(\d+\.?\d*)");
                            if (match.Success)
                            {
                                int hrs = int.Parse(match.Groups[1].Value);
                                int mins = int.Parse(match.Groups[2].Value);
                                double secs = double.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture);
                                double current = hrs * 3600 + mins * 60 + secs;

                                if (totalDuration > 0)
                                {
                                    double pct = Math.Min(99, (current / totalDuration) * 100);

                                    // ETA Calculation
                                    string etaStr = "";
                                    if (current > 0 && totalDuration > current)
                                    {
                                        // If we have speed, use it: remaining_sec / speed
                                        if (double.TryParse(speedStr.Replace("x", ""), NumberStyles.Float, CultureInfo.InvariantCulture, out double spd) && spd > 0.01)
                                        {
                                            double remainingSec = (totalDuration - current) / spd;
                                            etaStr = "ETA: " + TimeSpan.FromSeconds(remainingSec).ToString(@"hh\:mm\:ss");
                                        }
                                    }

                                    string msg = $"Processing... {UiUtils.FormatDuration(current)} / {UiUtils.FormatDuration(totalDuration)}";
                                    if (!string.IsNullOrEmpty(speedStr)) msg += $" ({speedStr})";
                                    if (!string.IsNullOrEmpty(etaStr)) msg += $" - {etaStr}";

                                    onProgress(pct, msg);
                                }
                            }
                        }
                    };

                    p.Start();
                    p.BeginErrorReadLine();

                    while (!p.WaitForExit(100))
                    {
                        if (checkCancel?.Invoke() == true)
                        {
                            try { p.Kill(); } catch (Exception ex) { Logger.Error($"{logPrefix} Failed to kill ffmpeg process", ex); }
                            Logger.Log($"{logPrefix} Cancelled by user.");
                            return -999;
                        }
                    }
                    return p.ExitCode;
                });
            }
            catch (Exception ex)
            {
                Logger.Error($"{logPrefix} Fatal error", ex);
                exitCode = -1;
            }

            bool success = exitCode == 0 && File.Exists(outPath);
            if (!success && exitCode == -999 && File.Exists(outPath))
            {
                try { File.Delete(outPath); } catch (Exception ex) { Logger.Error($"{logPrefix} Failed to cleanup cancelled output", ex); }
            }

            onProgress(success ? 100 : 0, success ? "done" : "error");
            return success;
        }

        public string GetPlannedOutputPath(VideoProcessingOptions options)
        {
            if (!string.IsNullOrEmpty(options.OutputPathOverride)) return options.OutputPathOverride;
            if (string.IsNullOrEmpty(options.InputPath)) return "";
            string dir = Path.GetDirectoryName(options.InputPath)!;
            string name = Path.GetFileNameWithoutExtension(options.InputPath);
            string ext = !string.IsNullOrEmpty(options.OutputExtension) ? $".{options.OutputExtension}" : Path.GetExtension(options.InputPath);
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH.mm.ss");
            return Path.Combine(dir, $"{name}_{timestamp}{ext}");
        }

        public static string GetEffectiveVideoCodec(VideoProcessingOptions options)
        {
            return string.IsNullOrEmpty(options.VideoCodec) ? "libx264 (Auto)" : options.VideoCodec;
        }

        public static string GetEffectiveAudioCodec(VideoProcessingOptions options)
        {
            if (options.Mute) return "None (Muted)";
            return string.IsNullOrEmpty(options.AudioCodec) ? "aac (Auto)" : options.AudioCodec;
        }
    }
}
