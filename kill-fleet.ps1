# Fleet Process Killer
# Usage: .\kill-fleet.ps1

$killed = 0

# Kill native apphost processes (if present)
$native = Get-Process -Name "Fleet.Server", "Fleet.AppHost" -ErrorAction SilentlyContinue
foreach ($p in $native) {
    Stop-Process -Id $p.Id -Force -ErrorAction SilentlyContinue
    $killed++
}

# Kill dotnet-hosted Fleet processes (common when launched via `dotnet run`)
$dotnetFleet = Get-CimInstance Win32_Process -Filter "Name = 'dotnet.exe'" |
Where-Object {
    ($_.CommandLine -match 'Fleet\\Fleet\.AppHost') -or
    ($_.CommandLine -match 'Fleet\\Fleet\.Server')
}

foreach ($p in $dotnetFleet) {
    Stop-Process -Id $p.ProcessId -Force -ErrorAction SilentlyContinue
    $killed++
}

Write-Host "[OK] Killed $killed Fleet-related process(es)." -ForegroundColor Green
