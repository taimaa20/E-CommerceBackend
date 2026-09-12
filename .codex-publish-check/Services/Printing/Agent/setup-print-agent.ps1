# ─── setup-print-agent.ps1 ─────────────────────────────────────────────────
# One-shot installer for the in-store print agent.
#
# What it does:
#   1. Asks you 4 questions (URL, tenant id, agent username, password).
#   2. Saves them as machine-wide environment variables.
#   3. Registers POSPrintAgent as a Windows service that auto-starts on boot
#      and self-restarts if it crashes.
#   4. Starts the service.
#
# After this, the cashier just uses the POS like normal. The agent runs
# silently in the background, polls https://<your backend> every 2 seconds,
# claims any printable jobs, and writes them to the local USB / network
# printer. No popups, no dialogs, no interaction.
#
# Where to put this script:
#   Copy the published agent folder to C:\POSAgent\, then run this script
#   from that same folder in an ELEVATED PowerShell (Run as administrator).
# ───────────────────────────────────────────────────────────────────────────

$ErrorActionPreference = "Stop"

# Sanity check: are we elevated?
if (-not ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole(
    [Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw "Run this script in an elevated PowerShell (Run as administrator)."
}

# Sanity check: is the exe present in this folder?
$ExePath = Join-Path $PSScriptRoot "RestaurantPos.Api.exe"
if (-not (Test-Path $ExePath)) {
    throw "RestaurantPos.Api.exe not found in $PSScriptRoot. Did you copy the publish folder here?"
}

Write-Host ""
Write-Host "POS Print Agent - one-time setup"          -ForegroundColor Cyan
Write-Host "================================="          -ForegroundColor Cyan
Write-Host ""

# ── 1. Collect config (4 questions) ────────────────────────────────────────

$BaseUrl = Read-Host "Backend URL (e.g. https://sandobox.net)"
if ([string]::IsNullOrWhiteSpace($BaseUrl)) { throw "URL required." }

# Tenant id is optional - backend uses its hardcoded fallback when empty.
$TenantId = Read-Host "Tenant id (press Enter to use the backend default)"

$Username = Read-Host "Agent username (default: print-agent)"
if ([string]::IsNullOrWhiteSpace($Username)) { $Username = "print-agent" }

$Pwd = Read-Host "Agent password" -AsSecureString
$PwdPlain = [System.Net.NetworkCredential]::new("", $Pwd).Password
if ([string]::IsNullOrWhiteSpace($PwdPlain)) { throw "Password required." }

# ── 2. Save as machine-wide environment variables ──────────────────────────

Write-Host ""
Write-Host "Saving configuration..." -ForegroundColor Gray
[Environment]::SetEnvironmentVariable("PrintAgent__Enabled",            "true",    "Machine")
[Environment]::SetEnvironmentVariable("PrintAgent__BaseUrl",            $BaseUrl,  "Machine")
[Environment]::SetEnvironmentVariable("PrintAgent__TenantId",           $TenantId, "Machine")
[Environment]::SetEnvironmentVariable("PrintAgent__Username",           $Username, "Machine")
[Environment]::SetEnvironmentVariable("PrintAgent__Password",           $PwdPlain, "Machine")
[Environment]::SetEnvironmentVariable("PrintAgent__PollIntervalSeconds","2",       "Machine")
[Environment]::SetEnvironmentVariable("PrintAgent__BatchSize",          "5",       "Machine")
# Bind Kestrel to loopback only so the cashier UI can POST receipts directly
# to the agent during cloud outages (POST /local/print/receipt-html).
[Environment]::SetEnvironmentVariable("ASPNETCORE_URLS",                "http://127.0.0.1:5005", "Machine")
Write-Host "  Stored as machine env vars (visible to the service)." -ForegroundColor Gray

# ── 3. Register or replace the Windows service ─────────────────────────────

$ServiceName = "POSPrintAgent"

if (Get-Service $ServiceName -ErrorAction SilentlyContinue) {
    Write-Host ""
    Write-Host "Existing $ServiceName found - stopping and removing..." -ForegroundColor Yellow
    Stop-Service $ServiceName -Force -ErrorAction SilentlyContinue
    sc.exe delete $ServiceName | Out-Null
    Start-Sleep -Seconds 2
}

Write-Host ""
Write-Host "Installing $ServiceName..." -ForegroundColor Gray
sc.exe create $ServiceName binPath= "`"$ExePath`"" start= auto DisplayName= "POS Print Agent" | Out-Null
sc.exe description $ServiceName "Bridges cloud POS print queue to local printers." | Out-Null
sc.exe failure $ServiceName reset= 86400 actions= restart/5000/restart/5000/restart/60000 | Out-Null

# Install sidecar dependencies BEFORE starting the service.
# If the service starts first, the first jobs fail because Puppeteer isn't unpacked yet.
$sidecarDir = Join-Path $PSScriptRoot 'print-agent-sidecar'
$puppeteerCache = Join-Path $sidecarDir '.puppeteer-cache'

# Pin Puppeteer's browser cache to a folder next to the sidecar so the LocalSystem
# service finds Chrome at the same path the install used. (Default cache is the
# user's ~\.cache\puppeteer, which the service account can't read.)
$env:PUPPETEER_CACHE_DIR = $puppeteerCache
[Environment]::SetEnvironmentVariable('PUPPETEER_CACHE_DIR', $puppeteerCache, 'Machine')

Write-Host "Installing sidecar dependencies in $sidecarDir..." -ForegroundColor Gray
Push-Location $sidecarDir
npm install --omit=dev
# Puppeteer v22+ no longer downloads Chrome via `npm install`. Install it explicitly,
# directing the cache to the pinned folder above.
Write-Host "Installing Chrome for Puppeteer (this can take a few minutes)..." -ForegroundColor Gray
npx --yes puppeteer browsers install chrome
Pop-Location

# Sanity-check that Chrome actually landed in the pinned cache.
$chromeFound = $false
if (Test-Path $puppeteerCache) {
    $chromeExe = Get-ChildItem -Path $puppeteerCache -Recurse -Filter 'chrome.exe' -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($chromeExe) {
        Write-Host "  Chrome installed at: $($chromeExe.FullName)" -ForegroundColor Gray
        $chromeFound = $true
    }
}
if (-not $chromeFound) {
    Write-Host "  WARNING: Chrome was not found under $puppeteerCache" -ForegroundColor Yellow
    Write-Host "  The receipt sidecar will fail until this is resolved." -ForegroundColor Yellow
    Write-Host "  Try manually:  cd '$sidecarDir'; `$env:PUPPETEER_CACHE_DIR='$puppeteerCache'; npx --yes puppeteer browsers install chrome" -ForegroundColor Yellow
}

# Download SumatraPDF if not present (needed for USB printers).
$sumatraExe = Join-Path $sidecarDir 'SumatraPDF.exe'
if (-not (Test-Path $sumatraExe)) {
    Write-Host "Downloading SumatraPDF..." -ForegroundColor Gray
    Invoke-WebRequest -Uri 'https://www.sumatrapdfreader.org/dl/rel/3.5.2/SumatraPDF-3.5.2-64.exe' `
        -OutFile $sumatraExe -UseBasicParsing
}

# Verify Node.js is reachable to the System account (not just the current user).
# The service runs as LocalSystem, which uses the System PATH only.
Write-Host "Verifying Node.js on System PATH..." -ForegroundColor Gray
$nodeOnSystemPath = $null
$systemPath = [Environment]::GetEnvironmentVariable('PATH', 'Machine')
foreach ($p in ($systemPath -split ';')) {
    if ($p -and (Test-Path (Join-Path $p 'node.exe'))) { $nodeOnSystemPath = Join-Path $p 'node.exe'; break }
}
if (-not $nodeOnSystemPath) {
    Write-Host "  WARNING: node.exe is not on the System PATH." -ForegroundColor Yellow
    Write-Host "  The Node installer often writes to the User PATH only. The Windows" -ForegroundColor Yellow
    Write-Host "  service runs as LocalSystem and will fail to spawn the sidecar." -ForegroundColor Yellow
    Write-Host "  Fix: System Properties -> Environment Variables -> add Node's install" -ForegroundColor Yellow
    Write-Host "  dir (e.g. C:\Program Files\nodejs\) to the *System* Path, then reboot." -ForegroundColor Yellow
} else {
    Write-Host "  Found: $nodeOnSystemPath" -ForegroundColor Gray
}
node --version | Out-Host

Write-Host "Starting $ServiceName..." -ForegroundColor Gray
Start-Service $ServiceName

# ── 4. Final status ────────────────────────────────────────────────────────

Start-Sleep -Seconds 2
$status = (Get-Service $ServiceName).Status
Write-Host ""
if ($status -eq "Running") {
    Write-Host "Done. POS Print Agent is RUNNING." -ForegroundColor Green
    Write-Host ""
    Write-Host "What happens now:"
    Write-Host "  * Place an order in the POS - both kitchen ticket and customer"
    Write-Host "    receipt print within 2-3 seconds."
    Write-Host "  * The service auto-starts on every boot - no further action."
    Write-Host "  * To check status later:    Get-Service $ServiceName"
    Write-Host "  * To view logs:             Get-EventLog -LogName Application -Source $ServiceName -Newest 20"
    Write-Host "  * To stop:                  sc.exe stop $ServiceName"
    Write-Host "  * To uninstall:             sc.exe delete $ServiceName  (after stop)"
} else {
    Write-Host "Service registered but not running (status: $status)." -ForegroundColor Red
    Write-Host "Check Event Viewer -> Application -> POSPrintAgent for the error."
}
Write-Host ""
