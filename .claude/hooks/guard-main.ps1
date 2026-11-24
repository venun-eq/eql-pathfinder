# PreToolUse hook for Edit, Write and NotebookEdit (.claude\settings.json). It refuses a write inside the main
# checkout, the release lane that tools\ship.ps1 releases from every hour (CLAUDE.md, "Never in the main checkout").
# Exit 2 refuses the call, and Claude reads what was written to stderr. Exit 0 lets it through. A guard that breaks
# must never stop work, so anything unexpected is exit 0. tools\check.ps1 runs it on samples.
try {
    $in = New-Object IO.MemoryStream
    [Console]::OpenStandardInput().CopyTo($in)
    $call = [Text.Encoding]::UTF8.GetString($in.ToArray()).TrimStart([char]0xFEFF) | ConvertFrom-Json
    $path = $call.tool_input.file_path
    if (-not $path) { $path = $call.tool_input.notebook_path }
    if (-not $path) { exit 0 }
    # Git Bash's form of a path, /c/Users/..., which the file tools take as well.
    $posix = [regex]::Match($path, '^/([a-zA-Z])/(.*)$')
    if ($posix.Success) { $path = "$($posix.Groups[1].Value):\$($posix.Groups[2].Value)" }
    if (-not [IO.Path]::IsPathRooted($path)) { $path = Join-Path $call.cwd $path }
    $path = [IO.Path]::GetFullPath($path)

    # The main checkout holds the repository's .git folder. A worktree has a .git file that points into it, and this
    # script runs from whichever checkout the session started in.
    $here = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot "..\.."))
    $dotGit = Join-Path $here ".git"
    if (Test-Path $dotGit -PathType Container) { $main = $here }
    elseif (Test-Path $dotGit -PathType Leaf) {
        $m = [regex]::Match([IO.File]::ReadAllText($dotGit), 'gitdir:\s*(.+?)[\\/]\.git[\\/]worktrees[\\/]')
        if (-not $m.Success) { exit 0 }
        $main = [IO.Path]::GetFullPath($m.Groups[1].Value)
    }
    else { exit 0 }

    $main = $main.TrimEnd('\')
    if (-not $path.StartsWith($main + '\', [StringComparison]::OrdinalIgnoreCase)) { exit 0 }
    $rel = $path.Substring($main.Length + 1)
    [Console]::Error.WriteLine("Refused: $main is the release lane, and nothing is edited there (CLAUDE.md). " +
        "Work in a worktree: .\tools\feature.ps1 start <name> makes $main-wt\<name>, and $rel is edited there. " +
        ".\tools\feature.ps1 list shows the worktrees in flight.")
    exit 2
}
catch { exit 0 }
