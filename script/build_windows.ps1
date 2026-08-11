param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",
    [switch]$NoRun
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root "src\MiTVRemote.WinForms\MiTVRemote.WinForms.csproj"
$output = Join-Path $root "dist\windows"

dotnet restore $project
dotnet build $project --configuration $Configuration --no-restore
if ($LASTEXITCODE -ne 0) { throw "Build failed." }

if (Test-Path $output) { Remove-Item $output -Recurse -Force }
dotnet publish $project --configuration $Configuration --runtime win-x64 `
    --self-contained false -p:PublishSingleFile=true --output $output
if ($LASTEXITCODE -ne 0) { throw "Publish failed." }

$exe = Join-Path $output "MiTV-Remote.exe"
Write-Host "Output: $exe"
if (-not $NoRun) { Start-Process $exe }
