
param(
    [switch]$WithMinioSmoke
)

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

    if ($WithMinioSmoke) {
        Write-Host ""
        Write-Host "==> MinIO storage smoke test"

        $envFile = Join-Path $root ".env"

        $envNames = @(
            "MINIO_APP_ACCESS_KEY",
            "MINIO_APP_SECRET_KEY",
            "MINIO_BUCKET_NAME"
        )

        $originalEnvValues = @{}

        foreach ($name in $envNames) {
            $originalEnvValues[$name] =
                [Environment]::GetEnvironmentVariable($name, "Process")
        }

        try {
            foreach ($name in $envNames) {
                if (-not [string]::IsNullOrWhiteSpace($originalEnvValues[$name])) {
                    continue
                }

                if (-not (Test-Path $envFile)) {
                    throw "Missing .env file."
                }

                $line = Get-Content $envFile |
                    Where-Object { $_.StartsWith("$name=") } |
                    Select-Object -First 1

                if (-not $line) {
                    throw "Missing $name in .env"
                }

                $value = $line.Substring($name.Length + 1).Trim()

                if ($value.Length -ge 2 -and (
                    ($value.StartsWith('"') -and $value.EndsWith('"')) -or
                    ($value.StartsWith("'") -and $value.EndsWith("'"))
                )) {
                    $value = $value.Substring(1, $value.Length - 2)
                }

                if ([string]::IsNullOrWhiteSpace($value)) {
                    throw "Empty value for $name"
                }

                [Environment]::SetEnvironmentVariable(
                    $name, $value, "Process")
            }

            Write-Host ""
            Write-Host "==> Docker Compose validation"

            docker compose config --quiet
            Assert-LastExitCode "Docker Compose validation"

            Write-Host ""
            Write-Host "==> Running document storage smoke test"

            dotnet run --project `
                .\tests\document-storage-smoke\DocumentStorageSmoke.csproj

            Assert-LastExitCode "MinIO storage smoke test"

            Write-Host ""
            Write-Host "MinIO storage smoke test passed."
        }
        finally {
            foreach ($name in $envNames) {
                [Environment]::SetEnvironmentVariable(
                    $name,
                    $originalEnvValues[$name],
                    "Process")
            }
        }
    }
}
finally {
    Pop-Location
}

Write-Host ""
Write-Host "Verification completed successfully."
