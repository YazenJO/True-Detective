# Compiles the game scripts with the Roslyn compiler that ships inside Unity, against
# Unity's own reference assemblies plus the uGUI/TextMeshPro sources (that package ships
# as source, so there is no prebuilt DLL to reference).
#
# The uGUI sources use attributes that are internal to Unity's UI modules and reachable
# only through InternalsVisibleTo, so each assembly is built under its real Unity name -
# Unity.InternalAPIEngineBridge.004, UnityEngine.UI, Unity.TextMeshPro - which is what
# makes those grants apply.
#
# This is a real API and syntax check without needing a Unity licence, which batchmode
# does need. It cannot see meta files or asset references - only the editor resolves
# those - but it catches every compile error.
$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

$U       = 'E:\GameDev\Unity\6000.3.24f1\Editor\Data'
$dotnet  = "$U\NetCoreRuntime\dotnet.exe"
$csc     = "$U\DotNetSdkRoslyn\csc.dll"
$managed = "$U\Managed\UnityEngine"
$ugui    = "$U\Resources\PackageManager\BuiltInPackages\com.unity.ugui"
$proj    = 'E:\GameDev\Projects\TrueDetective\Assets'
$outDir  = 'E:\GameDev\Staging\compile-out'

New-Item -ItemType Directory -Force $outDir | Out-Null
foreach ($p in @($dotnet, $csc, $managed, $ugui, $proj)) {
    if (-not (Test-Path $p)) { throw "missing: $p" }
}

$refs = @(Get-ChildItem "$managed\*.dll" | ForEach-Object { $_.FullName })
$refs += "$U\NetStandard\ref\2.1.0\netstandard.dll"
$refs = @($refs | Where-Object { Test-Path $_ } | Sort-Object -Unique)

function Get-Sources([string]$dir) {
    if (-not (Test-Path $dir)) { return @() }
    return @(Get-ChildItem $dir -Recurse -Filter *.cs | ForEach-Object { $_.FullName })
}

$srcBridge = Get-Sources "$ugui\Runtime\InternalBridge"
$srcUgui   = Get-Sources "$ugui\Runtime\UGUI"
$srcTmp    = Get-Sources "$ugui\Runtime\TMP"
$srcGame   = Get-Sources "$proj\Scripts"
$srcEditor = Get-Sources "$proj\Editor"

"references : $($refs.Count) dlls"
"bridge     : $($srcBridge.Count) files"
"uGUI       : $($srcUgui.Count) files"
"TMP        : $($srcTmp.Count) files"
"game       : $($srcGame.Count) files"
"editor     : $($srcEditor.Count) files"
''

# csc chokes on very long command lines, so everything goes through a response file
function Invoke-Csc {
    param(
        [string]   $AssemblyName,
        [string[]] $Sources,
        [string[]] $ExtraRefs = @(),
        [string]   $ExtraDefines = '',
        [switch]   $Unsafe
    )

    $rsp = Join-Path $outDir "$AssemblyName.rsp"
    $dll = Join-Path $outDir "$AssemblyName.dll"

    $defines = 'UNITY_2022_1_OR_NEWER;UNITY_2023_1_OR_NEWER;UNITY_6000_0_OR_NEWER;' +
               'UNITY_EDITOR;UNITY_EDITOR_WIN;UNITY_STANDALONE_WIN;UNITY_STANDALONE;' +
               'PACKAGE_PHYSICS;PACKAGE_PHYSICS2D;PACKAGE_TILEMAP;PACKAGE_ANIMATION;PACKAGE_UITOOLKIT'
    if ($ExtraDefines) { $defines += ';' + $ExtraDefines }

    $lines = @(
        '-target:library'
        "-out:$dll"
        '-nostdlib+'
        '-noconfig'
        '-langversion:9.0'
        '-nowarn:0169,0414,0649,0618,0067,0108,0114,1701,1702,8632,0282'
        "-define:$defines"
    )
    if ($Unsafe) { $lines += '-unsafe+' }
    foreach ($r in ($refs + $ExtraRefs)) { $lines += "-r:`"$r`"" }
    foreach ($s in $Sources)             { $lines += "`"$s`"" }

    Set-Content -Path $rsp -Value $lines -Encoding UTF8

    $out = & $dotnet $csc "@$rsp" 2>&1
    return @{
        Output = $out
        Errors = @($out | Where-Object { $_ -match ': error ' })
        Warns  = @($out | Where-Object { $_ -match ': warning ' })
        Dll    = $dll
    }
}

function Report([string]$label, $result, [int]$maxShow = 15) {
    if ($result.Errors.Count -gt 0) {
        "$label FAILED - $($result.Errors.Count) errors:"
        $result.Errors | Select-Object -First $maxShow | ForEach-Object { "   $_" }
        return $false
    }
    "$label OK"
    return $true
}

# ---- Unity's own assemblies, under their real names ----
'=== Unity.InternalAPIEngineBridge.004 ==='
$rB = Invoke-Csc -AssemblyName 'Unity.InternalAPIEngineBridge.004' -Sources $srcBridge -Unsafe
if (-not (Report 'bridge' $rB)) { exit 2 }

'=== UnityEngine.UI ==='
$rU = Invoke-Csc -AssemblyName 'UnityEngine.UI' -Sources $srcUgui -ExtraRefs @($rB.Dll)
if (-not (Report 'uGUI' $rU)) { exit 2 }

'=== Unity.TextMeshPro ==='
$rT = Invoke-Csc -AssemblyName 'Unity.TextMeshPro' -Sources $srcTmp -ExtraRefs @($rB.Dll, $rU.Dll) -Unsafe
if (-not (Report 'TextMeshPro' $rT)) { exit 2 }
''

# ---- the project ----
'=== game scripts ==='
$rG = Invoke-Csc -AssemblyName 'Assembly-CSharp' -Sources $srcGame -ExtraRefs @($rB.Dll, $rU.Dll, $rT.Dll)
$gameOk = Report 'game' $rG 40
if ($rG.Warns.Count -gt 0) {
    "   warnings ($($rG.Warns.Count)):"
    $rG.Warns | Select-Object -First 10 | ForEach-Object { "     $_" }
}
''

'=== editor scripts ==='
$editorOk = $true
$rE = $null
if ($srcEditor.Count -gt 0) {
    $rE = Invoke-Csc -AssemblyName 'Assembly-CSharp-Editor' -Sources $srcEditor `
          -ExtraRefs @($rB.Dll, $rU.Dll, $rT.Dll, $rG.Dll)
    $editorOk = Report 'editor' $rE 40
}
''

$total = $rG.Errors.Count + $(if ($rE) { $rE.Errors.Count } else { 0 })
"==== $total compile errors ===="
if ($total -gt 0) { exit 1 }
