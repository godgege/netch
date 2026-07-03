$ErrorActionPreference = 'Stop'
$BuildVersion = '1'

Push-Location (Split-Path $MyInvocation.MyCommand.Path -Parent)

try {
    $updateChecker = Get-Content -Raw -Encoding UTF8 -Path 'Netch\Controllers\UpdateChecker.cs'
    $match = [regex]::Match($updateChecker, 'AssemblyVersion\s*=\s*@"(?<version>[^"]+)"')
    if (-Not $match.Success) {
        throw 'Cannot find AssemblyVersion in Netch\Controllers\UpdateChecker.cs.'
    }

    $version = $match.Groups['version'].Value
    $versionParts = $version.Split('.')
    if ($versionParts.Length -eq 3) {
        $version = "$version.$BuildVersion"
    }

    Write-Host "Packing Netch $version with Velopack..."

    .\build-velopack.ps1 `
        -Configuration Release `
        -PackDir release `
        -OutputDir velopack `
        -Version $version

    if (-Not $?) {
        exit $lastExitCode
    }

    Write-Host ''
    Write-Host 'Velopack output:'
    Get-ChildItem -Path 'velopack' -File | Sort-Object Name | ForEach-Object {
        Write-Host "  $($_.Name)"
    }
}
finally {
    Pop-Location
}