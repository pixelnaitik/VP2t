using System;
using System.Collections.Generic;
using System.Globalization;

namespace VPT.Core
{
    // Legacy compatibility adapter: prefer VideoProcessingService + FfmpegBuilder for new work.
    [Obsolete("Use VideoProcessingService/FfmpegBuilder for command generation.")]
    public class VideoOptions
    {
        public string ScaleFilter { get; set; } = "";
        public bool Rotate90 { get; set; }
        public bool Rotate180 { get; set; }
        public bool Rotate270 { get; set; }
        public float CustomRotateDeg { get; set; }
        public bool FlipHorizontal { get; set; }
        public bool FlipVertical { get; set; }
        public bool Mute { get; set; }
        public bool StereoToMono { get; set; }
        public float VolumeGainDb { get; set; }
        public bool Grayscale { get; set; }
        public bool Watermark { get; set; }
        public string WatermarkPath { get; set; } = "";
    }

    [Obsolete("Use VideoProcessingService/FfmpegBuilder for command generation.")]
    public static class VideoEngine
    {
        public static (string Arguments, string LogOutput) BuildArguments(string inputPath, string outputPath, VideoOptions options)
        {
            var logLines = new List<string>();
            var builder = new FfmpegBuilder()
                .SetInput(inputPath)
                .SetOutput(outputPath)
                .Overwrite(true)
                .SetVideoCodec("libx264")
                .SetPreset("veryfast")
                .SetCrf("20");

            if (!string.IsNullOrWhiteSpace(options.ScaleFilter))
            {
                builder.AddVideoFilter(options.ScaleFilter);
                logLines.Add($"Scale filter: {options.ScaleFilter}");
            }

            if (options.Rotate90) { builder.AddVideoFilter("transpose=clock"); logLines.Add("Rotate 90°"); }
            else if (options.Rotate180) { builder.AddVideoFilter("transpose=clock,transpose=clock"); logLines.Add("Rotate 180°"); }
            else if (options.Rotate270) { builder.AddVideoFilter("transpose=cclock"); logLines.Add("Rotate 270°"); }
            else if (Math.Abs(options.CustomRotateDeg) > 0.001f)
            {
                string rad = (options.CustomRotateDeg * Math.PI / 180.0).ToString("0.########", CultureInfo.InvariantCulture);
                builder.AddVideoFilter($"rotate={rad}:'rotw(iw,ih)':'roth(iw,ih)':0:0:black");
                logLines.Add($"Custom rotate: {options.CustomRotateDeg:0.###}° ({rad} rad)");
            }

            if (options.FlipHorizontal) { builder.AddVideoFilter("hflip"); logLines.Add("Flip horizontal"); }
            if (options.FlipVertical) { builder.AddVideoFilter("vflip"); logLines.Add("Flip vertical"); }
            if (options.Grayscale) { builder.AddVideoFilter("hue=s=0"); logLines.Add("Filter: Grayscale"); }

            if (options.Watermark && !string.IsNullOrEmpty(options.WatermarkPath))
            {
                string escapedPath = options.WatermarkPath.Replace("\\", "/").Replace(":", "\\:");
                builder.SetFilterComplex($"[0:v]null[main];movie='{escapedPath}'[wm];[main][wm]overlay=W-w-10:H-h-10[outv]")
                    .AddMap("[outv]")
                    .AddMap("0:a?");
                logLines.Add("Filter: Watermark (bottom-right)");
            }

            if (options.Mute)
            {
                builder.SetAudioCodec("").AddArg("-an");
                logLines.Add("Audio: Mute");
            }
            else
            {
                builder.SetAudioCodec("aac").AddArg("-b:a 192k");
                if (Math.Abs(options.VolumeGainDb) > 0.01f)
                {
                    builder.AddAudioFilter($"volume={options.VolumeGainDb.ToString("0.###", CultureInfo.InvariantCulture)}dB");
                    logLines.Add($"Volume: {options.VolumeGainDb:+#.###;-#.###}dB");
                }
                if (options.StereoToMono)
                {
                    builder.AddAudioFilter("pan=mono|c0=.5*c0+.5*c1");
                    logLines.Add("Audio: Stereo to Mono");
                }
            }

            return (builder.Build(), string.Join(Environment.NewLine, logLines));
        }
    }
}
