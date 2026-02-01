# Deploy script for cluster2mqtt
# Usage: .\deploy.ps1

$ErrorActionPreference = "Stop"

$Server = "debian@mqtt.oarc.uk"
$PublishDir = "publish"

Write-Host "Building for Linux..." -ForegroundColor Cyan

# Clean publish directory first
if (Test-Path $PublishDir) {
    Remove-Item -Recurse -Force $PublishDir
}

dotnet publish -c Release -r linux-x64 --self-contained -o $PublishDir

if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed!" -ForegroundColor Red
    exit 1
}

Write-Host "Creating remote directory..." -ForegroundColor Cyan
ssh $Server "mkdir -p /tmp/cluster2mqtt-deploy"

Write-Host "Copying files to $Server..." -ForegroundColor Cyan

# Copy binaries
scp -r "$PublishDir/*" "${Server}:/tmp/cluster2mqtt-deploy/"

# Copy systemd service file
scp "cluster2mqtt.service" "${Server}:/tmp/cluster2mqtt.service"

# Copy and convert install script (fix line endings)
$installScript = Get-Content -Path "install-remote.sh" -Raw
$installScript = $installScript -replace "`r`n", "`n"
$installScript | Set-Content -Path "$env:TEMP\install-remote.sh" -NoNewline -Encoding utf8
scp "$env:TEMP\install-remote.sh" "${Server}:/tmp/install-remote.sh"

Write-Host "Installing on server..." -ForegroundColor Cyan
ssh $Server "chmod +x /tmp/install-remote.sh && /tmp/install-remote.sh"

if ($LASTEXITCODE -eq 0) {
    Write-Host ""
    Write-Host "Deployment complete!" -ForegroundColor Green
    Write-Host "View logs: ssh $Server 'sudo journalctl -u cluster2mqtt -f'" -ForegroundColor Yellow
} else {
    Write-Host "Deployment failed!" -ForegroundColor Red
    exit 1
}
