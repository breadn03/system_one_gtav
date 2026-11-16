param([string]$OutputDirectory = 'artifacts/package')
$ErrorActionPreference = 'Stop'
$workspaceRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$targetPath = if ([IO.Path]::IsPathRooted($OutputDirectory)) { [IO.Path]::GetFullPath($OutputDirectory) } else { [IO.Path]::GetFullPath((Join-Path $workspaceRoot $OutputDirectory)) }
if (-not $targetPath.StartsWith($workspaceRoot.TrimEnd('\') + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Package destination must be a child of this workspace.' }
if (Test-Path -LiteralPath $targetPath) { throw 'Destination already exists. Choose a new output directory.' }
$buildPath = Join-Path $workspaceRoot 'src/Pursuit.Legacy/bin/Release/net48'
$dlls = @('Pursuit.Legacy.dll', 'Pursuit.Core.dll', 'Newtonsoft.Json.dll')
foreach ($dll in $dlls) { if (-not (Test-Path -LiteralPath (Join-Path $buildPath $dll))) { throw "Build Release first: missing $dll" } }
foreach ($directory in @('scripts', 'service/pursuit_service', 'config', 'benchmarks')) { New-Item -ItemType Directory -Path (Join-Path $targetPath $directory) -Force | Out-Null }
foreach ($dll in $dlls) { Copy-Item -LiteralPath (Join-Path $buildPath $dll) -Destination (Join-Path $targetPath 'scripts') }
Copy-Item -LiteralPath (Join-Path $workspaceRoot 'config/Pursuit.example.ini') -Destination (Join-Path $targetPath 'scripts/Pursuit.ini')
Get-ChildItem -LiteralPath (Join-Path $workspaceRoot 'service/pursuit_service') -File -Filter '*.py' | ForEach-Object { Copy-Item -LiteralPath $_.FullName -Destination (Join-Path $targetPath 'service/pursuit_service') }
foreach ($file in @('pyproject.toml', 'requirements.lock.txt')) { Copy-Item -LiteralPath (Join-Path $workspaceRoot "service/$file") -Destination (Join-Path $targetPath 'service') }
Copy-Item -LiteralPath (Join-Path $workspaceRoot 'config/runtime-manifest.json') -Destination (Join-Path $targetPath 'config')
Copy-Item -LiteralPath (Join-Path $workspaceRoot 'benchmarks/scenarios.jsonl') -Destination (Join-Path $targetPath 'benchmarks')
Copy-Item -LiteralPath (Join-Path $workspaceRoot 'docs') -Destination (Join-Path $targetPath 'docs') -Recurse
foreach ($sourceFolder in @('src', 'tests', 'contracts', 'scripts')) {
    Get-ChildItem -LiteralPath (Join-Path $workspaceRoot $sourceFolder) -Recurse -File | Where-Object { $_.Extension -in @('.cs', '.csproj', '.json', '.ps1') -and $_.FullName -notmatch '\\(bin|obj)\\' } | ForEach-Object {
        $relativePath = $_.FullName.Substring($workspaceRoot.Length + 1)
        $sourceTarget = Join-Path $targetPath $relativePath
        New-Item -ItemType Directory -Path (Split-Path -Parent $sourceTarget) -Force | Out-Null
        Copy-Item -LiteralPath $_.FullName -Destination $sourceTarget
    }
}
New-Item -ItemType Directory -Path (Join-Path $targetPath 'service/tests') -Force | Out-Null
Get-ChildItem -LiteralPath (Join-Path $workspaceRoot 'service/tests') -File -Filter '*.py' | ForEach-Object { Copy-Item -LiteralPath $_.FullName -Destination (Join-Path $targetPath 'service/tests') }
foreach ($file in @('README.md', 'LICENSE')) { Copy-Item -LiteralPath (Join-Path $workspaceRoot $file) -Destination $targetPath }
Write-Output "Package created: $targetPath"
