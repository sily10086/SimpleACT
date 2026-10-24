Set-StrictMode -Version Latest

function Get-UnityFileHash {
    param([string]$Path)
    $Algorithm = [Security.Cryptography.SHA256]::Create()
    $Stream = [IO.File]::OpenRead($Path)
    try { return [BitConverter]::ToString($Algorithm.ComputeHash($Stream)).Replace('-', '').ToLowerInvariant() }
    finally { $Stream.Dispose(); $Algorithm.Dispose() }
}

function Find-UnityExactEditor {
    param([string]$Version, [string]$ProjectPath)
    $Candidates = [Collections.Generic.List[string]]::new()
    $Running = Get-UnityProjectProcess -ProjectPath $ProjectPath
    if ($Running -and $Running.ExecutablePath) { $Candidates.Add($Running.ExecutablePath) }
    if ($env:UNITY_EDITOR_PATH) { $Candidates.Add($env:UNITY_EDITOR_PATH) }
    if ($env:ProgramFiles) { $Candidates.Add((Join-Path $env:ProgramFiles "Unity/Hub/Editor/$Version/Editor/Unity.exe")) }
    # Unity Hub is the authority for custom install locations; do not scan whole drives.
    $HubDirectory = Join-Path $env:APPDATA 'UnityHub'
    $EditorsFile = Join-Path $HubDirectory 'editors-v2.json'
    if (Test-Path -LiteralPath $EditorsFile) {
        try {
            $Editors = Get-Content -LiteralPath $EditorsFile -Raw -Encoding UTF8 | ConvertFrom-Json
            foreach ($Entry in $Editors.data) {
                if ($Entry.version -eq $Version) { foreach ($Location in @($Entry.location)) { if ($Location) { $Candidates.Add([string]$Location) } } }
            }
        } catch { } # Stale optional Hub metadata must not hide other exact installations.
    }
    $MetadataFile = Join-Path $HubDirectory 'installedEditorBinaryMetadata.json'
    if (Test-Path -LiteralPath $MetadataFile) {
        try {
            $Metadata = Get-Content -LiteralPath $MetadataFile -Raw -Encoding UTF8 | ConvertFrom-Json
            foreach ($Property in $Metadata.PSObject.Properties) { $Candidates.Add($Property.Name) }
        } catch { }
    }
    $SecondaryFile = Join-Path $HubDirectory 'secondaryInstallPath.json'
    if (Test-Path -LiteralPath $SecondaryFile) {
        try {
            $Secondary = Get-Content -LiteralPath $SecondaryFile -Raw -Encoding UTF8 | ConvertFrom-Json
            if ($Secondary -is [string] -and $Secondary) { $Candidates.Add((Join-Path $Secondary "$Version/Editor/Unity.exe")) }
        } catch { }
    }
    foreach ($Candidate in @($Candidates | Select-Object -Unique)) {
        if (-not (Test-Path -LiteralPath $Candidate -PathType Leaf)) { continue }
        try {
            $CandidateVersion = ((Get-Item -LiteralPath $Candidate).VersionInfo.ProductVersion -split '_')[0]
            if ($CandidateVersion -eq $Version) { return (Resolve-Path -LiteralPath $Candidate).Path }
        } catch { }
    }
    throw "Exact Editor missing: $Version. Install this version in Unity Hub, or pass -EditorPath '.../Editor/Unity.exe'. The project version was not changed."
}

