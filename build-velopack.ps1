param (
    [Parameter()]
    [ValidateSet('Debug', 'Release')]
    [string]
    $Configuration = 'Release',

    [Parameter()]
    [ValidateNotNullOrEmpty()]
    [string]
    $PackDir = 'release',

    [Parameter()]
    [ValidateNotNullOrEmpty()]
    [string]
    $OutputDir = 'velopack',

    [Parameter()]
    [ValidateNotNullOrEmpty()]
    [string]
    $PackId = 'Netch',

    [Parameter()]
    [ValidateNotNullOrEmpty()]
    [string]
    $PackTitle = 'Netch',

    [Parameter()]
    [ValidateNotNullOrEmpty()]
    [string]
    $MainExe = 'Netch.exe',

    [Parameter()]
    [ValidateNotNullOrEmpty()]
    [string]
    $Icon = 'Netch\Resources\Netch.ico',

    [Parameter()]
    [string]
    $Version = ''
)

Push-Location (Split-Path $MyInvocation.MyCommand.Path -Parent)

try {
    if ([string]::IsNullOrWhiteSpace($Version)) {
        if ([string]::IsNullOrWhiteSpace($env:GITHUB_REF_NAME)) {
            throw 'Version is required when GITHUB_REF_NAME is not set.'
        }

        $Version = $env:GITHUB_REF_NAME
    }

    $Version = $Version.TrimStart('v')

    if (-Not (Test-Path -LiteralPath $PackDir)) {
        .\build.ps1 -Configuration $Configuration -OutputPath $PackDir
        if (-Not $?) { exit $lastExitCode }
    }

    if (-Not (Test-Path -LiteralPath (Join-Path $PackDir $MainExe))) {
        throw "Main executable not found: $(Join-Path $PackDir $MainExe)"
    }

    if (Test-Path -LiteralPath $OutputDir) {
        Remove-Item -Recurse -Force -LiteralPath $OutputDir
    }

    dotnet tool restore
    if (-Not $?) { exit $lastExitCode }

    dotnet tool run vpk -- pack `
        --packId $PackId `
        --packVersion $Version `
        --packDir $PackDir `
        --mainExe $MainExe `
        --packTitle $PackTitle `
        --icon $Icon `
        --outputDir $OutputDir
    if (-Not $?) { exit $lastExitCode }
}
finally {
    Pop-Location
}

exit 0
