<div align="center">

# 🎬 VPT — Video Processing Tool

A modern, beginner-friendly Windows video utility powered by FFmpeg.

[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?style=for-the-badge&logo=dotnet)](https://dotnet.microsoft.com/)
[![Windows](https://img.shields.io/badge/Windows-10%2B-0078D6?style=for-the-badge&logo=windows)](https://www.microsoft.com/windows)
[![License](https://img.shields.io/badge/License-MIT-green?style=for-the-badge)](LICENSE)

<img src="Assets/screenshot.png" alt="VPT Screenshot" width="800" />
</div>

---

## Overview

**VPT (Video Processing Tool)** helps creators perform common video tasks quickly without learning a full non-linear editor.
It provides a polished dark-mode interface over FFmpeg workflows for everyday operations like rotate, trim, crop, watermark, and transcode.

### Why VPT

- ⚡ **Fast everyday edits** for creator workflows
- 🧩 **Simple UI** with task-focused tabs
- 🛠️ **FFmpeg-powered output** with practical defaults
- 🪶 **Lightweight desktop app** for Windows users

---

## Feature Set

VPT is organized into four main tabs:

### 1) Single Clicks
Quick operations for routine fixes:
- Rotate (90°/180°/270°)
- Flip horizontal / vertical
- Audio actions (mute, stereo-to-mono)
- Speed presets

### 2) Crop / Trim
Precise timeline and framing controls:
- Start/end trim selection
- Aspect-ratio assisted cropping
- Interactive preview-based crop selection

### 3) Watermark
Branding controls for creators:
- Image watermark placement
- Text watermark support
- Opacity and scale tuning
- Preset positions + custom drag positioning

### 4) Transcode
Format conversion for compatibility and delivery:
- MP4, MKV, AVI, MOV, WMV, and more
- Quality presets for web/social/high-quality exports
- Default format persistence for repeat workflows

---

## Quick Start

### Prerequisites

- Windows 10+
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

### Run from source

```bash
git clone <your-repo-url>
cd VPT-
dotnet run
```

### Typical workflow

1. Drop/select a video file
2. Make edits in one or more tabs
3. Click **Render** / **Convert Video**
4. Confirm settings and process

Output is saved next to the source file by default.

---

## FFmpeg Integration

VPT checks for FFmpeg tools in:

1. App directory
2. `ThirdParty/bin/`
3. User-local app data storage

If missing, the app can guide users through automatic download and setup.

---

## Project Structure

```text
Assets/                     # App icons and visual assets
src/Core/                   # Core utilities, builders, app services
src/Services/               # Video processing and FFmpeg orchestration
src/Forms/                  # Main forms, dialogs, and UI controls
ThirdParty/                 # Bundled third-party components/licenses
Program.cs                  # Application entry point
VPT.csproj                  # Project configuration
```

---

## Build & Publish

VPT is split into four main tabs to keep things simple:

```bash
dotnet build
```

### Publish (configured as single-file Windows executable)

### 3️⃣ Watermark
Add branding with image or text overlays.
* Place a logo with position controls (corners, center, custom).
* Control watermark scale and opacity.
* Optionally generate a text watermark directly in the app.

### 4️⃣ Transcode (Format Conversion)
Ever have an editor refuse to open an `.MKV` or `.WebM` file?
*   Convert any video to standard `.MP4`, `.MOV`, or `.AVI`.
*   Choose Quality presets from `Lowest` (smallest file size) to `High` (best looking).

---

## Troubleshooting

- **`dotnet` command not found**
  - Install .NET 8 SDK and restart your terminal.
- **FFmpeg not found**
  - Place `ffmpeg.exe`, `ffprobe.exe`, `ffplay.exe` in `ThirdParty/bin/`, or use in-app setup.
- **Unexpected processing error**
  - Check the `logs/` directory for diagnostic details.

---

## Contributing

Contributions are welcome.

If you plan to contribute:
- Open an issue describing the proposed change
- Keep changes focused and reviewable
- Include testing notes in your PR description

---

## License

This project is licensed under the MIT License.
See [`LICENSE`](LICENSE) for details.
