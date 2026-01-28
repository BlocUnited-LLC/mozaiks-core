# Keycloak realm export script
$ErrorActionPreference = "Stop"

$tokenResponse = Invoke-RestMethod -Uri 'http://localhost:8080/realms/master/protocol/openid-connect/token' -Method Post -ContentType 'application/x-www-form-urlencoded' -Body 'username=admin&password=admin&grant_type=password&client_id=admin-cli'
$token = $tokenResponse.access_token
Write-Host "Token obtained"

$headers = @{Authorization = "Bearer $token"}
$outputDir = Split-Path -Parent $PSScriptRoot
if (-not $outputDir) { $outputDir = "." }

try {
    # Export clients
    Write-Host "Exporting clients..."
    $clients = Invoke-RestMethod -Uri 'http://localhost:8080/admin/realms/mozaiks/clients' -Headers $headers
    $clients | ConvertTo-Json -Depth 10 | Out-File "$PSScriptRoot/clients-export.json" -Encoding UTF8
    Write-Host "Clients exported: $($clients.Count) clients"

    # Export roles
    Write-Host "Exporting roles..."
    $roles = Invoke-RestMethod -Uri 'http://localhost:8080/admin/realms/mozaiks/roles' -Headers $headers
    $roles | ConvertTo-Json -Depth 10 | Out-File "$PSScriptRoot/roles-export.json" -Encoding UTF8
    Write-Host "Roles exported: $($roles.Count) roles"

    # Export realm config
    Write-Host "Exporting realm config..."
    $realm = Invoke-RestMethod -Uri 'http://localhost:8080/admin/realms/mozaiks' -Headers $headers
    $realm | ConvertTo-Json -Depth 10 | Out-File "$PSScriptRoot/realm-config.json" -Encoding UTF8
    Write-Host "Realm config exported"

    Write-Host "`nExport completed successfully!"
} catch {
    Write-Host "Error: $_"
    Write-Host $_.Exception.Response.StatusCode
    exit 1
}
