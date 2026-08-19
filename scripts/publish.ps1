param([string]$Version="0.1.0",[string[]]$Rids=@("win-x64","win-arm64","linux-x64","linux-arm64","osx-arm64"))
$ErrorActionPreference="Stop"
$root=Split-Path $PSScriptRoot -Parent
$artifacts=Join-Path $root "artifacts"
New-Item -ItemType Directory -Force -Path $artifacts | Out-Null
Push-Location (Join-Path $root "src/WebhookStudio.Web")
try { npm ci; if($LASTEXITCODE){throw "npm ci failed"}; npm run build; if($LASTEXITCODE){throw "frontend build failed"} } finally { Pop-Location }
foreach($rid in $Rids){
  $publish=Join-Path $artifacts "publish-$rid"
  $project=Join-Path $root "src/WebhookStudio.Api/WebhookStudio.Api.csproj"
  dotnet restore $project -r $rid --configfile (Join-Path $root "NuGet.Config"); if($LASTEXITCODE){throw "restore failed for $rid"}
  dotnet publish $project -c Release -r $rid --self-contained true --no-restore -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o $publish; if($LASTEXITCODE){throw "publish failed for $rid"}
  if(!(Test-Path (Join-Path $publish "wwwroot/index.html"))){throw "Frontend assets missing for $rid"}
  $archive=Join-Path $artifacts "WebhookStudio-v$Version-$rid.zip"
  Compress-Archive -Path (Join-Path $publish "*") -DestinationPath $archive -Force
  $hash=(Get-FileHash -Algorithm SHA256 $archive).Hash.ToLowerInvariant()
  Set-Content -Encoding ascii -Path "$archive.sha256" -Value "$hash  $(Split-Path $archive -Leaf)"
}
