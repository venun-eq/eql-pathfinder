using System.Windows;
using Forms = System.Windows.Forms;

namespace EqlPathfinder.App;

/// <summary>The compass in the notification area. Double-click locks or unlocks every overlay.</summary>
internal sealed class TrayIcon : IDisposable
{
    private readonly App _app;
    private readonly Forms.NotifyIcon _icon;
    private readonly Forms.ToolStripMenuItem _overlays, _onlyGame, _lockAll, _arrange, _sound, _restart, _check;
    private readonly List<(Forms.ToolStripMenuItem Item, string Id)> _overlayItems = new();
    private readonly Forms.ToolStripSeparator _restartSep;
    /// <summary>The fragment value items; value null = "Not sure: recommended".</summary>
    private readonly List<(Forms.ToolStripMenuItem item, double? value)> _frag = new();

    public TrayIcon(App app)
    {
        _app = app;
        var menu = new Forms.ContextMenuStrip();
        _restart = new Forms.ToolStripMenuItem("Restart to update", null, (_, _) => _app.RestartToUpdate()) { Visible = false };
        _restart.Font = new System.Drawing.Font(menu.Font, System.Drawing.FontStyle.Bold);
        _restartSep = new Forms.ToolStripSeparator { Visible = false };
        menu.Items.Add(_restart);
        menu.Items.Add(_restartSep);
        menu.Items.Add("Open EQL Pathfinder", null, (_, _) => _app.OpenMain()).Font =
            new System.Drawing.Font(menu.Font, System.Drawing.FontStyle.Bold);
        menu.Items.Add(new Forms.ToolStripSeparator());
        // One submenu for every overlay: each one's switch, then what applies to all of them.
        _overlays = new Forms.ToolStripMenuItem("Overlays");
        foreach (var k in OverlayKinds.All)
        {
            var id = k.Id;
            var item = new Forms.ToolStripMenuItem(k.Name, null, (_, _) => { _app.SetOverlayVisible(id, !_app.OverlayPrefs(id).Visible); Refresh(); })
            {
                ToolTipText = k.About,
            };
            _overlayItems.Add((item, id));
            _overlays.DropDownItems.Add(item);
        }
        _overlays.DropDownItems.Add(new Forms.ToolStripSeparator());
        _lockAll = new Forms.ToolStripMenuItem("Lock overlays (clicks pass through to the game)", null, (_, _) => { _app.SetAllOverlaysLocked(!_app.AllOverlaysLocked); Refresh(); });
        _onlyGame = new Forms.ToolStripMenuItem("Only while EverQuest Legends is running", null, (_, _) => { _app.ToggleOnlyWithGame(); Refresh(); });
        _arrange = new Forms.ToolStripMenuItem("Arrange overlays…", null, (_, _) => { _app.SetArranging(!_app.Arranging); Refresh(); });
        _overlays.DropDownItems.Add(_lockAll);
        _overlays.DropDownItems.Add(_onlyGame);
        _overlays.DropDownItems.Add(_arrange);
        menu.Items.Add(_overlays);
        _sound = new Forms.ToolStripMenuItem("Sound on drops above T5", null, (_, _) => { _app.ToggleSound(); Refresh(); });
        menu.Items.Add(_sound);
        menu.Items.Add("Send feedback or vote on what's next…", null, (_, _) => _app.OpenFeedback());
        var online = new Forms.ToolStripMenuItem("Pathfinder online");
        online.DropDownItems.Add("Discord", null, (_, _) => WebViewSecurity.OpenExternal("https://eqlpathfinder.com/discord"));
        online.DropDownItems.Add("Source on GitHub", null, (_, _) => WebViewSecurity.OpenExternal("https://eqlpathfinder.com/github"));
        online.DropDownItems.Add("Website", null, (_, _) => WebViewSecurity.OpenExternal("https://eqlpathfinder.com"));
        menu.Items.Add(online);

        // A fragment's worth depends on how high you take the gear you feed with motes (docs: The ideal split).
        var frag = new Forms.ToolStripMenuItem("Fragment value");
        var choices = new List<(string Label, double? Value)>
            { ($"Not sure: recommended  ({EqlPathfinder.Engine.Model.Potential.RecommendedFragmentValue:0.##}× today)", null) };
        foreach (var (goal, v) in EqlPathfinder.Engine.Model.Potential.FragmentValueChoices)
            choices.Add((goal >= 7 ? $"Mote-fed gear to +{goal}  ({v:0.##}×)" : $"Mote-fed gear to +{goal} or lower, or spells only  ({v:0.0}×)", v));
        foreach (var (label, v) in choices)
        {
            var item = new Forms.ToolStripMenuItem(label, null, (_, _) => { _app.SetFragmentValue(v); Refresh(); });
            _frag.Add((item, v));
            frag.DropDownItems.Add(item);
        }
        menu.Items.Add(frag);
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Change character / re-import history…", null, (_, _) => _app.ChangeCharacter());
        _check = new Forms.ToolStripMenuItem("Check for updates", null, (_, _) => _app.CheckForUpdates());
        menu.Items.Add(_check);
        menu.Items.Add("Open data folder", null, (_, _) => _app.OpenDataFolder());
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => _app.Quit());

        var iconStream = Application.GetResourceStream(new Uri("pack://application:,,,/Assets/pathfinder.ico"))!.Stream;
        _icon = new Forms.NotifyIcon
        {
            Icon = new System.Drawing.Icon(iconStream, new System.Drawing.Size(16, 16)),
            Text = "EQL Pathfinder",
            ContextMenuStrip = menu,
            Visible = true,
        };
        // Double-click: lock or unlock every overlay (or finish arranging them).
        _icon.DoubleClick += (_, _) => { _app.ToggleOverlaysLocked(); Refresh(); };
        menu.Opening += (_, _) => Refresh();
    }

    public void Refresh()
    {
        foreach (var (item, id) in _overlayItems) item.Checked = _app.OverlayPrefs(id).Visible;
        _lockAll.Checked = _app.AllOverlaysLocked;
        _lockAll.Enabled = !_app.Arranging;
        _onlyGame.Checked = _app.OverlaysOnlyWithGame;
        _arrange.Text = _app.Arranging ? "Done arranging" : "Arrange overlays…";
        _arrange.Font = new System.Drawing.Font(_arrange.Font, _app.Arranging ? System.Drawing.FontStyle.Bold : System.Drawing.FontStyle.Regular);
        _icon.Text = _app.LogOff ? "EQL Pathfinder · log is off: type /log on"
            : _app.GameRunning ? "EQL Pathfinder · tracking" : "EQL Pathfinder · waiting for EverQuest Legends";
        _sound.Checked = _app.Settings.SoundOnHighTier;
        foreach (var (item, v) in _frag)
            item.Checked = v == null ? _app.Settings.FragmentValueRecommended
                : !_app.Settings.FragmentValueRecommended && Math.Abs(v.Value - _app.Settings.FragmentValue) < 0.001;
        var pending = _app.PendingUpdate;
        _restart.Visible = _restartSep.Visible = pending != null;
        _restart.Text = $"Restart to update to {pending}";
        _check.Text = _app.AppVersion is { } ver ? $"Check for updates (you have {ver})" : "Check for updates";
    }

    public void Notify(string title, string text) =>
        _icon.ShowBalloonTip(6000, title, text, Forms.ToolTipIcon.Info);

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
    }
}
