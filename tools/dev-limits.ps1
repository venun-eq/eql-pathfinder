<#
.SYNOPSIS
  Keeps dev work off the game's cores. Dot-source it, then call Set-DevLimits: this process (and everything it starts:
  builds, tests, Edge) runs on the E-cores plus at most 2 P-cores at below-normal CPU priority. Memory and disk priority
  stay normal on purpose: low ones hang the page tests (docs/page-tests.md). docs/developing-while-playing.md says how to undo it.
.DESCRIPTION
  Set EQLP_NO_LIMITS=1 to skip all of it. No E-cores found (not a hybrid CPU, or the topology can't be read): only
  below-normal. A process already on fewer cores keeps them: the mask is intersected, never widened.
#>

# Cores as @{ Class; Mask } (Class: the EfficiencyClass, higher is a faster core; Mask: its logical processors, group 0).
function Get-CoreTopology {
    if (-not ("DevLimitsNative" -as [type])) {
        Add-Type -TypeDefinition @"
using System; using System.Collections.Generic; using System.Runtime.InteropServices;
public static class DevLimitsNative {
    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool GetLogicalProcessorInformationEx(int relation, IntPtr buffer, ref int length);
    // Each core: its EfficiencyClass and the logical processors it holds (group 0 only: this PC has 24).
    public static ulong[][] Cores() {
        int len = 0; GetLogicalProcessorInformationEx(0, IntPtr.Zero, ref len);
        if (len == 0) return new ulong[0][];
        IntPtr buf = Marshal.AllocHGlobal(len);
        try {
            if (!GetLogicalProcessorInformationEx(0, buf, ref len)) return new ulong[0][];
            var list = new List<ulong[]>(); int off = 0;
            while (off < len) {
                int size = Marshal.ReadInt32(buf, off + 4);
                int cls = Marshal.ReadByte(buf, off + 8 + 1);
                ulong mask = (ulong)Marshal.ReadInt64(buf, off + 8 + 24);
                list.Add(new ulong[] { (ulong)cls, mask });
                off += size;
            }
            return list.ToArray();
        } finally { Marshal.FreeHGlobal(buf); }
    }
}
"@
    }
    @([DevLimitsNative]::Cores() | ForEach-Object { @{ Class = [int]$_[0]; Mask = [uint64]$_[1] } })
}

# The mask for dev work: every E-core and the last $MaxP P-cores. $null when all cores are alike.
function Select-DevMask($Cores, [int]$MaxP = 2) {
    $Cores = @($Cores)
    if ($Cores.Count -eq 0) { return $null }
    $top = ($Cores | ForEach-Object { $_.Class } | Measure-Object -Maximum).Maximum
    $low = @($Cores | Where-Object { $_.Class -lt $top })
    if ($low.Count -eq 0) { return $null }
    $p = @($Cores | Where-Object { $_.Class -eq $top })
    $mask = [uint64]0
    foreach ($c in $low) { $mask = $mask -bor $c.Mask }
    foreach ($c in ($p | Select-Object -Last $MaxP)) { $mask = $mask -bor $c.Mask }
    $mask
}

function Set-DevLimits {
    if ($env:EQLP_NO_LIMITS -eq "1") { return }
    $me = [Diagnostics.Process]::GetCurrentProcess()
    try {
        $mask = Select-DevMask (Get-CoreTopology)
        if ($mask) {
            $now = [uint64]$me.ProcessorAffinity.ToInt64()
            $narrow = $now -band $mask
            if ($narrow -ne 0) { $me.ProcessorAffinity = [IntPtr][int64]$narrow }
        }
    } catch { Write-Host "dev-limits: not pinned ($($_.Exception.Message))" }
    try { $me.PriorityClass = [Diagnostics.ProcessPriorityClass]::BelowNormal } catch { }
}

function Test-GameRunning { [bool](Get-Process -Name eqgame -ErrorAction SilentlyContinue) }

# MSBuild's -m: every core normally, 2 while the game runs.
function Get-BuildThreads { if (Test-GameRunning) { "-m:2" } else { "-m" } }
