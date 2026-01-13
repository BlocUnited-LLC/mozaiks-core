[CmdletBinding()]
param(
  [string]$ResourceGroup = "rg-mozaiks-prod-eastus",
  [string]$ContainerApp = "mozaiks-keycloak",
  [string]$Environment = "cae-mozaiks-prod",
  [string]$Hostname = "auth.mozaiks.ai",
  [string]$CertificateName = "mc-auth-mozaiks-ai",
  [switch]$ConfigureKeycloakHostname
)

$ErrorActionPreference = 'Stop'

# Use a clean extension directory to avoid issues with locked/broken global extensions.
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
$env:AZURE_EXTENSION_DIR = Join-Path $repoRoot '.azext'
New-Item -ItemType Directory -Force $env:AZURE_EXTENSION_DIR | Out-Null

function Assert-DnsReady {
  param(
    [string]$Hostname,
    [string]$ExpectedCnameTarget,
    [string]$VerificationId
  )

  $txtName = "asuid.$Hostname"

  $txtOk = $false
  try {
    $txt = Resolve-DnsName -Name $txtName -Type TXT -ErrorAction Stop
    $txtValues = @($txt | ForEach-Object { $_.Strings } | Where-Object { $_ })
    $txtOk = $txtValues -contains $VerificationId
  } catch {
    $txtOk = $false
  }

  $cnameOk = $false
  try {
    $cname = Resolve-DnsName -Name $Hostname -Type CNAME -ErrorAction Stop
    $cnameOk = ($cname.NameHost.TrimEnd('.') -ieq $ExpectedCnameTarget.TrimEnd('.'))
  } catch {
    $cnameOk = $false
  }

  if (-not $txtOk -or -not $cnameOk) {
    Write-Host "DNS is not ready yet for $Hostname." -ForegroundColor Yellow
    Write-Host "Create/verify these records in your DNS provider:" -ForegroundColor Yellow
    Write-Host "  1) TXT   $txtName    = $VerificationId" -ForegroundColor Yellow
    Write-Host "  2) CNAME $Hostname   -> $ExpectedCnameTarget" -ForegroundColor Yellow
    Write-Host "Then wait for DNS to propagate and re-run this script." -ForegroundColor Yellow
    throw "DNS records missing or not propagated."
  }
}

$app = az containerapp show -g $ResourceGroup -n $ContainerApp -o json | ConvertFrom-Json
$fqdn = $app.properties.configuration.ingress.fqdn
if (-not $fqdn) { throw "Could not determine container app ingress FQDN." }

$verificationId = az containerapp show-custom-domain-verification-id -o tsv
if (-not $verificationId) { throw "Could not determine Container Apps custom domain verification id." }

Write-Host "Target container app:" -ForegroundColor Cyan
Write-Host "  App:  $ContainerApp" -ForegroundColor Cyan
Write-Host "  FQDN: $fqdn" -ForegroundColor Cyan
Write-Host "Custom domain:" -ForegroundColor Cyan
Write-Host "  Hostname: $Hostname" -ForegroundColor Cyan
Write-Host "  TXT:      asuid.$Hostname = $verificationId" -ForegroundColor Cyan

Assert-DnsReady -Hostname $Hostname -ExpectedCnameTarget $fqdn -VerificationId $verificationId

Write-Host "Adding hostname to container app..." -ForegroundColor Cyan
az containerapp hostname add -g $ResourceGroup -n $ContainerApp --hostname $Hostname -o none

Write-Host "Creating managed certificate in environment..." -ForegroundColor Cyan

# Create (or re-create) managed certificate. If it already exists, this will error; we fall back to reading it.
try {
  az containerapp env certificate create -g $ResourceGroup -n $Environment --certificate-name $CertificateName --hostname $Hostname --validation-method TXT -o none
} catch {
  Write-Host "Managed certificate create returned an error (may already exist). Continuing to status check..." -ForegroundColor Yellow
}

$cert = az containerapp env certificate list -g $ResourceGroup -n $Environment -o json | ConvertFrom-Json |
  Where-Object { $_.name -eq $CertificateName -or $_.properties.subjectName -eq $Hostname } |
  Select-Object -First 1

if (-not $cert) { throw "Could not find managed certificate '$CertificateName' (hostname: $Hostname)." }

if ($cert.properties.provisioningState -ne 'Succeeded') {
  Write-Host "Managed certificate is not ready yet: $($cert.properties.provisioningState)" -ForegroundColor Yellow
  Write-Host "GoDaddy DNS: add this TXT record, then wait a few minutes:" -ForegroundColor Yellow
  Write-Host "  Host:  _dnsauth.$Hostname" -ForegroundColor Yellow
  Write-Host "  Value: $($cert.properties.validationToken)" -ForegroundColor Yellow
  Write-Host "Then re-run this script to complete the binding." -ForegroundColor Yellow
  return
}

Write-Host "Binding hostname to managed certificate..." -ForegroundColor Cyan
az containerapp hostname bind -g $ResourceGroup -n $ContainerApp --hostname $Hostname --environment $Environment --certificate $CertificateName --validation-method TXT -o none

if ($ConfigureKeycloakHostname) {
  Write-Host "Updating Keycloak hostname env vars (creates a new revision)..." -ForegroundColor Cyan
  az containerapp update -g $ResourceGroup -n $ContainerApp --set-env-vars KC_HOSTNAME=$Hostname KC_HOSTNAME_STRICT=true -o none
}

Write-Host "Done." -ForegroundColor Green
Write-Host "Keycloak should be reachable at: https://$Hostname/" -ForegroundColor Green
Write-Host "Realm issuer should become:      https://$Hostname/realms/mozaiks" -ForegroundColor Green
