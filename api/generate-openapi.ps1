<#
.SYNOPSIS
    Regenerates api/openapi.json from the API (#61).

.DESCRIPTION
    Builds the API, then asks Swashbuckle's CLI for the "v1" document and writes it to
    api/openapi.json. Commit the result: CI regenerates it and fails if the committed
    file differs, so the contract can't drift from the code.

    Runs on Windows PowerShell 5.1 and PowerShell 7 (which CI uses).

.PARAMETER Configuration
    Build configuration whose output is read. Defaults to Release, as in CI.

.PARAMETER NoBuild
    Skip the build and use the existing output, as CI does after its own build step.

.EXAMPLE
    ./api/generate-openapi.ps1
#>
param(
    [string] $Configuration = 'Release',
    [switch] $NoBuild
)

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$output = Join-Path $root 'api/openapi.json'
$assembly = Join-Path $root "src/GolfProgress.Api/bin/$Configuration/net8.0/GolfProgress.Api.dll"

# The CLI starts the app's host (up to app.Run(), with a no-op server) to read the
# document from it. Program.cs refuses to start without a connection string and a JWT
# key of at least 32 bytes; the spec never connects to a database or signs a token, so
# placeholders are enough. The audit-event purge is a background service that would
# try to reach that placeholder database, so it's switched off. All of these are set
# for this process only.
$placeholders = @{
    'ConnectionStrings__DefaultConnection' = 'Server=openapi-generation-only;Database=none;'
    'Jwt__Key'                             = 'openapi-generation-only-placeholder-not-a-signing-key'
    'EventRetention__Enabled'              = 'false'
}

Push-Location $root
try {
    if (-not $NoBuild) {
        dotnet build src/GolfProgress.Api --configuration $Configuration --nologo --verbosity quiet
        if ($LASTEXITCODE -ne 0) { throw 'Building the API failed.' }
    }

    if (-not (Test-Path $assembly)) {
        throw "No build output at $assembly. Build the API first, or run without -NoBuild."
    }

    dotnet tool restore | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'dotnet tool restore failed.' }

    foreach ($name in $placeholders.Keys) {
        [Environment]::SetEnvironmentVariable($name, $placeholders[$name], 'Process')
    }

    # Run from the project folder: the app reads appsettings.json (JWT issuer and
    # audience, and other defaults) from the current directory.
    Push-Location (Join-Path $root 'src/GolfProgress.Api')
    try { dotnet swagger tofile --output $output $assembly v1 }
    finally { Pop-Location }
    if ($LASTEXITCODE -ne 0) { throw 'Generating the OpenAPI spec failed.' }

    Write-Host "Wrote $output"
}
finally {
    foreach ($name in $placeholders.Keys) {
        [Environment]::SetEnvironmentVariable($name, $null, 'Process')
    }
    Pop-Location
}
