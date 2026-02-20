$url = "https://www.gyan.dev/ffmpeg/builds/ffmpeg-release-essentials.zip"
$zip = "ffmpeg.zip"
$extract = "ffmpeg_extracted"
$tools = @("ffmpeg.exe", "ffprobe.exe", "ffplay.exe")
$dest = "ThirdParty\bin"

Write-Host "Downloading FFmpeg..."
Invoke-WebRequest -Uri $url -OutFile $zip

Write-Host "Extracting..."
Expand-Archive -Path $zip -DestinationPath $extract -Force

$binDir = Get-ChildItem -Path $extract -Recurse -Directory | Where-Object { $_.Name -eq "bin" } | Select-Object -ExpandProperty FullName -First 1

if ($binDir) {
    New-Item -ItemType Directory -Force -Path $dest | Out-Null
    foreach ($tool in $tools) {
        $src = Join-Path $binDir $tool
        if (Test-Path $src) {
            Copy-Item -Path $src -Destination $dest -Force
            Write-Host "Copied $tool"
        } else {
            Write-Host "Warning: $tool not found in $binDir"
        }
    }
} else {
    Write-Host "Error: bin directory not found in extraction"
}

Remove-Item $zip -Force
Remove-Item $extract -Recurse -Force
