# Compiles ArabicText.cs with the in-box C# compiler and shapes each test line.
# Kept ASCII-only on purpose: PowerShell 5.1 reads a BOM-less .ps1 as ANSI, so the
# Arabic lives in a separate UTF-8 file that we read with an explicit encoding.
$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

$src = Get-Content 'E:\GameDev\Staging\Scripts\Core\ArabicText.cs' -Raw -Encoding UTF8
Add-Type -TypeDefinition $src -Language CSharp

$lines = Get-Content 'E:\GameDev\Staging\arabic-test-cases.txt' -Encoding UTF8

$pass = 0; $fail = 0
foreach ($line in $lines) {
    if ([string]::IsNullOrWhiteSpace($line)) { continue }
    $fixed = [TrueDetective.Core.ArabicText]::Fix($line)

    # every Arabic letter should now be a presentation form (FB50-FEFF), not a base
    # letter from the 0600 block. Any leftover 0620-064A means shaping missed it.
    $leftover = @($fixed.ToCharArray() | Where-Object {
        [int]$_ -ge 0x0620 -and [int]$_ -le 0x064A
    })

    if ($leftover.Count -eq 0) { $pass++; $status = 'PASS' } else { $fail++; $status = 'FAIL' }
    "[$status] IN : $line"
    "       OUT: $fixed"
    if ($leftover.Count -gt 0) {
        $hex = ($leftover | ForEach-Object { 'U+{0:X4}' -f [int]$_ }) -join ' '
        "       UNSHAPED: $hex"
    }
    ''
}
"==== $pass passed / $fail failed ===="
