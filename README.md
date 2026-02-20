<div align="center">
<img src="./Assets/AppIcon.png" alt="VPT Icon" width="128" />

# 🎬 VPT — Video Processing Tool

**A modern, intuitive video processing application built with C# and Windows Forms**

[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?style=for-the-badge&logo=dotnet)](https://dotnet.microsoft.com/)
[![Windows](https://img.shields.io/badge/Windows-10%2B-0078D6?style=for-the-badge&logo=windows)](https://www.microsoft.com/windows)
[![License](https://img.shields.io/badge/License-MIT-green?style=for-the-badge)](LICENSE)

</div>

<br/>

<div align="center">
  <img src="./Assets/screenshot.png" alt="VPT Screenshot" width="800" />
</div>

<br/>

---

## ✨ Features

VPT is organized into four main tabs to streamline different workflows without the complexity of a full non-linear editor:

### 1️⃣ Single Clicks (Quick Fixes)
- **Video Control**: Custom Play/Pause button and interactive Timeline Seeking (Click/Drag to scrub)
- **One-Click Operations**: Rotate (90°, 180°, 270°, custom) and flip (horizontal/vertical)
- **Audio Controls**: Volume adjustment (+50%, +25%, -25%, -50%), mute, stereo-to-mono
- **Speed Presets**: Fast-forward or slow-motion adjustments

### 2️⃣ Crop / Trim
Precise timeline and framing controls:
- **Trimming**: Start/end trim selection using an interactive range slider
- **Cropping**: Aspect-ratio assisted cropping and interactive preview-based selection

### 3️⃣ Watermark
Branding controls for creators:
- **Image Watermarks**: Place logos with preset position controls (corners, center) or custom drag positioning
- **Text Watermarks**: Generate text watermarks directly in the app
- **Adjustments**: Control watermark scale and opacity

### 4️⃣ Transcode
Format conversion for compatibility and delivery:
- **Formats**: Convert to MP4, MKV, AVI, MOV, WMV, and more
- **Quality Options**: Quality presets ranging from Web/Social (Lowest) to 4K Ultra HD (High)

---

## 🖼️ Modern Interface
- **Dark Theme**: Beautiful, eye-friendly dark UI
- **Click-to-Upload**: Simply click the preview area or drag & drop files
- **Live Preview**: Instant thumbnail preview of uploaded videos
- **Timeline Display**: Visual timeline showing video duration

### ⚡ Render Options
- **240p** - Ultra compact
- **360p** - Mobile-friendly
- **480p** - Standard definition
- **720p HD** - High definition
- **1080p HD** - Full HD
- **1440p HD** - 2K resolution
- **2160p 4K** - Ultra HD
- **Original** - Keep source resolution

---

## 🚀 Getting Started

### Prerequisites
- **Operating System**: Windows 10 or later (64-bit)
- **Development**: [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (for building from source)
- **Runtime Only**: [.NET 8.0 Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) (for running pre-built executable)

### Installation

#### Option 1: Run from Source (Development)
```bash
# 1. Clone the repository
git clone https://github.com/yourusername/VPT.git

# 2. Navigate to the project directory
cd VPT/VPT-main

# 3. Restore dependencies
dotnet restore

# 4. Run the application in Debug mode
dotnet run

# Or run in Release mode for better performance
dotnet run -c Release
```

#### Option 2: Build Portable Executable
```bash
# 1. Navigate to project directory
cd VPT/VPT-main

# 2. Publish as self-contained single-file executable
dotnet publish -c Release

# 3. Find your executable
# Output: VPT-main/publish/VPT.exe
```

The published executable is **self-contained** and includes:
- All .NET runtime dependencies (no .NET installation required)
- Embedded FFmpeg binaries
- Compressed single-file for easy distribution

#### Option 3: Build with Visual Studio
1. Open `VPT.csproj` in Visual Studio 2022+
2. Select **Release** configuration
3. Build → Publish → Folder
4. Choose the `publish` folder as target

### Verifying Installation
```bash
# Check .NET SDK version
dotnet --version
# Should show 8.0.x or higher

# Verify build works
dotnet build
# Should complete without errors
```

---

## 🔧 Troubleshooting

### Common Issues

#### ❌ "dotnet" is not recognized as a command
**Cause**: .NET SDK is not installed or not in PATH.

**Solution**:
1. Download and install [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
2. Restart your terminal/PowerShell
3. Verify with `dotnet --version`

#### ❌ Build fails with "Target framework 'net8.0-windows' not found"
**Cause**: Windows-specific workload not installed.

**Solution**:
```bash
dotnet workload install microsoft-net-sdk-blazorwebassembly-aot
```
Or reinstall the .NET 8.0 SDK with Windows Desktop development checked.

#### ❌ FFmpeg errors during video processing
**Cause**: FFmpeg binaries missing or corrupted.

**Solution**:
1. Ensure `ThirdParty/bin/` folder contains:
   - `ffmpeg.exe`
   - `ffprobe.exe`
   - `ffplay.exe`
2. Download FFmpeg from [ffmpeg.org](https://ffmpeg.org/download.html) if missing
3. Place the executables in `VPT-main/ThirdParty/bin/`

#### ❌ Application crashes on startup
**Cause**: Missing runtime or corrupted build.

**Solution**:
```bash
# Clean and rebuild
dotnet clean
dotnet restore
dotnet build -c Release
```

#### ❌ Video preview not showing
**Cause**: Unsupported video format or codec.

**Solution**:
- Ensure your video is in a supported format (MP4, AVI, MKV, MOV)
- Try converting the video to MP4 with H.264 codec first

#### ❌ Render button not working
**Cause**: No video loaded or no actions selected.

**Solution**:
1. Load a video by clicking the preview area
2. Select at least one action (rotation, flip, volume, etc.)
3. Click **🎬 Render** and choose output quality

#### ❌ Output video not found
**Cause**: Render completed but file saved to unexpected location.

**Solution**:
- Output is saved in the **same directory** as the source video
- Filename format: `originalname_processed.mp4`
- Check `logs/` folder for detailed processing info

### Getting Help
If your issue isn't listed above:
1. Check the `logs/` folder for error messages
2. Open an issue on GitHub with:
   - Your Windows version
   - .NET SDK version (`dotnet --version`)
   - Error message/screenshot
   - Steps to reproduce

---

## 📖 Usage

### Basic Workflow

1. **Upload Video**
   - Click the preview area and select a video file, OR
   - Drag and drop a video file onto the preview area

2. **Select Operations**
   - Click on action buttons to toggle them (green = active)
   - Rotation options are mutually exclusive
   - Volume adjustments can be stacked in the same direction

3. **Choose Quality & Render**
   - Click the **🎬 Render** button
   - Select output quality from the dialog
   - Wait for processing to complete

4. **Find Output**
   - Processed video is saved in the same directory as the source
   - Check the `logs` folder for detailed processing information

---

## 🧠 Technical Details & Architecture

VPT is written in C# using Windows Forms (.NET 8.0) and utilizes FFmpeg as its backend video processing engine.

### How it Works
1. **Interactive Preview**: When a video is selected, VPT uses `ffprobe` to extract metadata (duration, resolution) and `ffmpeg` to capture a quick thumbnail frame. The thumbnail is rendered in a WinForms `PictureBox` overlaid with custom interaction elements. Playback utilizes an embedded and customized Windows Media Player component (`AxWindowsMediaPlayer`).
2. **Command Generation**: User interactions set boolean flags and values in a `VideoProcessingOptions` object. This object is passed to `VideoProcessingService`, which uses `FfmpegBuilder` to translate those options into raw FFmpeg command line arguments (filters, inputs, outputs).
3. **Execution**: The FFmpeg command is handed off to `VideoEngine` to run asynchronously without blocking the UI thread. The system parses standard error (since FFmpeg logs its progress output to stderr) to extract timestamps and update a progress bar dynamically in the UI.
4. **Third-Party Binaries**: VPT relies on standalone `ffmpeg.exe` and `ffprobe.exe`. If they are not natively found in the `ThirdParty/bin` directory, the application can guide the user or automatically extract/download the latest static builds for Windows to its temporary AppData folder.

---

## 🛠️ Technology Stack

| Component | Technology |
|-----------|------------|
| Framework | .NET 8.0 |
| UI | Windows Forms |
| Video Processing | FFmpeg |
| Language | C# 12 |

---

## 📁 Project Structure

```text
VPT-main/
├── src/                       # Source code
│   ├── Core/                  # Logic & Engines
│   │   ├── FfmpegBuilder.cs   # FFmpeg argument string builder
│   │   └── VideoEngine.cs     # FFmpeg execution wrapper
│   ├── Services/              # Processing & orchestration
│   │   ├── VideoProcessingService.cs
│   │   └── PngIconService.cs  # Dynamic UI icon recoloring
│   └── Forms/                 # UI Components
│       ├── Form1.cs           # Main window
│       └── Controls/          # Modular tabs (SingleClicks, CropTrim, etc.)
├── Assets/                    # Application icons
│   ├── AppIcon.ico
│   └── VPU_Icon_*.png
├── ThirdParty/                # FFmpeg binaries
│   ├── bin/
│   │   ├── ffmpeg.exe
│   │   ├── ffplay.exe
│   │   └── ffprobe.exe
│   └── doc/
├── Program.cs                 # Application entry point
└── VPT.csproj                 # Project configuration
```

---

<div align="center">
VPT - Video Processing Toolkit

</div>
