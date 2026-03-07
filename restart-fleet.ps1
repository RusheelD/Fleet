# Fleet Restart Helper
# Usage:
#   .\restart-fleet.ps1
#   .\restart-fleet.ps1 -Background

param(
    [switch]$Background
)

Clear-Host

$scriptRoot = if ($PSScriptRoot) { $PSScriptRoot } else { (Get-Location).Path }
$killScript = Join-Path $scriptRoot "kill-fleet.ps1"

if (Test-Path $killScript) {
    & $killScript
}
else {
    Write-Host "[WARN] kill-fleet.ps1 not found, continuing..." -ForegroundColor Yellow
}

Start-Sleep -Seconds 1

Push-Location $scriptRoot
try {
    Write-Host "[INFO] Starting new Fleet AppHost instance..." -ForegroundColor Cyan

    if ($Background) {
        $escapedRoot = $scriptRoot.Replace("'", "''")
        $command = "Set-Location '$escapedRoot'; dotnet run --project Fleet.AppHost"
        Start-Process -FilePath "powershell.exe" -ArgumentList "-NoExit", "-Command", $command | Out-Null
        Write-Host "[OK] Fleet AppHost launched in a new PowerShell window." -ForegroundColor Green
    }
    else {
        dotnet run --project Fleet.AppHost
    }
}
finally {
    Pop-Location
}
