[CmdletBinding()]
param(
  [string]$BaseUrl = "https://auth.mozaiks.ai",
  [string]$Realm = "mozaiks",
  [string]$Theme = "mozaiks",
  [switch]$AlsoSetMaster
)

$ErrorActionPreference = 'Stop'

function Get-AdminPassword {
  try {
    return (az keyvault secret show --vault-name agentrepo-secrets --name keycloak-admin-password --query value -o tsv)
  } catch {
    throw "Could not read keycloak-admin-password from Key Vault 'agentrepo-secrets'."
  }
}

function Get-AdminToken {
  param([string]$BaseUrl)

  $adminPassword = Get-AdminPassword
  $token = Invoke-RestMethod -Method Post -Uri "$BaseUrl/realms/master/protocol/openid-connect/token" -ContentType 'application/x-www-form-urlencoded' -Body (@{
      client_id  = 'admin-cli'
      grant_type = 'password'
      username   = 'admin'
      password   = $adminPassword
    })

  return $token.access_token
}

function Set-RealmTheme {
  param(
    [string]$BaseUrl,
    [string]$Realm,
    [string]$Theme,
    [string]$Token
  )

  $headers = @{ Authorization = "Bearer $Token" }

  # Some realm fields may be absent; use a hashtable so we can add keys safely.
  $realmObj = Invoke-RestMethod -Headers $headers -Uri "$BaseUrl/admin/realms/$Realm" -Method Get |
    ConvertTo-Json -Depth 80 |
    ConvertFrom-Json -AsHashtable

  $realmObj['loginTheme'] = $Theme

  # Keep admin/account on built-in themes; we only provide a login theme in-repo.
  if ($realmObj.ContainsKey('accountTheme')) { $realmObj.Remove('accountTheme') }
  if ($realmObj.ContainsKey('adminTheme')) { $realmObj.Remove('adminTheme') }

  Invoke-RestMethod -Headers $headers -Uri "$BaseUrl/admin/realms/$Realm" -Method Put -ContentType 'application/json' -Body ($realmObj | ConvertTo-Json -Depth 80)
}

$token = Get-AdminToken -BaseUrl $BaseUrl
Set-RealmTheme -BaseUrl $BaseUrl -Realm $Realm -Theme $Theme -Token $token
Write-Host "Set themes for realm '$Realm' => '$Theme'" -ForegroundColor Green

if ($AlsoSetMaster) {
  $token2 = Get-AdminToken -BaseUrl $BaseUrl
  Set-RealmTheme -BaseUrl $BaseUrl -Realm 'master' -Theme $Theme -Token $token2
  Write-Host "Set themes for realm 'master' => '$Theme'" -ForegroundColor Green
}
