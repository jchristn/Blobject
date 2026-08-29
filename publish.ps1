param(
    [Parameter(Mandatory = $true, Position = 0)]
    [string] $ApiKey
)

$ErrorActionPreference = 'Stop'

if ($args.Count -ne 0 -or [String]::IsNullOrWhiteSpace($ApiKey))
{
    Write-Error "Usage: .\publish.ps1 <nuget-api-key>"
    exit 1
}

$source = 'https://api.nuget.org/v3/index.json'
$timeoutSeconds = '600'
$maxAttempts = 3
$retryDelaySeconds = 30

$packages = @(
    @{
        Id = 'Blobject.Core'
        Version = '5.1.0'
        Directory = 'src\Blobject.Core\bin\Release'
    },
    @{
        Id = 'Blobject.Disk'
        Version = '5.1.0'
        Directory = 'src\Blobject.Disk\bin\Release'
    },
    @{
        Id = 'Blobject.AzureBlob'
        Version = '5.1.0'
        Directory = 'src\Blobject.AzureBlob\bin\Release'
    },
    @{
        Id = 'Blobject.CIFS'
        Version = '5.1.0'
        Directory = 'src\Blobject.CIFS\bin\Release'
    },
    @{
        Id = 'Blobject.GoogleCloud'
        Version = '5.1.0'
        Directory = 'src\Blobject.GoogleCloud\bin\Release'
    },
    @{
        Id = 'Blobject.NFS'
        Version = '5.1.0'
        Directory = 'src\Blobject.NFS\bin\Release'
    },
    @{
        Id = 'Blobject.AmazonS3'
        Version = '5.1.0'
        Directory = 'src\Blobject.AmazonS3\bin\Release'
    },
    @{
        Id = 'Blobject.AmazonS3Lite'
        Version = '5.1.0'
        Directory = 'src\Blobject.AmazonS3Lite\bin\Release'
    }
)

function Resolve-PackagePath
{
    param(
        [Parameter(Mandatory = $true)]
        [hashtable] $Package,

        [Parameter(Mandatory = $true)]
        [string] $Extension
    )

    $fileName = "$($Package.Id).$($Package.Version).$Extension"
    $path = Join-Path $PSScriptRoot (Join-Path $Package.Directory $fileName)

    if (-not (Test-Path -LiteralPath $path -PathType Leaf))
    {
        throw "Package artifact not found: $path. Run a Release build before publishing."
    }

    return $path
}

function Invoke-Push
{
    param(
        [Parameter(Mandatory = $true)]
        [string] $Path,

        [Parameter(Mandatory = $true)]
        [bool] $NoSymbols
    )

    $arguments = @(
        'nuget',
        'push',
        $Path,
        '--source',
        $source,
        '--api-key',
        $ApiKey,
        '--skip-duplicate',
        '--timeout',
        $timeoutSeconds
    )

    if ($NoSymbols)
    {
        $arguments += '--no-symbols'
    }

    for ($attempt = 1; $attempt -le $maxAttempts; $attempt++)
    {
        Write-Host "Publishing $Path (attempt $attempt of $maxAttempts)..."
        & dotnet @arguments

        if ($LASTEXITCODE -eq 0)
        {
            return
        }

        if ($attempt -eq $maxAttempts)
        {
            throw "dotnet nuget push failed for $Path with exit code $LASTEXITCODE."
        }

        Write-Host "Push failed. Retrying in $retryDelaySeconds seconds..."
        Start-Sleep -Seconds $retryDelaySeconds
    }
}

foreach ($package in $packages)
{
    $packagePath = Resolve-PackagePath -Package $package -Extension 'nupkg'
    $symbolsPath = Resolve-PackagePath -Package $package -Extension 'snupkg'

    Invoke-Push -Path $packagePath -NoSymbols $true
    Invoke-Push -Path $symbolsPath -NoSymbols $false
}

Write-Host 'Package publishing complete.'
