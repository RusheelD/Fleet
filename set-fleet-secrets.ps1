# Fleet Secrets Setup (local developer vault via .NET User Secrets)
# Usage:
#   .\set-fleet-secrets.ps1
#   .\set-fleet-secrets.ps1 -AzureOpenAiApiKey "..." -GitHubClientSecret "..."

param(
    [string]$AzureOpenAiApiKey,
    [string]$AzureOpenAiEndpoint = "https://prakash-dev-pg.cognitiveservices.azure.com/openai/responses?api-version=2025-04-01-preview",
    [string]$AzureOpenAiModel = "gpt-5.2-codex",
    [string]$GitHubClientSecret
)

$scriptRoot = if ($PSScriptRoot) { $PSScriptRoot } else { (Get-Location).Path }

if ([string]::IsNullOrWhiteSpace($AzureOpenAiApiKey)) {
    $AzureOpenAiApiKey = Read-Host "Enter Azure OpenAI API key"
}

if ([string]::IsNullOrWhiteSpace($AzureOpenAiEndpoint)) {
    $AzureOpenAiEndpoint = Read-Host "Enter Azure OpenAI Responses endpoint URI"
}

if ([string]::IsNullOrWhiteSpace($AzureOpenAiModel)) {
    $AzureOpenAiModel = Read-Host "Enter Azure OpenAI model/deployment key"
}

if ([string]::IsNullOrWhiteSpace($GitHubClientSecret)) {
    $GitHubClientSecret = Read-Host "Enter GitHub client secret"
}

if ([string]::IsNullOrWhiteSpace($AzureOpenAiApiKey) -or [string]::IsNullOrWhiteSpace($AzureOpenAiEndpoint) -or [string]::IsNullOrWhiteSpace($AzureOpenAiModel) -or [string]::IsNullOrWhiteSpace($GitHubClientSecret)) {
    Write-Host "[ERROR] Azure OpenAI key/endpoint/model and GitHub client secret are required." -ForegroundColor Red
    exit 1
}

Push-Location $scriptRoot
try {
    # AppHost vault entries (used to inject server env vars)
    dotnet user-secrets set "Secrets:AzureOpenAiApiKey" "$AzureOpenAiApiKey" --project "Fleet.AppHost" | Out-Null
    dotnet user-secrets set "Secrets:AzureOpenAiEndpoint" "$AzureOpenAiEndpoint" --project "Fleet.AppHost" | Out-Null
    dotnet user-secrets set "Secrets:AzureOpenAiModel" "$AzureOpenAiModel" --project "Fleet.AppHost" | Out-Null
    dotnet user-secrets set "Secrets:GitHubClientSecret" "$GitHubClientSecret" --project "Fleet.AppHost" | Out-Null

    # Optional direct Server secrets (for running Fleet.Server standalone)
    dotnet user-secrets set "LLM:Provider" "azure-openai" --project "Fleet.Server" | Out-Null
    dotnet user-secrets set "LLM:ApiKey" "$AzureOpenAiApiKey" --project "Fleet.Server" | Out-Null
    dotnet user-secrets set "LLM:Endpoint" "$AzureOpenAiEndpoint" --project "Fleet.Server" | Out-Null
    dotnet user-secrets set "LLM:Model" "$AzureOpenAiModel" --project "Fleet.Server" | Out-Null
    dotnet user-secrets set "LLM:GenerateModel" "$AzureOpenAiModel" --project "Fleet.Server" | Out-Null
    dotnet user-secrets set "GitHub:ClientSecret" "$GitHubClientSecret" --project "Fleet.Server" | Out-Null

    Write-Host "[OK] Secrets stored in User Secrets for Fleet.AppHost and Fleet.Server." -ForegroundColor Green
}
finally {
    Pop-Location
}

