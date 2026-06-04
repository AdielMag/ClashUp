#Requires -Version 5.1
<#
.SYNOPSIS
    Wires up the AetherNet vendor clone for local development.

.DESCRIPTION
    Run once after cloning ClashUp (and whenever you want to pull AetherNet updates).

    What this script does
    ---------------------
    1. Clones AetherNet into external/AetherNet (or git-pulls if already cloned).
    2. Builds AetherNet.Shared (netstandard2.0) with dotnet.
    3. Copies AetherNet.Shared.dll to Assets/Packages/ so Unity picks it up as
       a precompiled reference (the same pattern as MagicOnion, MessagePack, etc.).

    Server / .NET note
    ------------------
    ClashUp.GameServer.csproj and ClashUp.Shared.csproj use a conditional
    MSBuild <Choose> block (AetherNet.refs.props): when the clone exists they use
    a live ProjectReference; otherwise they fall back to the 'AetherNet.Shared'
    NuGet package (version pinned in Directory.Packages.props).

    Re-run this script after pulling AetherNet changes to rebuild and recopy the DLL.

.EXAMPLE
    ./tools/setup-aethernet.ps1
#>

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$RepoRoot    = Split-Path $PSScriptRoot -Parent
$CloneDir    = Join-Path $RepoRoot 'external\AetherNet'
$RemoteUrl   = 'https://github.com/AdielMag/AetherNet.git'
$DotNet      = '/c/Program Files/dotnet/dotnet.exe'
$SharedCsproj = Join-Path $CloneDir 'src\AetherNet.Shared\AetherNet.Shared.csproj'
$DllSrc      = Join-Path $CloneDir 'src\AetherNet.Shared\bin\Debug\netstandard2.0\AetherNet.Shared.dll'
$DllDest     = Join-Path $RepoRoot 'client\ClashUp.Unity\Assets\Packages\AetherNet.Shared.0.1.0\lib\netstandard2.0'

# ── 1. Clone / update AetherNet ───────────────────────────────────────────────
if (Test-Path (Join-Path $CloneDir '.git')) {
    Write-Host "[aethernet] Pulling latest..." -ForegroundColor Cyan
    Push-Location $CloneDir
    try { git pull --ff-only } finally { Pop-Location }
} else {
    Write-Host "[aethernet] Cloning $RemoteUrl -> $CloneDir" -ForegroundColor Cyan
    git clone $RemoteUrl $CloneDir
}

if (-not (Test-Path $SharedCsproj)) {
    Write-Error "Clone succeeded but expected file not found: $SharedCsproj"
}
Write-Host "[aethernet] Clone OK." -ForegroundColor Green

# ── 2. Apply the Directory.Packages.props opt-out patch ───────────────────────
# (Already committed as external/AetherNet/Directory.Packages.props — nothing to do here.)

# ── 3. Build AetherNet.Shared ─────────────────────────────────────────────────
Write-Host "[aethernet] Building AetherNet.Shared..." -ForegroundColor Cyan
& $DotNet build $SharedCsproj -c Debug --nologo -v quiet
if ($LASTEXITCODE -ne 0) { Write-Error "Build failed (exit $LASTEXITCODE)" }
Write-Host "[aethernet] Build OK." -ForegroundColor Green

# ── 4. Copy DLL to Unity Assets/Packages ──────────────────────────────────────
New-Item -ItemType Directory -Force $DllDest | Out-Null
Copy-Item $DllSrc $DllDest -Force
Write-Host "[aethernet] Copied AetherNet.Shared.dll -> $DllDest" -ForegroundColor Green

Write-Host ""
Write-Host "[aethernet] Setup complete. Open Unity Editor and let it recompile." -ForegroundColor Green