function Assert-UnityWritePath {
    param([string]$Root, [string]$Path)
    $RootPath = [IO.Path]::GetFullPath($Root).TrimEnd('\','/')
    $Absolute = [IO.Path]::GetFullPath($Path).TrimEnd('\','/')
    if ($Absolute -ne $RootPath -and -not $Absolute.StartsWith($RootPath + '\',[StringComparison]::OrdinalIgnoreCase)) { throw "Write path escapes its target: $Absolute" }
    $Current = $Absolute
    while ($true) {
        if (Test-Path -LiteralPath $Current) {
            if (((Get-Item -LiteralPath $Current -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { throw "Write path contains a junction or symbolic link: $Current" }
        }
        if ($Current -eq $RootPath) { break }
        $Current = Split-Path $Current -Parent
        if (-not $Current) { throw 'Cannot establish the target write boundary.' }
    }
}

function Get-UnityToolContext {
    param([string]$ProjectPath, [string]$EditorPath)
    $SkillRoot = Split-Path $PSScriptRoot -Parent
    $Manifest = Get-Content -LiteralPath (Join-Path $SkillRoot 'bundle-manifest.json') -Raw -Encoding UTF8 | ConvertFrom-Json
    if ($Manifest.name -ne 'unity-true-cli' -or $Manifest.platforms -notcontains 'windows-x64' -or -not $Manifest.files) { throw 'Invalid Unity True CLI bundle manifest.' }
    $SkillAbsolute = [IO.Path]::GetFullPath($SkillRoot).TrimEnd('\','/')
    $SeenFiles = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach ($File in $Manifest.files) {
        $Target = [IO.Path]::GetFullPath([IO.Path]::Combine($SkillRoot, $File.path))
        if (-not $Target.StartsWith($SkillAbsolute + '\',[StringComparison]::OrdinalIgnoreCase) -or -not $SeenFiles.Add($Target)) { throw 'Invalid or duplicated bundle member path.' }
        Assert-UnityWritePath -Root $SkillAbsolute -Path $Target
        if (-not [IO.File]::Exists($Target)) { throw "Bundle file missing: $($File.path)" }
        if ((Get-Item -LiteralPath $Target).Length -ne $File.size -or (Get-UnityFileHash -Path $Target) -ne $File.sha256) {
            throw "Bundle integrity mismatch: $($File.path)"
        }
    }
    foreach ($ExtraFile in Get-ChildItem -LiteralPath $SkillRoot -File -Recurse -Force) {
        if ($ExtraFile.FullName -ne (Join-Path $SkillRoot 'bundle-manifest.json') -and -not $SeenFiles.Contains($ExtraFile.FullName)) { throw "Bundle contains an unlisted file: $($ExtraFile.FullName)" }
    }
    $Context = @{ SkillRoot = $SkillRoot; Manifest = $Manifest; CliPath = Join-Path $SkillRoot 'runtime/windows-x64/unity.exe' }
    if (-not $ProjectPath) { return $Context }
    $Root = (Resolve-Path -LiteralPath $ProjectPath).Path
    $VersionFile = Join-Path $Root 'ProjectSettings/ProjectVersion.txt'
    if (-not (Test-Path -LiteralPath $VersionFile)) { throw "Not a Unity project: $Root" }
    $Match = [regex]::Match((Get-Content -LiteralPath $VersionFile -Raw -Encoding UTF8), '(?m)^m_EditorVersion:\s*(\S+)')
    $Version = $Match.Groups[1].Value
    $Compatibility = Get-Content -LiteralPath (Join-Path $SkillRoot 'references/compatibility.json') -Raw -Encoding UTF8 | ConvertFrom-Json
    $SeriesMatch = [regex]::Match($Version, '^([0-9]+\.[0-9]+)\.[0-9]+[fp][0-9]+(?:c[0-9]+)?$')
    if (-not $SeriesMatch.Success -or $SeriesMatch.Groups[1].Value -notin $Compatibility.candidate_editor_series) {
        throw "Unsupported Editor series or prerelease: $Version"
    }
    if (-not $EditorPath) {
        $EditorPath = Find-UnityExactEditor -Version $Version -ProjectPath $Root
    }
    if (-not (Test-Path -LiteralPath $EditorPath -PathType Leaf)) { throw "Exact Editor missing: $Version; pass -EditorPath with its executable" }
    $ActualVersion = ((Get-Item -LiteralPath $EditorPath).VersionInfo.ProductVersion -split '_')[0]
    if ($ActualVersion -ne $Version) { throw "Editor version mismatch: project=$Version executable=$ActualVersion" }
    $Context.ProjectPath = $Root
    $Context.Version = $Version
    $Context.EditorPath = (Resolve-Path -LiteralPath $EditorPath).Path
    $Context.PackageSource = Join-Path $SkillRoot 'assets/com.unity.pipeline'
    $Context.PackagePath = Join-Path $Root 'Tools/UnityTrueCLI/com.unity.pipeline'
    return $Context
}

function Get-UnityProjectProcess {
    param([string]$ProjectPath)
    foreach ($Process in Get-CimInstance Win32_Process -Filter "Name = 'Unity.exe'") {
        $Match = [regex]::Match([string]$Process.CommandLine, '(?i)-projectPath\s+(?:"([^"]+)"|(\S+))')
        if (-not $Match.Success) { continue }
        $Candidate = if ($Match.Groups[1].Success) { $Match.Groups[1].Value } else { $Match.Groups[2].Value }
        if ([IO.Path]::GetFullPath($Candidate).TrimEnd('\', '/') -eq $ProjectPath.TrimEnd('\', '/')) { return $Process }
    }
    return $null
}

function Test-UnityPipelineReady {
    param([string]$ProjectPath)
    $DescriptorPath = Join-Path $ProjectPath 'Library/Pipeline/.unity-pipeline-port'
    if (-not (Test-Path -LiteralPath $DescriptorPath)) { return $false }
    $Descriptor = $null
    for ($ReadAttempt = 0; $ReadAttempt -lt 5; $ReadAttempt++) {
        try {
            $Descriptor = Get-Content -LiteralPath $DescriptorPath -Raw -Encoding UTF8 | ConvertFrom-Json
            if ($Descriptor -and $Descriptor.projectPath -and $Descriptor.port -and $Descriptor.evalToken) { break }
        }
        catch { if ($ReadAttempt -eq 4) { throw 'Pipeline descriptor stayed incomplete or invalid; no reinstall was started' } }
        Start-Sleep -Milliseconds 50
    }
    if (-not $Descriptor -or -not $Descriptor.projectPath) { throw 'Pipeline descriptor stayed incomplete; no reinstall was started' }
    if ($Descriptor.projectPath.TrimEnd('\', '/') -ne $ProjectPath.TrimEnd('\', '/')) { throw 'Pipeline descriptor belongs to another project' }
    if (-not (Get-Process -Id $Descriptor.pid -ErrorAction SilentlyContinue)) { return $false }
    $Handler = [Net.Http.HttpClientHandler]::new()
    $Handler.UseProxy = $false
    $Client = [Net.Http.HttpClient]::new($Handler)
    $Client.Timeout = [TimeSpan]::FromSeconds(2)
    $Client.DefaultRequestHeaders.Authorization = [Net.Http.Headers.AuthenticationHeaderValue]::new('Bearer', $Descriptor.evalToken)
    try {
        $ReadyDeadline = [DateTime]::UtcNow.AddSeconds(30)
        do {
            $Response = $Client.GetAsync("http://127.0.0.1:$($Descriptor.port)/api/status").GetAwaiter().GetResult()
            try {
                if (-not $Response.IsSuccessStatusCode) { throw "Pipeline status HTTP $([int]$Response.StatusCode); no reinstall was started" }
                $Status = $Response.Content.ReadAsStringAsync().GetAwaiter().GetResult() | ConvertFrom-Json
            }
            finally { $Response.Dispose() }
            if ($Status.status -eq 'ready') { return $true }
            if ($Status.status -ne 'settling') { throw "Pipeline is $($Status.status); inspect Editor/dialogs; no reinstall was started" }
            Start-Sleep -Milliseconds 200
        } while ([DateTime]::UtcNow -lt $ReadyDeadline)
        throw 'Pipeline remained settling for 30 seconds; no reinstall was started'
    }
    catch [Net.Http.HttpRequestException] { return $false }
    catch [Threading.Tasks.TaskCanceledException] { return $false }
    finally { $Client.Dispose(); $Handler.Dispose() }
}

function ConvertTo-UnityProcessArgument {
    param([AllowEmptyString()][string]$Argument)
    if ($Argument -ne '' -and $Argument -notmatch '[\s"]') { return $Argument }
    $Quoted = [regex]::Replace($Argument, '(\\*)"', '${1}${1}\"')
    $Quoted = [regex]::Replace($Quoted, '(\\+)$', '${1}${1}')
    return '"' + $Quoted + '"'
}

function Get-UnityInvocation {
    param([string[]]$ArgumentList)
    $ValueFlagList = @('--format', '--color', '--proxy')
    $OptionEnd = [Array]::IndexOf($ArgumentList, '--')
    $OptionArgumentList = @(if ($OptionEnd -lt 0) { $ArgumentList } else { $ArgumentList | Select-Object -First $OptionEnd })
    for ($Index = 0; $Index -lt $OptionArgumentList.Count; $Index++) {
        $Argument = $ArgumentList[$Index]
        if ($Argument -in $ValueFlagList) { $Index++; continue }
        if (-not $Argument -or $Argument.StartsWith('-')) { continue }
        $Verb = switch ($Argument) {
            'cmd' { 'command' }
            'pipe' { 'pipeline' }
            'upgrade' { 'self-update' }
            default { $Argument }
        }
        return @{ Index = $Index; Verb = $Verb; Help = $OptionArgumentList -contains '--help' -or $OptionArgumentList -contains '-h' }
    }
    return @{ Index = -1; Verb = ''; Help = $false }
}
