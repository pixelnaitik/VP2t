using System.Collections.Generic;
using System.Text;

namespace VPT.Core
{
    public class FfmpegBuilder
    {
        private readonly List<string> _inputPaths = new();
        private string _outputPath = "";
        private bool _overwrite = true;
        private string _seekStart = "";
        private string _seekEnd = ""; // using -to

        private readonly List<string> _videoFilters = new();
        private readonly List<string> _audioFilters = new();
        private readonly List<string> _extraArgs = new();
        private readonly List<string> _maps = new();

        private string _filterComplex = "";

        private string _videoCodec = "libx264";
        private string _audioCodec = "aac";
        private string _preset = "veryfast";
        private string _crf = "20";
        private string _bitrateSettings = ""; // -b:v ...

        public FfmpegBuilder SetInput(string path)
        {
            _inputPaths.Clear();
            _inputPaths.Add(path);
            return this;
        }

        public FfmpegBuilder AddInput(string path)
        {
            _inputPaths.Add(path);
            return this;
        }

        public FfmpegBuilder SetOutput(string path)
        {
            _outputPath = path;
            return this;
        }

        public FfmpegBuilder Overwrite(bool overwrite)
        {
            _overwrite = overwrite;
            return this;
        }

        public FfmpegBuilder Trim(string start, string end)
        {
            _seekStart = start;
            _seekEnd = end;
            return this;
        }

        public FfmpegBuilder AddVideoFilter(string filter)
        {
            if (!string.IsNullOrWhiteSpace(filter)) _videoFilters.Add(filter);
            return this;
        }

        public FfmpegBuilder AddAudioFilter(string filter)
        {
            if (!string.IsNullOrWhiteSpace(filter)) _audioFilters.Add(filter);
            return this;
        }

        public FfmpegBuilder SetFilterComplex(string filter)
        {
            _filterComplex = filter;
            return this;
        }

        public FfmpegBuilder AddMap(string map)
        {
            _maps.Add(map);
            return this;
        }

        public FfmpegBuilder SetVideoCodec(string codec)
        {
            _videoCodec = codec;
            return this;
        }

        public FfmpegBuilder SetAudioCodec(string codec)
        {
            _audioCodec = codec;
            return this;
        }

        public FfmpegBuilder SetPreset(string preset)
        {
            _preset = preset;
            return this;
        }

        public FfmpegBuilder SetCrf(string crf)
        {
            _crf = crf;
            return this;
        }

        public FfmpegBuilder SetBitrateSettings(string settings)
        {
            _bitrateSettings = settings;
            return this;
        }

        public FfmpegBuilder CopyAll()
        {
            _videoCodec = "copy";
            _audioCodec = "copy";
            _preset = "";
            _crf = "";
            return this;
        }

        public FfmpegBuilder AddArg(string arg)
        {
            _extraArgs.Add(arg);
            return this;
        }

        public string Build()
        {
            var sb = new StringBuilder();
            if (_overwrite) sb.Append("-y ");

            if (!string.IsNullOrEmpty(_seekStart)) sb.Append($"-ss {_seekStart} ");
            if (!string.IsNullOrEmpty(_seekEnd)) sb.Append($"-to {_seekEnd} ");

            foreach (var input in _inputPaths)
            {
                sb.Append($"-i \"{input}\" ");
            }

            if (!string.IsNullOrEmpty(_filterComplex))
            {
                sb.Append($"-filter_complex \"{_filterComplex}\" ");
            }
            else if (_videoFilters.Count > 0)
            {
                sb.Append($"-vf \"{string.Join(",", _videoFilters)}\" ");
            }

            // Audio filters valid for simple audio stream or if mapped, but standard -af works on output stream usually
            if (_audioFilters.Count > 0)
                sb.Append($"-af \"{string.Join(",", _audioFilters)}\" ");

            if (_maps.Count > 0)
            {
                foreach (var m in _maps) sb.Append($"-map {m} ");
            }
            else
            {
                // Default mapping if not confusing
                // If complex filter exists but no maps, ffmpeg tries to auto pick.
                // We leave it to caller or auto.
                // Standard logic:
                if (string.IsNullOrEmpty(_filterComplex))
                {
                    sb.Append("-map 0:v? -map 0:a? ");
                }
            }

            // Progress pipe
            sb.Append("-progress pipe:1 ");

            if (_videoCodec == "copy")
            {
                sb.Append("-c:v copy ");
            }
            else
            {
                sb.Append($"-c:v {_videoCodec} ");
                if (!string.IsNullOrEmpty(_preset)) sb.Append($"-preset {_preset} ");
                if (!string.IsNullOrEmpty(_bitrateSettings)) sb.Append($"{_bitrateSettings} ");
                else if (!string.IsNullOrEmpty(_crf)) sb.Append($"-crf {_crf} ");
            }

            if (_audioCodec == "copy")
            {
                sb.Append("-c:a copy ");
            }
            else
            {
                if (string.IsNullOrEmpty(_audioCodec)) { /* mute / no audio */ }
                else sb.Append($"-c:a {_audioCodec} ");
            }

            foreach (var arg in _extraArgs) sb.Append($"{arg} ");

            sb.Append($"\"{_outputPath}\"");

            return sb.ToString();
        }
    }
}
