$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot

function Assert-LastExitCode {
    param(
        [string]$Step
    )

    if ($LASTEXITCODE -ne 0) {
        throw "$Step failed with exit code $LASTEXITCODE"
    }
}

Push-Location $root

try {
    Write-Host ""
    Write-Host "==> Backend build"

    dotnet build .\EnterpriseGenAIPlatform.slnx
    Assert-LastExitCode "Backend build"

    Write-Host ""
    Write-Host "==> Backend tests"

    dotnet test .\EnterpriseGenAIPlatform.slnx --no-build
    Assert-LastExitCode "Backend tests"

    Write-Host ""
    Write-Host "==> Frontend verification"

    Push-Location .\src\web

    try {
        Write-Host ""
        Write-Host "==> Frontend lint"

        npm run lint
        Assert-LastExitCode "Frontend lint"

        Write-Host ""
        Write-Host "==> Frontend tests"

        npm test
        Assert-LastExitCode "Frontend tests"

        Write-Host ""
        Write-Host "==> Frontend build"

        npm run build
        Assert-LastExitCode "Frontend build"
    }
    finally {
        Pop-Location
    }
}
finally {
    Pop-Location
}

Write-Host ""
Write-Host "Verification completed successfully."