using System.Globalization;
using System.IO;
using System.Windows;
using EqlPathfinder.Engine.Analysis;
using EqlPathfinder.Engine.Combat;

namespace EqlPathfinder.App;

public partial class App
{
    // ------------------------------------------------------------------ fight meter

    private FightMeterService? _meter;
    private MeterWindow? _meterWindow;
    /// <summary>The trio timeline the meter last got (a new meter gets the current one with the next snapshot).</summary>
    private volatile TrioTimeline? _sentTimeline;

    /// <summary>
    /// The fight meter reads the same logs on its own (fights.db), and only while you have it switched on. The first time
    /// it reads your past fights, which is where "your usual" comes from.
    /// </summary>
    private void StartMeter()
    {
        StopMeter();
        if (!_settings.Overlay(OverlayKinds.Meter).Visible || _service == null) return;
        try
        {
            var meter = new FightMeterService(_service.Logs, Path.Combine(AppSettings.DataDir, "fights.db"))
            {
                GameRunning = _game?.Running == true,
                LingerSeconds = MeterFightEnd(),
            };
            meter.Error += ex => AppSettings.LogError("fight meter", ex);
            meter.FrameChanged += frame => Dispatcher.BeginInvoke(() => { if (_meter == meter && _meterWindow?.IsVisible == true) _meterWindow.Update(frame); });
            _meter = meter;
            _sentTimeline = null;   // the next snapshot hands it the app's trios
            _meterWindow ??= new MeterWindow(_settings);
            meter.Start();
        }
        catch (Exception ex) { AppSettings.LogError("starting the fight meter", ex); }
    }

    private void StopMeter()
    {
        var meter = _meter;
        _meter = null;
        if (meter != null) Task.Run(meter.Dispose);
    }

    // ------------------------------------------------------------------ overlays

    /// <summary>The overlay windows made so far (when tracking starts), with their shared plumbing.</summary>
    private IEnumerable<OverlayChrome> OverlayChromes()
    {
        if (_overlay != null) yield return _overlay.Chrome;
        if (_meterWindow != null) yield return _meterWindow.Chrome;
    }

    private bool _arranging;
    internal bool Arranging => _arranging;
    internal bool OverlaysOnlyWithGame => _settings.OverlaysOnlyWithGame;
    internal OverlayPrefs OverlayPrefs(string id) => _settings.Overlay(id);

    /// <summary>Every overlay that's switched on is locked (what "Lock overlays" shows).</summary>
    internal bool AllOverlaysLocked =>
        OverlayKinds.All.Select(k => _settings.Overlay(k.Id)).Where(p => p.Visible).DefaultIfEmpty().All(p => p == null || p.Locked);

    /// <summary>
    /// Each overlay shows when it's switched on and (unless you turned that off) the game is running; while you arrange
    /// them, every switched-on overlay shows whether the game runs or not.
    /// </summary>
    private void ApplyOverlayVisibility()
    {
        bool now = !_settings.OverlaysOnlyWithGame || _game?.Running == true || _arranging;
        if (_meterWindow != null)
            ShowOrHide(_meterWindow, _settings.Overlay(OverlayKinds.Meter).Visible && _meter != null && now,
                () => { if (_meter?.Frame is { } frame) _meterWindow.Update(frame); });
        if (_overlay != null)
            ShowOrHide(_overlay, _settings.Overlay(OverlayKinds.Crawl).Visible && now, () => { if (LastSnapshot != null) _overlay.Update(LastSnapshot); });
    }

    private static void ShowOrHide(Window w, bool show, Action refresh)
    {
        if (show && !w.IsVisible) { refresh(); w.Show(); }
        else if (!show && w.IsVisible) w.Hide();
    }

    private void OverlaysChanged()
    {
        _settings.Save();
        _tray?.Refresh();
        _main?.Refresh();
    }

    internal void SetOverlayVisible(string id, bool on)
    {
        var p = _settings.Overlay(id);
        if (p.Visible == on) return;
        p.Visible = on;
        if (id == OverlayKinds.Meter) { if (on) StartMeter(); else StopMeter(); }
        ApplyOverlayVisibility();
        OverlaysChanged();
    }

    internal void SetOverlayLocked(string id, bool on)
    {
        _settings.Overlay(id).Locked = on;
        foreach (var c in OverlayChromes()) if (c.Id == id) c.ApplyLock();
        OverlaysChanged();
    }

    /// <summary>Lock or unlock every overlay at once (the tray compass's double-click, and Settings).</summary>
    internal void SetAllOverlaysLocked(bool on)
    {
        foreach (var k in OverlayKinds.All) _settings.Overlay(k.Id).Locked = on;
        foreach (var c in OverlayChromes()) c.ApplyLock();
        OverlaysChanged();
    }

    internal void ToggleOverlaysLocked()
    {
        if (_arranging) SetArranging(false);
        else SetAllOverlaysLocked(!AllOverlaysLocked);
    }

    internal void SetOverlayOpacity(string id, double opacity)
    {
        _settings.Overlay(id).Opacity = Math.Clamp(opacity, 0.3, 1.0);
        foreach (var c in OverlayChromes()) if (c.Id == id) c.ApplyBackground();
        OverlaysChanged();
    }

    /// <summary>One of an overlay's own choices (crawl: size, mode).</summary>
    internal void SetOverlayOption(string id, string key, string value)
    {
        _settings.Overlay(id).Options[key] = value;
        if (id == OverlayKinds.Crawl && key == "mode" && _service != null) _service.ModeSetting = value;
        if (id == OverlayKinds.Crawl && key == "size") _overlay?.ApplySize();
        if (id == OverlayKinds.Meter && key == "fightEnd" && _meter != null) _meter.LingerSeconds = MeterFightEnd();
        OverlaysChanged();
    }

    /// <summary>The meter's "A fight ends" setting: seconds a mob that hasn't died keeps its fight open.</summary>
    private double MeterFightEnd() =>
        double.TryParse(_settings.Overlay(OverlayKinds.Meter).Options.GetValueOrDefault("fightEnd"), NumberStyles.Float,
            CultureInfo.InvariantCulture, out var s) ? s : FightTracker.DefaultLingerSeconds;

    internal void ToggleOnlyWithGame()
    {
        _settings.OverlaysOnlyWithGame = !_settings.OverlaysOnlyWithGame;
        ApplyOverlayVisibility();
        OverlaysChanged();
    }

    /// <summary>
    /// Arranging: every overlay that's switched on shows (game running or not), unlocked, outlined and named, so you can
    /// place them; done puts the locks and the game rule back.
    /// </summary>
    internal void SetArranging(bool on)
    {
        if (_arranging == on) return;
        _arranging = on;
        if (on) UsageService.Metrics.Feature("arrange");
        OverlayChrome.Arranging = on;
        if (on && _settings.Overlay(OverlayKinds.Meter).Visible && _meter == null) StartMeter();
        ApplyOverlayVisibility();
        foreach (var c in OverlayChromes()) c.ApplyArranging();
        OverlaysChanged();
        if (on) _tray?.Notify("Arranging the overlays", "Drag them where you want them. Choose Done arranging in the tray menu, or double-click the compass.");
    }
}
