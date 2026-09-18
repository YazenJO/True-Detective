# Headless playthrough of the whole case: compiles the Unity-free logic files with the
# in-box C# compiler, hydrates case01.json by reflection, then walks the intended path
# and every wrong path the design promises to handle.
# ASCII only - Arabic assertions live in test-playthrough-strings.txt.
$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

$root = 'E:\GameDev\Projects\TrueDetective\Assets\Scripts'
$files = @(
    "$root\Core\ArabicText.cs",
    "$root\Data\CaseData.cs",
    "$root\Core\CaseSession.cs"
)

# One compilation unit, so every `using` has to be hoisted above the namespaces.
$usings = New-Object 'System.Collections.Generic.HashSet[string]'
$bodies = @()
foreach ($f in $files) {
    $kept = @()
    foreach ($line in (Get-Content $f -Encoding UTF8)) {
        if ($line -match '^\s*using\s+[A-Za-z0-9_.]+\s*;\s*$') { [void]$usings.Add($line.Trim()) }
        else { $kept += $line }
    }
    $bodies += ($kept -join "`n")
}
$unit = (($usings | Sort-Object) -join "`n") + "`n" + ($bodies -join "`n")

Add-Type -TypeDefinition $unit -Language CSharp

# ---- JSON -> typed object, by reflection over public fields ----
function ConvertTo-Typed {
    param([Parameter(Mandatory)] $Node, [Parameter(Mandatory)][Type] $Type)

    if ($null -eq $Node) { return $null }

    if ($Type.IsArray) {
        $elem = $Type.GetElementType()
        $items = @($Node)
        $arr = [Array]::CreateInstance($elem, $items.Count)
        for ($i = 0; $i -lt $items.Count; $i++) {
            $arr.SetValue((ConvertTo-Typed -Node $items[$i] -Type $elem), $i)
        }
        # leading comma stops PowerShell unrolling the array back into Object[]
        return ,$arr
    }

    if ($Type -eq [string])  { return [string]$Node }
    if ($Type -eq [int])     { return [int]$Node }
    if ($Type -eq [float])   { return [float]$Node }
    if ($Type -eq [bool])    { return [bool]$Node }

    $obj = [Activator]::CreateInstance($Type)
    foreach ($f in $Type.GetFields([Reflection.BindingFlags]::Public -bor [Reflection.BindingFlags]::Instance)) {
        $prop = $Node.PSObject.Properties[$f.Name]
        if ($null -eq $prop) { continue }
        $f.SetValue($obj, (ConvertTo-Typed -Node $prop.Value -Type $f.FieldType))
    }
    return $obj
}

$raw  = Get-Content 'E:\GameDev\Projects\TrueDetective\Assets\Resources\Cases\case01.json' -Raw -Encoding UTF8 | ConvertFrom-Json
$case = ConvertTo-Typed -Node $raw -Type ([TrueDetective.Data.CaseData])

$script:pass = 0; $script:fail = 0
function Check([string]$what, [bool]$ok) {
    if ($ok) { $script:pass++; "  [ok]   $what" }
    else     { $script:fail++; "  [FAIL] $what" }
}

'=== case loaded ==='
Check "5 evidence"   ($case.evidence.Length  -eq 5)
Check "3 locations"  ($case.locations.Length -eq 3)
Check "7 slots"      ($case.report.slots.Length -eq 7)
''

# =====================================================================
'=== A. intended path ==='
$s = New-Object TrueDetective.Core.CaseSession($case)

Check "starts in the office"        ($s.CurrentLocationId -eq 'office')
Check "stage = Investigate"         ($s.CurrentStage -eq [TrueDetective.Core.Stage]::Investigate)
Check "E5 is gated at the start"    (-not $s.CollectEvidence('E5'))
Check "E5 not on file"              (-not $s.HasEvidence('E5'))

Check "travel to the archive"       ($s.TravelTo('archive_hall'))
Check "collect E1"                  ($s.CollectEvidence('E1'))
Check "E1 does not duplicate"       (-not $s.CollectEvidence('E1'))

# interrogate Samer
$qs = @($s.AvailableQuestions('samer'))
Check "Samer has 3 questions before the confrontation" ($qs.Count -eq 3)
Check "s_q4 is hidden before it"    (-not ($qs | Where-Object { $_.id -eq 's_q4' }))
$q1 = $qs | Where-Object { $_.id -eq 's_q1' }
$s.AskQuestion('samer', $q1)
Check "E2 filed from s_q1"          ($s.HasEvidence('E2'))

