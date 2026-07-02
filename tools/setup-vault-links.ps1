# Recreates the machine-local junction that activates the vault's rule docs for Claude Code.
# Canonical rule docs live in docs/rules/ (committed, browsable in the Obsidian vault). Claude Code
# only auto-loads rules from .claude/rules/, so we junction that path to docs/rules/.
# Run once after cloning: pwsh -File tools/setup-vault-links.ps1

$ErrorActionPreference = "Stop"
$repo = Split-Path -Parent $PSScriptRoot
$link = Join-Path $repo ".claude\rules"
$target = Join-Path $repo "docs\rules"

if (-not (Test-Path $target)) { throw "Target not found: $target" }
if (Test-Path $link) {
    Write-Host ".claude/rules already exists — leaving as-is."
} else {
    New-Item -ItemType Junction -Path $link -Target $target | Out-Null
    Write-Host "Created junction: .claude/rules -> docs/rules"
}
