# 发布自包含可运行目录 + zip
# 勿用 PublishSingleFile(WinUI 会 0x80040111 闪退)
$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.IO.Compression.FileSystem

$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

$out = Join-Path $root "publish"
$dist = Join-Path $root "dist"
$zip = Join-Path $dist "AirSword-win-x64.zip"

if (Test-Path $out) { Remove-Item -Recurse -Force $out }
if (-not (Test-Path $dist)) { New-Item -ItemType Directory -Path $dist | Out-Null }
if (Test-Path $zip) { Remove-Item -Force $zip }

Write-Host "Publishing..."
dotnet publish (Join-Path $root "src\LingXuZhi.App\LingXuZhi.App.csproj") `
  -c Release -r win-x64 -p:Platform=x64 `
  -p:SelfContained=true `
  -p:WindowsAppSDKSelfContained=true `
  -p:PublishSingleFile=false `
  -o $out

if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed: $LASTEXITCODE" }

Get-ChildItem $out -Filter *.pdb -Recurse -ErrorAction SilentlyContinue | Remove-Item -Force

# 先拷到临时目录再压缩,避开对 publish 目录的文件锁
$stage = Join-Path $env:TEMP ("AirSword-publish-" + [guid]::NewGuid().ToString("N"))
try {
    Write-Host "Staging for zip..."
    Copy-Item -Path $out -Destination $stage -Recurse -Force
    Write-Host "Compressing -> $zip"
    [System.IO.Compression.ZipFile]::CreateFromDirectory($stage, $zip, [System.IO.Compression.CompressionLevel]::Optimal, $false)
}
finally {
    if (Test-Path $stage) { Remove-Item -Recurse -Force $stage -ErrorAction SilentlyContinue }
}

if (-not (Test-Path $zip)) { throw "zip not created: $zip" }

$sizeMb = [math]::Round((Get-Item $zip).Length / 1MB, 1)
Write-Host ""
Write-Host "OK  folder: $out"
Write-Host "OK  zip:    $zip  ($sizeMb MB)"
Write-Host "Run:        $(Join-Path $out 'LingXuZhi.App.exe')"