Check "travel to the guard room"    ($s.TravelTo('guard_room'))
Check "collect E3"                  ($s.CollectEvidence('E3'))

$nq = @($s.AvailableQuestions('nader'))
Check "Nader has 3 questions before it" ($nq.Count -eq 3)
Check "n_q4 hidden before CF1"          (-not ($nq | Where-Object { $_.id -eq 'n_q4' }))
$n1 = $nq | Where-Object { $_.id -eq 'n_q1' }
$s.AskQuestion('nader', $n1)
Check "E4 filed from n_q1"          ($s.HasEvidence('E4'))
Check "stage = Connect"             ($s.CurrentStage -eq [TrueDetective.Core.Stage]::Connect)

# connect
$m = $null
$bad = $s.TryConnect('E1', 'E4', 'contradicts', [ref]$m)
Check "wrong pair is rejected"      ($bad -eq [TrueDetective.Core.ConnectResult]::Wrong)
$badRel = $s.TryConnect('E2', 'E3', 'supports', [ref]$m)
Check "wrong relation is rejected"  ($badRel -eq [TrueDetective.Core.ConnectResult]::Wrong)
$good = $s.TryConnect('E2', 'E3', 'contradicts', [ref]$m)
Check "E2+E3 contradicts accepted"  ($good -eq [TrueDetective.Core.ConnectResult]::Correct)
$again = $s.TryConnect('E3', 'E2', 'contradicts', [ref]$m)
Check "re-asserting is AlreadyKnown, order-free" ($again -eq [TrueDetective.Core.ConnectResult]::AlreadyKnown)
Check "stage = Confront"            ($s.CurrentStage -eq [TrueDetective.Core.Stage]::Confront)

# premature accusation - the lesson the case is built on
$ans = New-Object 'System.Collections.Generic.Dictionary[string,string]'
foreach ($sl in $case.report.slots) { $ans[$sl.key] = $sl.correct }
$early = $s.SubmitReport($ans, [string[]]@('E1','E2','E3','E4'))
Check "perfect slots still rejected without E5" (-not $early.Accepted)
Check "flagged as premature accusation"         ($early.PrematureAccusation)
Check "case is not closed"                      (-not $s.Solved)

# confront
$cf = $s.PendingConfrontation('samer')
Check "confrontation is open for Samer"  ($null -ne $cf)
Check "no confrontation for Nader"       ($null -eq $s.PendingConfrontation('nader'))
$s.CompleteConfrontation($cf)
Check "Samer is marked confronted"       ($s.HasConfrontedCharacter('samer'))

$qs2 = @($s.AvailableQuestions('samer'))
Check "s_q4 opens after the confrontation" ([bool]($qs2 | Where-Object { $_.id -eq 's_q4' }))
$q1b = $qs2 | Where-Object { $_.id -eq 's_q1' }
Check "Samer's answer changes"           ($s.AnswerFor('samer', $q1b) -ne $q1b.answer)

$acts = @($s.AvailableActions())
Check "the footage request is unlocked"  ($acts.Count -eq 1 -and $acts[0].id -eq 'act_request_room_footage')

$nq2 = @($s.AvailableQuestions('nader'))
$n4 = $nq2 | Where-Object { $_.id -eq 'n_q4' }
Check "n_q4 opens via requires=CF1"        ($null -ne $n4)
Check "n_q4 has answer text"               (-not [string]::IsNullOrEmpty($s.AnswerFor('nader', $n4)))
Check "E5 still not on file"               (-not $s.HasEvidence('E5'))

Check "performing the action works"      ($s.PerformAction('act_request_room_footage'))
Check "E5 is now on file"                ($s.HasEvidence('E5'))
Check "the action cannot repeat"         (-not $s.PerformAction('act_request_room_footage'))
Check "stage = Report"                   ($s.CurrentStage -eq [TrueDetective.Core.Stage]::Report)

