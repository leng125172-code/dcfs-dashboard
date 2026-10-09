param(
    [string]$OutputPath = "$PSScriptRoot/artifacts/host"
)

$ErrorActionPreference = 'Stop'
dotnet publish "$PSScriptRoot/../src/WhaleDeck.Agent/WhaleDeck.Agent.csproj" -c Release -r linux-x64 --self-contained true -o "$OutputPath/agent"
dotnet publish "$PSScriptRoot/../src/WhaleDeck.MaintenanceHost/WhaleDeck.MaintenanceHost.csproj" -c Release -r linux-x64 --self-contained true -o "$OutputPath/maintenance"
Get-ChildItem "$OutputPath" -Recurse -File | Get-FileHash -Algorithm SHA256 | ForEach-Object { "{0}  {1}" -f $_.Hash.ToLowerInvariant(), $_.Path.Substring($OutputPath.Length + 1).Replace('\', '/') } | Set-Content "$OutputPath/SHA256SUMS"
