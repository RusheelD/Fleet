# Fleet Secrets Setup (local developer vault via .NET User Secrets)
# Usage:
#   .\set-fleet-secrets.ps1
#   .\set-fleet-secrets.ps1 -AnthropicApiKey "..." -GitHubClientSecret "..."

param(
    [string]$AnthropicApiKey,
    [string]$GitHubClientSecret
)

$scriptRoot = if ($PSScriptRoot) { $PSScriptRoot } else { (Get-Location).Path }

if ([string]::IsNullOrWhiteSpace($AnthropicApiKey)) {
    $AnthropicApiKey = Read-Host "Enter Anthropic (Claude) API key"
}

if ([string]::IsNullOrWhiteSpace($GitHubClientSecret)) {
    $GitHubClientSecret = Read-Host "Enter GitHub client secret"
}

if ([string]::IsNullOrWhiteSpace($AnthropicApiKey) -or [string]::IsNullOrWhiteSpace($GitHubClientSecret)) {
    Write-Host "[ERROR] Both secrets are required." -ForegroundColor Red
    exit 1
}

Push-Location $scriptRoot
try {
    # AppHost vault entries (used to inject server env vars)
    dotnet user-secrets set "Secrets:AnthropicApiKey" "$AnthropicApiKey" --project "Fleet.AppHost" | Out-Null
    dotnet user-secrets set "Secrets:GitHubClientSecret" "$GitHubClientSecret" --project "Fleet.AppHost" | Out-Null

    # Optional direct Server secrets (for running Fleet.Server standalone)
    dotnet user-secrets set "LLM:ApiKey" "$AnthropicApiKey" --project "Fleet.Server" | Out-Null
    dotnet user-secrets set "GitHub:ClientSecret" "$GitHubClientSecret" --project "Fleet.Server" | Out-Null

    Write-Host "[OK] Secrets stored in User Secrets for Fleet.AppHost and Fleet.Server." -ForegroundColor Green
}
finally {
    Pop-Location
}