# report
$partial = New-Object 'System.Collections.Generic.Dictionary[string,string]'
foreach ($sl in $case.report.slots) { $partial[$sl.key] = $sl.correct }
$partial['code'] = "$([char]0x0623)-74"   # the alef-47 / alef-74 trap
$r1 = $s.SubmitReport($partial, [string[]]@('E1','E3','E5'))
Check "one wrong slot fails"             (-not $r1.Accepted)
Check "score is 6 of 7"                  ($r1.CorrectSlots -eq 6 -and $r1.TotalSlots -eq 7)
Check "not flagged premature"            (-not $r1.PrematureAccusation)

$r2 = $s.SubmitReport($ans, [string[]]@('E1','E3'))
Check "missing E5 in the citation fails" (-not $r2.Accepted)
Check "E5 is named as missing"           ($r2.MissingEvidence -contains 'E5')

$r3 = $s.SubmitReport($ans, [string[]]@('E1','E3','E5'))
Check "full correct report is accepted"  ($r3.Accepted)
Check "case is solved"                   ($s.Solved)
Check "stage = Closed"                   ($s.CurrentStage -eq [TrueDetective.Core.Stage]::Closed)
''

# =====================================================================
'=== B. a different collection order ==='
$s2 = New-Object TrueDetective.Core.CaseSession($case)
$s2.TravelTo('guard_room') | Out-Null
Check "guard room reachable first"  ($s2.CurrentLocationId -eq 'guard_room')
$s2.CollectEvidence('E3') | Out-Null
$nq3 = @($s2.AvailableQuestions('nader'))
$s2.AskQuestion('nader', ($nq3 | Where-Object { $_.id -eq 'n_q1' }))
$s2.TravelTo('archive_hall') | Out-Null
$sq3 = @($s2.AvailableQuestions('samer'))
Check "s_q3 hidden while E1 is missing" (-not ($sq3 | Where-Object { $_.id -eq 's_q3' }))
$s2.CollectEvidence('E1') | Out-Null
$sq4 = @($s2.AvailableQuestions('samer'))
Check "s_q3 appears once E1 is filed"   ([bool]($sq4 | Where-Object { $_.id -eq 's_q3' }))
$s2.AskQuestion('samer', ($sq4 | Where-Object { $_.id -eq 's_q1' }))
$m2 = $null
Check "reverse order still connects"    ($s2.TryConnect('E3','E2','contradicts',[ref]$m2) -eq [TrueDetective.Core.ConnectResult]::Correct)
$s2.CompleteConfrontation($s2.PendingConfrontation('samer'))
$s2.PerformAction('act_request_room_footage') | Out-Null
Check "same ending by another route"    ($s2.SubmitReport($ans, [string[]]@('E1','E3','E5')).Accepted)
''

# =====================================================================
'=== C. hints ==='
$s3 = New-Object TrueDetective.Core.CaseSession($case)
$h1 = $s3.NextHint(); $h2 = $s3.NextHint(); $h3 = $s3.NextHint(); $h4 = $s3.NextHint()
Check "hints escalate then hold"  ($h1 -ne $h2 -and $h2 -ne $h3 -and $h3 -eq $h4)
Check "investigate has 3 hints"   ($s3.HintsAvailable([TrueDetective.Core.Stage]::Investigate) -eq 3)
Check "hints cost no progress"    ($s3.EvidenceCount -eq 0 -and -not $s3.Solved)
''

# =====================================================================
'=== D. replay resets everything ==='
$s.Reset()
Check "evidence cleared"      ($s.EvidenceCount -eq 0)
Check "not solved"            (-not $s.Solved)
Check "back in the office"    ($s.CurrentLocationId -eq 'office')
Check "contradiction cleared" (-not $s.HasProven('C1'))
Check "confrontation cleared" (-not $s.HasConfrontedCharacter('samer'))
Check "action cleared"        (-not $s.HasDoneAction('act_request_room_footage'))
Check "E5 gated again"        (-not $s.CollectEvidence('E5'))
Check "s_q4 hidden again"     (-not (@($s.AvailableQuestions('samer')) | Where-Object { $_.id -eq 's_q4' }))
Check "hints reset"           ($s.HintsUsed([TrueDetective.Core.Stage]::Investigate) -eq 0)
Check "stage = Investigate"   ($s.CurrentStage -eq [TrueDetective.Core.Stage]::Investigate)
''

"==== $script:pass passed / $script:fail failed ===="
if ($script:fail -gt 0) { exit 1 }
