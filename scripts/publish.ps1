# 发布自包含可运行目录(勿用 PublishSingleFile,WinUI 会 0x80040111 闪退)
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

$out = Join-Path $root "publish"
if (Test-Path $out) { Remove-Item -Recurse -Force $out }

dotnet publish (Join-Path $root "src\LingXuZhi.App\LingXuZhi.App.csproj") `
  -c Release -r win-x64 -p:Platform=x64 `
  -p:SelfContained=true `
  -p:WindowsAppSDKSelfContained=true `
  -p:PublishSingleFile=false `
  -o $out

if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed: $LASTEXITCODE" }

# 去掉 pdb,减小体积
Get-ChildItem $out -Filter *.pdb -Recurse | Remove-Item -Force

Start-Sleep -Seconds 2
$zip = Join-Path $root "AirSword-win-x64.zip"
if (Test-Path $zip) { Remove-Item -Force $zip }
Compress-Archive -Path (Join-Path $out "*") -DestinationPath $zip -Force

Write-Host "OK: $out"
Write-Host "ZIP: $zip"
Write-Host "Run: $(Join-Path $out 'LingXuZhi.App.exe')"
