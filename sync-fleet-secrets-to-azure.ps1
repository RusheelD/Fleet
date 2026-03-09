param(
    [string]$AppName = "fleet-ai-dev",
    [string]$ProjectPath = "Fleet.Server",
    [string]$ResourceGroup,
    [string]$SubscriptionId,
    [switch]$NoRestart,
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"

function Get-UserSecretsMap([string]$projectPath) {
    $map = @{}
    $lines = dotnet user-secrets list --project $projectPath
    foreach ($line in $lines) {
        $parts = $line -split " = ", 2
        if ($parts.Length -eq 2) {
            $map[$parts[0].Trim()] = $parts[1]
        }
    }

    return $map
}

function Get-SecretValue([hashtable]$secrets, [string[]]$keys) {
    foreach ($key in $keys) {
        if ($secrets.ContainsKey($key) -and -not [string]::IsNullOrWhiteSpace($secrets[$key])) {
            return $secrets[$key]
        }
    }

    return $null
}

if (-not (Get-Command az -ErrorAction SilentlyContinue)) {
    throw "Azure CLI (az) is required."
}

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw ".NET SDK (dotnet) is required."
}

if (-not (Test-Path $ProjectPath)) {
    throw "Project path '$ProjectPath' was not found."
}

if (-not [string]::IsNullOrWhiteSpace($SubscriptionId)) {
    az account set --subscription $SubscriptionId | Out-Null
}

if ([string]::IsNullOrWhiteSpace($ResourceGroup)) {
    $ResourceGroup = az webapp list --query "[?name=='$AppName'].resourceGroup | [0]" -o tsv
}

if ([string]::IsNullOrWhiteSpace($ResourceGroup)) {
    throw "Could not find Azure Web App '$AppName' in current subscription context. Use -SubscriptionId and/or -ResourceGroup."
}

$secrets = Get-UserSecretsMap -projectPath $ProjectPath

$gitHubClientId = Get-SecretValue -secrets $secrets -keys @("GitHub:ClientId")
if ([string]::IsNullOrWhiteSpace($gitHubClientId)) {
    $defaultSettingsPath = Join-Path $ProjectPath "appsettings.json"
    if (Test-Path $defaultSettingsPath) {
        $json = Get-Content -Path $defaultSettingsPath -Raw | ConvertFrom-Json
        if ($json.GitHub -and $json.GitHub.ClientId) {
            $gitHubClientId = [string]$json.GitHub.ClientId
        }
    }
}

$settings = [ordered]@{}
$settings["GitHub__ClientId"] = $gitHubClientId
$settings["GitHub__ClientSecret"] = Get-SecretValue -secrets $secrets -keys @("GitHub:ClientSecret")
$settings["ConnectionStrings__fleetdb"] = Get-SecretValue -secrets $secrets -keys @("ConnectionStrings:fleetdb", "ConnectionStrings:Default")
$settings["ConnectionStrings__fleetdb_migrations"] = Get-SecretValue -secrets $secrets -keys @("ConnectionStrings:fleetdb_migrations")
$settings["ConnectionStrings__cache"] = Get-SecretValue -secrets $secrets -keys @("ConnectionStrings:cache")
$settings["LLM__Provider"] = Get-SecretValue -secrets $secrets -keys @("LLM:Provider")
$settings["LLM__ApiKey"] = Get-SecretValue -secrets $secrets -keys @("LLM:ApiKey")
$settings["LLM__Endpoint"] = Get-SecretValue -secrets $secrets -keys @("LLM:Endpoint")
$settings["LLM__Model"] = Get-SecretValue -secrets $secrets -keys @("LLM:Model")
$settings["LLM__GenerateModel"] = Get-SecretValue -secrets $secrets -keys @("LLM:GenerateModel")

$settingsArgs = New-Object System.Collections.Generic.List[string]
$syncedKeys = New-Object System.Collections.Generic.List[string]
$missingKeys = New-Object System.Collections.Generic.List[string]

foreach ($entry in $settings.GetEnumerator()) {
    if (-not [string]::IsNullOrWhiteSpace([string]$entry.Value)) {
        $settingsArgs.Add(("{0}={1}" -f $entry.Key, $entry.Value))
        $syncedKeys.Add($entry.Key)
    } else {
        $missingKeys.Add($entry.Key)
    }
}

if ($settingsArgs.Count -eq 0) {
    throw "No app settings could be sourced from local user-secrets."
}

if ($DryRun) {
    Write-Output ("Dry run only. Would sync {0} keys to app '{1}' in resource group '{2}':" -f $settingsArgs.Count, $AppName, $ResourceGroup)
    $syncedKeys | ForEach-Object { Write-Output (" - " + $_) }
    if ($missingKeys.Count -gt 0) {
        Write-Output ("Missing local values for: {0}" -f ([string]::Join(", ", $missingKeys)))
    }

    return
}

az webapp config appsettings set --resource-group $ResourceGroup --name $AppName --settings $settingsArgs --output none

if (-not $NoRestart) {
    az webapp restart --resource-group $ResourceGroup --name $AppName --output none
}

Write-Output ("Synced {0} app settings to '{1}' (resource group '{2}')." -f $settingsArgs.Count, $AppName, $ResourceGroup)
Write-Output ("Updated keys: {0}" -f ([string]::Join(", ", $syncedKeys)))
if ($missingKeys.Count -gt 0) {
    Write-Output ("Missing local values (not changed): {0}" -f ([string]::Join(", ", $missingKeys)))
}
