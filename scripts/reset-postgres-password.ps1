# Resets the local PostgreSQL 'postgres' user password.
# Run in PowerShell as Administrator:
#   Set-ExecutionPolicy -Scope Process Bypass -Force
#   .\scripts\reset-postgres-password.ps1

#Requires -RunAsAdministrator

param(
    [string]$NewPassword = "postgres",
    [string]$PgData = "D:\PostgreSQL\data",
    [string]$PgBin = "D:\PostgreSQL\bin",
    [string]$ServiceName = "postgresql-x64-18"
)

$ErrorActionPreference = "Stop"
$pgHbaPath = Join-Path $PgData "pg_hba.conf"
$backupPath = "$pgHbaPath.backup-$(Get-Date -Format 'yyyyMMdd-HHmmss')"

function Write-Step([string]$Message) {
    Write-Host "`n==> $Message" -ForegroundColor Cyan
}

if (-not (Test-Path $pgHbaPath)) {
    throw "pg_hba.conf not found at $pgHbaPath. Update -PgData if PostgreSQL is installed elsewhere."
}

Write-Step "Backing up pg_hba.conf"
Copy-Item $pgHbaPath $backupPath -Force
Write-Host "Backup: $backupPath"

Write-Step "Enabling temporary trust auth for localhost"
$content = Get-Content $pgHbaPath -Raw
$content = $content -replace '(?m)^host\s+all\s+all\s+127\.0\.0\.1/32\s+scram-sha-256\s*$', 'host    all             all             127.0.0.1/32            trust'
$content = $content -replace '(?m)^host\s+all\s+all\s+::1/128\s+scram-sha-256\s*$', 'host    all             all             ::1/128                 trust'
# PostgreSQL rejects UTF-8 BOM in pg_hba.conf
$utf8NoBom = New-Object System.Text.UTF8Encoding $false
[System.IO.File]::WriteAllText($pgHbaPath, $content, $utf8NoBom)

Write-Step "Restarting PostgreSQL service ($ServiceName)"
Restart-Service -Name $ServiceName -Force

Write-Step "Setting postgres user password"
$psql = Join-Path $PgBin "psql.exe"
$sql = "ALTER USER postgres PASSWORD '$NewPassword';"
& $psql -U postgres -h 127.0.0.1 -p 5432 -d postgres -c $sql

Write-Step "Restoring scram-sha-256 authentication"
Copy-Item $backupPath $pgHbaPath -Force
Restart-Service -Name $ServiceName -Force

Write-Step "Verifying login with new password"
$env:PGPASSWORD = $NewPassword
& $psql -U postgres -h 127.0.0.1 -p 5432 -d postgres -c "SELECT 'Password reset successful' AS status;"
Remove-Item Env:PGPASSWORD -ErrorAction SilentlyContinue

Write-Host "`nDone. Connection string password is now: $NewPassword" -ForegroundColor Green
Write-Host "Matches ATFM Gateway appsettings: Password=$NewPassword"
