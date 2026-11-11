param([string]$OutputDirectory = (Join-Path $PSScriptRoot 'dist'), [switch]$SkipBuild)
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
if (-not $SkipBuild) {
    foreach ($project in @('icebreaker-client/icebreaker-client.csproj', 'icebreaker-server/icebreaker-server.csproj')) {
        dotnet build (Join-Path $root $project) -c Release -p:DeployToGame=false --nologo
        if ($LASTEXITCODE -ne 0) { throw "Build failed: $project" }
    }
}
[xml]$props = Get-Content -LiteralPath (Join-Path $root 'Directory.Build.props')
$version = [string]$props.Project.PropertyGroup.ModVersion
$files = [ordered]@{}
$files['BepInEx/plugins/ManimalIcebreaker/ManimalIcebreakerClient.dll'] = Join-Path $root 'icebreaker-client/bin/Release/netstandard2.1/ManimalIcebreakerClient.dll'
$files['SPT_Runtime/user/mods/ManimalIcebreaker/icebreaker-server.dll'] = Join-Path $root 'icebreaker-server/bin/Release/icebreaker-server.dll'
foreach ($binary in @($files.Values)) {
    if ([Reflection.AssemblyName]::GetAssemblyName($binary).Version -ne [Version]"$version.0") { throw "Wrong binary version: $binary" }
}
$db = Join-Path $root 'icebreaker-server/db'
foreach ($file in Get-ChildItem -LiteralPath $db -Recurse -File | Sort-Object FullName) {
    if ($file.Name -match '^icebreaker_visits\.json|\.bak' -or $file.Extension -in '.md', '.cs', '.pdb') { continue }
    $relative = $file.FullName.Substring($db.Length + 1).Replace('\', '/')
    $files["SPT_Runtime/user/mods/ManimalIcebreaker/db/$relative"] = $file.FullName
}
$files['SPT_Runtime/user/mods/ManimalIcebreaker/bundles.json'] = Join-Path $root 'icebreaker-server/bundles.json'
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$zip = Join-Path $OutputDirectory 'Manimal-Icebreaker-zzap-v3.zip'
$stream = [IO.File]::Open($zip, [IO.FileMode]::Create, [IO.FileAccess]::ReadWrite)
$archive = [IO.Compression.ZipArchive]::new($stream, [IO.Compression.ZipArchiveMode]::Create)
try {
    foreach ($entry in $files.GetEnumerator()) {
        [void][IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, $entry.Value, $entry.Key, [IO.Compression.CompressionLevel]::Optimal)
    }
} finally { $archive.Dispose(); $stream.Dispose() }
$archive = [IO.Compression.ZipFile]::OpenRead($zip)
try {
    if ($archive.Entries.Count -ne $files.Count) { throw 'Archive file count mismatch' }
    foreach ($entry in $archive.Entries) {
        if ($entry.FullName.Contains('\') -or $entry.FullName -match '(^|/)\.\./|icebreaker_visits\.json|\.pdb$|\.cs$') { throw "Unexpected archive entry: $($entry.FullName)" }
        $inputStream = $entry.Open()
        try { $actual = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($inputStream)) } finally { $inputStream.Dispose() }
        $expected = (Get-FileHash -LiteralPath $files[$entry.FullName] -Algorithm SHA256).Hash
        if ($actual -ne $expected) { throw "Archive hash mismatch: $($entry.FullName)" }
    }
} finally { $archive.Dispose() }
$hash = (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash
"$hash  $([IO.Path]::GetFileName($zip))" | Set-Content -LiteralPath (Join-Path $OutputDirectory 'zzap-v3-SHA256SUMS.txt') -Encoding utf8
Write-Output "Verified $($files.Count) archive entries against source hashes."
Get-Item -LiteralPath $zip | Select-Object Name,Length
Write-Output "SHA256 $hash"
