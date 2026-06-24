# Build script for Peak Archipelago mod
Write-Host "Building Archipelago for Lethal Company..." -ForegroundColor Green

$REPO_ROOT=$PSScriptRoot
$PLUGIN_ROOT="$PSScriptRoot/APLC_plugin"
$APWORLD_ROOT="$PSScriptRoot/APLC_apworld"
$DIST="$REPO_ROOT/dist"

# Remove old dist folder if it exists
if (Test-Path $DIST) {
    Remove-Item -Path $DIST -Recurse -Force
}

# Build the .NET project
Write-Host "Building DLL..." -ForegroundColor Yellow
dotnet build APLC_plugin --configuration Release

if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed!" -ForegroundColor Red
    exit 1
}

Write-Host "DLL built successfully" -ForegroundColor Green

New-Item -Path $REPO_ROOT -Name "dist" -ItemType "Directory" | Out-Null

# Create the Thunderstore package
Write-Host "Packaging Thunderstore zip..." -ForegroundColor Yellow
Add-Type -Assembly System.IO.Compression.FileSystem
$zipPath = [System.IO.Path]::GetFullPath("$DIST/APLC.zip")
$thunderstoreDir=[System.IO.Path]::GetFullPath("$PLUGIN_ROOT/Thunderstore")
$zip = [System.IO.Compression.ZipFile]::Open($zipPath, 'Create')
Get-ChildItem -Path $thunderstoreDir -Recurse -File | ForEach-Object {
    $relativePath = $_.FullName.Substring($thunderstoreDir.Length + 1).Replace('\', '/')
    [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, $_.FullName, $relativePath) | Out-Null
}
$zip.Dispose()

# Create the .apworld package
Write-Host "Creating .apworld package..." -ForegroundColor Yellow

# Check if peak folder exists
if (-not (Test-Path "$APWORLD_ROOT/lethal_company")) {
    Write-Host "Error: 'apworld' folder not found!" -ForegroundColor Red
    exit 1
}

# Create the zip file with forward slashes for Linux compatibility
$zipPath = [System.IO.Path]::GetFullPath("$DIST/lethal_company.apworld")
$apworldDir = [System.IO.Path]::GetFullPath("$APWORLD_ROOT/lethal_company")
$exclusions = @("$apworldDir/__pycache__/*","$apworldDir/*/__pycache__/*","$apworldDir/*.pyc", "$apworldDir/test/*")
$zip = [System.IO.Compression.ZipFile]::Open($zipPath, 'Create')
Get-ChildItem -Path $apworldDir -Recurse -File -Exclude $exclusions | Where-Object {
    $_.FullName -NotMatch ([regex]::escape($apworldDir) + ".*\\__pycache__\\.*|\\.*\.pyc|\\test\\.*")
} | ForEach-Object { # -Exclude won't work. see test script on desktop
    $relativePath = $_.FullName.Substring($apworldDir.Length + 1).Replace('\', '/')
    $entryName = "lethal_company/" + $relativePath
    [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, $_.FullName, $entryName) | Out-Null
}
$zip.Dispose()

Write-Host "Successfully created lethal_company.apworld" -ForegroundColor Green

Write-Host "Done!" -ForegroundColor Green