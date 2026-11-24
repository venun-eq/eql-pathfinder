# UserPromptSubmit hook (.claude\settings.json). It says once, as a conversation's context passes 200k, 400k and 700k
# tokens, what that costs: every step re-reads the whole context, so a long conversation makes every step dear. On
# 11-17, 83% of all tokens went to steps that already had over 200k behind them (docs\roles.md, "Why sessions stay
# small"). It never blocks a prompt: anything unexpected is exit 0 with no output. tools\check.ps1 runs it on samples.
try {
    $in = New-Object IO.MemoryStream
    [Console]::OpenStandardInput().CopyTo($in)
    $ev = [Text.Encoding]::UTF8.GetString($in.ToArray()).TrimStart([char]0xFEFF) | ConvertFrom-Json
    $transcript = $ev.transcript_path
    if (-not $transcript -or -not (Test-Path $transcript)) { exit 0 }

    # The last reply's usage is the context now. Only the transcript's tail is read: the file can be tens of MB.
    $fs = [IO.File]::Open($transcript, [IO.FileMode]::Open, [IO.FileAccess]::Read, ([IO.FileShare]::ReadWrite -bor [IO.FileShare]::Delete))
    try {
        $take = [int][Math]::Min($fs.Length, 3MB)
        $null = $fs.Seek(-$take, [IO.SeekOrigin]::End)
        $buf = New-Object byte[] $take
        $got = 0
        while ($got -lt $take) {
            $n = $fs.Read($buf, $got, $take - $got)
            if ($n -le 0) { break }
            $got += $n
        }
    }
    finally { $fs.Dispose() }
    $lines = [Text.Encoding]::UTF8.GetString($buf, 0, $got) -split "`n"
    $tokens = 0
    for ($i = $lines.Count - 1; $i -ge 0 -and -not $tokens; $i--) {
        $l = $lines[$i]
        if ($l -notmatch '"type":\s*"assistant"' -or $l -match '"isSidechain":\s*true') { continue }
        # The reply's own usage comes after its content. Text inside the content is escaped, so it can't match.
        $at = $l.LastIndexOf('"usage":')
        if ($at -lt 0) { continue }
        $usage = $l.Substring($at, [Math]::Min(700, $l.Length - $at))
        foreach ($key in 'input_tokens', 'cache_read_input_tokens', 'cache_creation_input_tokens') {
            $m = [regex]::Match($usage, '"' + $key + '":\s*(\d+)')
            if ($m.Success) { $tokens += [long]$m.Groups[1].Value }
        }
    }

    $band = 0
    foreach ($b in 200000, 400000, 700000) { if ($tokens -ge $b) { $band = $b } }
    $state = Join-Path ([IO.Path]::GetTempPath()) "eqlp-context-$("$($ev.session_id)" -replace '[^\w-]', '').txt"
    $told = 0
    if (Test-Path $state) { $told = [int](Get-Content $state -TotalCount 1) }
    if ($band -eq $told) { exit 0 }
    Set-Content $state $band
    if ($band -lt $told) { exit 0 }   # the context shrank (it was compacted): start over, quietly

    $k = [Math]::Round($tokens / 1000)
    $out = @{
        systemMessage      = "This conversation is at ${k}k tokens, and every step re-reads all of it. For something new, /clear first: the worktree, the spec and the commits carry a feature, so the conversation doesn't have to."
        hookSpecificOutput = @{
            hookEventName     = "UserPromptSubmit"
            additionalContext = "This conversation's context is ${k}k tokens, so every step here is costly. Hand reading, building and reviewing to the role agents (qa, designer, engineer, architect), and keep only decisions and their reports in this conversation."
        }
    }
    [Console]::Out.Write(($out | ConvertTo-Json -Compress))
    exit 0
}
catch { exit 0 }
