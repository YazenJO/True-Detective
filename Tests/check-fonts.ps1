# Reads each font's cmap table directly and reports whether it covers the Arabic
# Presentation Forms-B block (U+FE70-FEFF). ArabicText.Fix outputs codepoints from that
# block, so a font without it renders the entire game as empty boxes.
param(
    [string] $FontDir = 'E:\GameDev\Projects\TrueDetective\Assets\Resources\Fonts'
)
$ErrorActionPreference = 'Stop'

function Read-CmapCodepoints([string]$path) {
    $b = [System.IO.File]::ReadAllBytes($path)

    function U16([int]$o) { return ([int]$b[$o] -shl 8) -bor [int]$b[$o+1] }
    function U32([int]$o) { return ([long]$b[$o] -shl 24) -bor ([long]$b[$o+1] -shl 16) -bor ([long]$b[$o+2] -shl 8) -bor [long]$b[$o+3] }

    $tag = [System.Text.Encoding]::ASCII.GetString($b[0..3])
    $base = 0
    if ($tag -eq 'ttcf') { $base = [int](U32 12) }   # font collection: use the first face

    $numTables = U16 ($base + 4)
    $cmapOff = -1
    for ($i = 0; $i -lt $numTables; $i++) {
        $rec = $base + 12 + ($i * 16)
        if ([System.Text.Encoding]::ASCII.GetString($b[$rec..($rec+3)]) -eq 'cmap') {
            $cmapOff = [int](U32 ($rec + 8)); break
        }
    }
    if ($cmapOff -lt 0) { throw "no cmap table" }

    # prefer a format 4 / format 12 unicode subtable
    $nSub = U16 ($cmapOff + 2)
    $best = -1; $bestFormat = -1
    for ($i = 0; $i -lt $nSub; $i++) {
        $rec = $cmapOff + 4 + ($i * 8)
        $plat = U16 $rec
        $enc  = U16 ($rec + 2)
        $off  = $cmapOff + [int](U32 ($rec + 4))
        $fmt  = U16 $off
        $unicode = ($plat -eq 3 -and ($enc -eq 1 -or $enc -eq 10)) -or ($plat -eq 0)
        if ($unicode -and ($fmt -eq 4 -or $fmt -eq 12)) {
            if ($fmt -gt $bestFormat) { $best = $off; $bestFormat = $fmt }
        }
    }
    if ($best -lt 0) { throw "no usable unicode cmap subtable" }

    $set = New-Object 'System.Collections.Generic.HashSet[int]'

    if ($bestFormat -eq 4) {
        $segX2 = U16 ($best + 6)
        $seg = $segX2 / 2
        $endO   = $best + 14
        $startO = $endO + $segX2 + 2
        $deltaO = $startO + $segX2
        $rangeO = $deltaO + $segX2
        for ($s = 0; $s -lt $seg; $s++) {
            $end   = U16 ($endO + $s*2)
            $start = U16 ($startO + $s*2)
            if ($start -eq 0xFFFF) { continue }
            for ($c = $start; $c -le $end -and $c -ne 0xFFFF; $c++) {
                $ro = U16 ($rangeO + $s*2)
                if ($ro -eq 0) {
                    $d = U16 ($deltaO + $s*2)
                    if ((($c + $d) -band 0xFFFF) -ne 0) { [void]$set.Add($c) }
                } else {
                    $gi = $rangeO + $s*2 + $ro + ($c - $start)*2
                    if ($gi + 1 -lt $b.Length -and (U16 $gi) -ne 0) { [void]$set.Add($c) }
                }
            }
        }
    } else {
        $nGroups = [int](U32 ($best + 12))
        for ($g = 0; $g -lt $nGroups; $g++) {
            $go = $best + 16 + ($g * 12)
            $s0 = [int](U32 $go); $e0 = [int](U32 ($go + 4))
            if ($s0 -gt 0xFFFF) { continue }
            for ($c = $s0; $c -le [Math]::Min($e0, 0xFFFF); $c++) { [void]$set.Add($c) }
        }
    }
    return $set
}

$fonts = Get-ChildItem (Join-Path $FontDir '*.ttf')
$allOk = $true

foreach ($f in $fonts) {
    "=== $($f.Name) ==="
    try {
        $cp = Read-CmapCodepoints $f.FullName

        # the exact ranges ArabicText.Fix can emit
        $ranges = @(
            @{ n = 'Arabic base      U+0620-064A'; a = 0x0620; b = 0x064A },
            @{ n = 'Presentation-B   U+FE70-FEFF'; a = 0xFE70; b = 0xFEFF },
            @{ n = 'lam-alef ligs    U+FEF5-FEFC'; a = 0xFEF5; b = 0xFEFC },
            @{ n = 'ASCII digits     U+0030-0039'; a = 0x0030; b = 0x0039 },
            @{ n = 'Latin caps       U+0041-005A'; a = 0x0041; b = 0x005A }
        )

        foreach ($r in $ranges) {
            $have = 0; $total = $r.b - $r.a + 1
            for ($c = $r.a; $c -le $r.b; $c++) { if ($cp.Contains($c)) { $have++ } }
            $pct = [math]::Round(100 * $have / $total)
            $verdict = if ($pct -ge 90) { 'OK  ' } elseif ($pct -ge 50) { 'PART' } else { 'MISS' }
            if ($verdict -ne 'OK  ' -and $r.n -notmatch 'lam-alef') { $allOk = $false }
            "  [$verdict] $($r.n)  $have/$total  ($pct%)"
        }

        # the specific glyphs the case text needs most
        $probes = @{
            'FEDF lam initial'    = 0xFEDF
            'FEE0 lam medial'     = 0xFEE0
            'FEFB lam-alef iso'   = 0xFEFB
            'FEFC lam-alef fin'   = 0xFEFC
            'FE8E alef final'     = 0xFE8E
            'FEA4 hah medial'     = 0xFEA4
            'FECC ain medial'     = 0xFECC
            'FEF4 yeh medial'     = 0xFEF4
        }
        $missing = @()
        foreach ($k in $probes.Keys) { if (-not $cp.Contains($probes[$k])) { $missing += $k } }
        if ($missing.Count -eq 0) { "  [OK  ] all probe glyphs present" }
        else { "  [MISS] missing: $($missing -join ', ')"; $allOk = $false }

        "  total codepoints: $($cp.Count)"
    } catch {
        "  [ERROR] $($_.Exception.Message)"
        $allOk = $false
    }
    ''
}

if ($allOk) { 'ALL FONTS USABLE' } else { 'FONT PROBLEM - see above'; exit 1 }
