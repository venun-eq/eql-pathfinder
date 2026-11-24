using System.Diagnostics.Metrics;
using System.Runtime.CompilerServices;
using EqlPathfinder.Engine.Usage;

namespace EqlPathfinder.Engine.Tests;

/// <summary>
/// Usage counts are the player's choice in the tour (docs/specs/usage-choice.md): until they answer, what setup counts
/// stays in memory, is never written to usage.json and is never pending to send; Yes keeps it, No drops it.
/// </summary>
public class UsageChoiceTests
{
    private static readonly UsageContext Who = new("0123456789abcdef0123456789abcdef", "stable", "10.0.26300.0", "2026-11-24");

    /// <summary>A first run's recorder, before the Usage step is answered: a meter of its own, so other tests never land in it.</summary>
    private static (UsageMetrics m, UsageRecorder r) Held(string? path, [CallerMemberName] string name = "")
    {
        var m = new UsageMetrics(new Meter("test-" + name + "-" + Guid.NewGuid().ToString("N")));
        return (m, new UsageRecorder(path, m, () => new DateTime(2026, 11, 24, 10, 0, 0), held: true));
    }

    private static string TempFile(out string dir)
    {
        dir = Path.Combine(Path.GetTempPath(), "eqlp-usage-choice-" + Guid.NewGuid().ToString("N"));
        return Path.Combine(dir, "usage.json");
    }

    private static long Count(UsagePayload p, string key) => p.Counts.TryGetValue(key, out var n) ? n : 0;

    [Fact]
    public void Before_the_answer_setup_is_counted_in_memory_only_and_nothing_waits_to_be_sent()
    {
        var path = TempFile(out var dir);
        try
        {
            var (m, r) = Held(path);
            m.Feature("setup-welcome");
            m.Feature("setup-usage");
            Assert.Equal(1, Count(r.Preview(Who), "feature.setup-welcome"));   // the step's own preview shows it
            r.Save();
            Assert.Empty(r.Pending(Who));
            Assert.False(File.Exists(path), "usage.json was written before the player answered");
            r.Dispose();
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }

    [Fact]
    public void Closing_setup_before_the_answer_keeps_nothing_not_even_the_setup_closed_count()
    {
        var path = TempFile(out var dir);
        try
        {
            var (m, r) = Held(path);
            m.Feature("setup-welcome");
            m.Life("setupquit");
            r.Save();       // UsageService.SetupQuit
            r.Dispose();    // closing
            Assert.False(File.Exists(path), "usage.json was left for the next start");
            var next = new UsageRecorder(path, new UsageMetrics(new Meter("next-" + Guid.NewGuid().ToString("N"))));   // the next start, unheld: reads usage.json if one was left
            Assert.Empty(next.Days);
            Assert.Empty(next.Pending(Who));
            next.Dispose();
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }

    [Fact]
    public void Yes_keeps_what_setup_counted_and_from_then_on_it_is_kept_and_sent()
    {
        var path = TempFile(out var dir);
        try
        {
            var (m, r) = Held(path);
            m.Feature("setup-welcome");
            Assert.Empty(r.Pending(Who));   // held until the answer
            r.Choose(true);
            m.Feature("setup-usage");
            var p = Assert.Single(r.Pending(Who)).Payload;
            Assert.Equal(1, Count(p, "feature.setup-welcome"));
            Assert.Equal(1, Count(p, "feature.setup-usage"));
            r.Save();
            Assert.True(File.Exists(path));
            r.Dispose();
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }

    [Fact]
    public void No_drops_what_setup_counted_and_counts_nothing_after()
    {
        var path = TempFile(out var dir);
        try
        {
            var (m, r) = Held(path);
            m.Feature("setup-welcome");
            m.Feature("setup-usage");
            r.Choose(false);
            m.Feature("setup-log");
            m.Life("setupquit");
            Assert.Empty(r.Days);
            Assert.Empty(r.Preview(Who).Counts);
            Assert.Empty(r.Pending(Who));
            r.Save();
            r.Dispose();
            Assert.False(File.Exists(path), "usage.json was written after No");
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }

    /// <summary>A settings.json written before this change has no answer in it: the copy keeps its setting and isn't asked.</summary>
    [Theory]
    [InlineData(true, UsageStart.On)]
    [InlineData(false, UsageStart.Off)]
    public void A_copy_installed_before_the_question_keeps_its_setting_and_is_never_asked(bool enabled, UsageStart start) =>
        Assert.Equal(start, UsageChoice.Read(chosen: null, enabled));

    /// <summary>A new copy, or one whose setup was closed before the Usage step was answered: asked (again), whatever UsageEnabled says.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void A_copy_that_has_not_answered_is_asked_whatever_the_switch_says(bool enabled) =>
        Assert.Equal(UsageStart.Ask, UsageChoice.Read(chosen: false, enabled));

    /// <summary>An answer stands: setup closed after it, the next start keeps it (the step shows it chosen).</summary>
    [Theory]
    [InlineData(true, UsageStart.On)]
    [InlineData(false, UsageStart.Off)]
    public void An_answer_given_stands_on_the_next_start(bool enabled, UsageStart start) =>
        Assert.Equal(start, UsageChoice.Read(chosen: true, enabled));

    /// <summary>The Usage step: the chosen card is the saved setting (none on Ask); No's line deletes only for a copy that is on and has an id.</summary>
    [Theory]
    [InlineData(UsageStart.Ask, false, null, false)]
    [InlineData(UsageStart.Ask, true, null, false)]
    [InlineData(UsageStart.On, false, true, false)]
    [InlineData(UsageStart.On, true, true, true)]
    [InlineData(UsageStart.Off, false, false, false)]
    [InlineData(UsageStart.Off, true, false, false)]
    public void The_step_shows_the_saved_setting_and_No_deletes_only_when_counts_are_on_with_an_id(UsageStart start, bool hasId, bool? selected, bool noDeletes) =>
        Assert.Equal(new UsageStep(selected, noDeletes), UsageChoice.Step(start, hasId));

    // ---- UsageSwitch: the rules for what's kept and sent (the app's UsageService only adds timers and HTTP) ----

    private sealed class FakeSettings : IUsageSettings
    {
        public bool? UsageChosen { get; set; }
        public bool UsageEnabled { get; set; }
        public string? UsageId { get; set; }
        public string? UsageSince { get; set; }
        public string[] UsageForget { get; set; } = [];
        public int Saves;
        /// <summary>What each Save wrote: the switch, the id and the pending deletes at that moment.</summary>
        public readonly List<(bool Enabled, string? Id, string[] Forget)> Written = new();
        public void Save() { Saves++; Written.Add((UsageEnabled, UsageId, UsageForget.ToArray())); }
    }

    private const string IdB = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

    private const string AnId = "abcdefabcdefabcdefabcdefabcdefab";

    private static (UsageSwitch sw, FakeSettings s, UsageMetrics m, Func<int> idsMade) Switch(bool? chosen, bool enabled, string? path = null, string? id = null, string[]? forget = null, string[]? ids = null, [CallerMemberName] string name = "")
    {
        var m = new UsageMetrics(new Meter("sw-" + name + "-" + Guid.NewGuid().ToString("N")));
        var s = new FakeSettings { UsageChosen = chosen, UsageEnabled = enabled, UsageId = id };
        if (forget != null) s.UsageForget = forget;
        int made = 0;
        var sw = new UsageSwitch(s, path, m, () => { var i = made++; return ids is { } l ? l[i] : AnId; }, () => new DateTime(2026, 11, 24, 10, 0, 0));
        return (sw, s, m, () => made);
    }

    [Fact]
    public void The_id_is_made_in_Begin_only()
    {
        var (sw, s, m, made) = Switch(chosen: false, enabled: false);
        sw.Listen();
        m.Feature("setup-welcome");
        sw.SetEnabled(true);          // Yes before the app has started
        Assert.Null(s.UsageId);
        Assert.Equal(0, made());
        Assert.True(sw.Begin("1.0.0", firstRun: true));
        Assert.Equal(AnId, s.UsageId);
        Assert.Equal("2026-11-24", s.UsageSince);
        Assert.Equal(1, made());
        sw.Begin("1.0.0", firstRun: false);   // an id that exists is kept
        Assert.Equal(1, made());
        sw.Dispose();
    }

    [Fact]
    public void A_recorder_still_held_at_Begin_is_dropped_as_a_No_and_no_id_is_made()
    {
        var (sw, s, m, made) = Switch(chosen: false, enabled: false);
        sw.Listen();
        m.Feature("setup-welcome");
        Assert.False(sw.Begin("1.0.0", firstRun: true));
        Assert.Null(sw.Recorder);
        Assert.Null(s.UsageId);
        Assert.Equal(0, made());
        Assert.False(s.UsageChosen == true, "Begin recorded an answer the player never gave");
    }

    [Fact]
    public void SetEnabled_marks_the_answer_chosen_before_its_already_so_return()
    {
        var (sw, s, _, _) = Switch(chosen: false, enabled: false);
        sw.Listen();
        var r = sw.SetEnabled(false);   // a first-run No: the switch is already off
        Assert.True(s.UsageChosen);
        Assert.False(s.UsageEnabled);
        Assert.Equal(1, s.Saves);
        Assert.Equal(default, r);
        Assert.Equal(UsageStart.Off, sw.Start);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void A_No_while_held_drops_the_counts_and_asks_for_no_delete(bool enabledInFile)
    {
        var path = TempFile(out var dir);
        try
        {
            var (sw, s, m, _) = Switch(chosen: false, enabled: enabledInFile, path);
            sw.Listen();
            m.Feature("setup-welcome");
            Assert.True(sw.Recorder!.Held);
            var r = sw.SetEnabled(false);
            Assert.Null(r.ForgetId);
            Assert.Null(sw.Recorder);
            Assert.Null(s.UsageId);
            m.Feature("setup-log");
            Assert.False(File.Exists(path), "usage.json was written after No");
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }

    [Fact]
    public void A_Yes_while_held_keeps_the_counts()
    {
        var (sw, s, m, _) = Switch(chosen: false, enabled: false);
        sw.Listen();
        m.Feature("setup-welcome");
        var held = sw.Recorder!;
        var r = sw.SetEnabled(true);
        Assert.False(r.Began);   // the app hasn't started: Begin comes later
        Assert.Same(held, sw.Recorder);
        Assert.False(held.Held);
        Assert.Equal(1, Count(Assert.Single(held.Pending(Who)).Payload, "feature.setup-welcome"));
        Assert.True(s.UsageEnabled);
        Assert.True(s.UsageChosen);
        Assert.True(sw.Begin("1.0.0", firstRun: true));
        sw.Dispose();
    }

    [Fact]
    public void A_delete_is_asked_for_only_when_there_is_an_id()
    {
        var (a, _, _, _) = Switch(chosen: true, enabled: true);
        a.Listen();
        Assert.Null(a.SetEnabled(false).ForgetId);        // on, never began: no id, nothing was sent
        var (b, _, _, _) = Switch(chosen: true, enabled: true, id: AnId);
        b.Listen();
        var off = b.SetEnabled(false);
        Assert.Equal(AnId, off.ForgetId);
        Assert.True(off.Stopped);
        Assert.Null(b.Recorder);
    }

    [Fact]
    public void A_send_round_that_started_before_a_No_may_not_post_after_it()
    {
        var (sw, _, _, _) = Switch(chosen: true, enabled: true, id: AnId);
        sw.Listen();
        sw.Begin("1.0.0", firstRun: false);
        var round = sw.Recorder!;          // the round starts with this recorder
        Assert.True(sw.MayPost(round));
        sw.SetEnabled(false);              // the No
        Assert.False(sw.MayPost(round));   // its next day is not posted
        sw.SetEnabled(true);               // a Yes again makes a new recorder: the old round still may not post
        Assert.False(sw.MayPost(round));
        Assert.True(sw.MayPost(sw.Recorder!));
        sw.Dispose();
    }

    // ---- a delete that didn't land is asked again (docs/specs/usage-choice.md, Decisions) ----

    private static (UsageSwitch sw, FakeSettings s) Begun(string? id, string[]? forget = null, bool on = true, string[]? ids = null, [CallerMemberName] string name = "")
    {
        var (sw, s, _, _) = Switch(chosen: true, enabled: on, id: id, forget: forget, ids: ids, name: name);
        sw.Listen();
        sw.Begin("1.0.0", firstRun: false);
        return (sw, s);
    }

    private static Func<string, Task> Asking(List<string> asked, params string[] failing) => id =>
    {
        asked.Add(id);
        return failing.Contains(id) ? Task.FromException(new HttpRequestException("offline")) : Task.CompletedTask;
    };

    [Fact]
    public void A_No_moves_the_id_to_the_pending_deletes_in_the_same_save()
    {
        var (sw, s) = Begun(AnId);
        var r = sw.SetEnabled(false);
        Assert.Equal(AnId, r.ForgetId);
        var save = Assert.Single(s.Written);
        Assert.False(save.Enabled);
        Assert.Null(save.Id);
        Assert.Equal(new[] { AnId }, save.Forget);
        Assert.Null(s.UsageSince);
    }

    [Fact]
    public async Task A_delete_the_server_answers_clears_its_mark()
    {
        var (sw, s) = Begun(AnId);
        sw.SetEnabled(false);
        var asked = new List<string>();
        await sw.ForgetAsync(Asking(asked), sw.Forgotten);
        Assert.Equal(new[] { AnId }, asked);
        Assert.Empty(s.UsageForget);
        Assert.Empty(s.Written[^1].Forget);   // and saved
    }

    [Fact]
    public async Task A_delete_that_fails_keeps_its_mark_and_is_asked_again()
    {
        var (sw, s) = Begun(AnId);
        sw.SetEnabled(false);
        var asked = new List<string>();
        await Assert.ThrowsAsync<HttpRequestException>(() => sw.ForgetAsync(Asking(asked, AnId), sw.Forgotten));
        Assert.Equal(new[] { AnId }, s.UsageForget);
        await sw.ForgetAsync(Asking(asked), sw.Forgotten);
        Assert.Equal(new[] { AnId, AnId }, asked);
        Assert.Empty(s.UsageForget);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task A_pending_delete_is_asked_again_by_the_next_start(bool listed)
    {
        var (sw, s) = Begun(null, forget: listed ? [AnId] : null, on: false);
        if (!listed) s.UsageForget = null!;   // a file without the field, read as null
        var asked = new List<string>();
        await sw.ForgetAsync(Asking(asked), sw.Forgotten);
        Assert.Equal(listed ? new[] { AnId } : Array.Empty<string>(), asked);
        Assert.Empty(s.UsageForget ?? []);
    }

    [Fact]
    public async Task A_Yes_while_a_delete_is_pending_counts_under_a_new_id_and_the_old_one_is_still_deleted()
    {
        var (sw, s) = Begun(AnId, ids: [IdB]);
        sw.SetEnabled(false);
        sw.SetEnabled(true);
        Assert.Equal(IdB, s.UsageId);
        Assert.Equal(new[] { AnId }, s.UsageForget);
        var seen = new List<string>();
        await sw.SendRoundAsync(() => new UsageContext(s.UsageId!, "stable", "10", s.UsageSince), null, p => { seen.Add(p.Id); return Task.CompletedTask; });
        Assert.NotEmpty(seen);
        Assert.All(seen, id => Assert.Equal(IdB, id));
        var asked = new List<string>();
        await sw.ForgetAsync(Asking(asked), sw.Forgotten);
        Assert.Equal(new[] { AnId }, asked);
        Assert.Equal(IdB, s.UsageId);
        sw.Dispose();
    }

    [Fact]
    public async Task A_second_No_before_the_first_delete_landed_keeps_both()
    {
        var (sw, s) = Begun(AnId, ids: [IdB]);
        sw.SetEnabled(false);
        sw.SetEnabled(true);
        sw.SetEnabled(false);
        Assert.Equal(new[] { AnId, IdB }, s.UsageForget);
        var asked = new List<string>();
        await Assert.ThrowsAsync<HttpRequestException>(() => sw.ForgetAsync(Asking(asked, IdB), sw.Forgotten));
        Assert.Equal(new[] { AnId, IdB }, asked);
        Assert.Equal(new[] { IdB }, s.UsageForget);
    }

    [Fact]
    public void A_copy_switched_off_before_this_has_its_delete_asked_once_more()
    {
        var (sw, s, _, _) = Switch(chosen: true, enabled: false, id: AnId);
        s.UsageForget = null!;   // a file from before the field
        sw.Listen();
        Assert.Null(s.UsageId);
        Assert.Equal(new[] { AnId }, s.UsageForget);
        Assert.Equal(new[] { AnId }, s.Written[^1].Forget);   // saved
        Assert.Null(sw.Recorder);
    }

    [Fact]
    public async Task A_copy_that_never_had_an_id_owes_no_delete()
    {
        var (a, sa, _, _) = Switch(chosen: false, enabled: false);
        a.Listen();
        a.SetEnabled(false);                      // a first-run No
        var (b, sb, _, _) = Switch(chosen: false, enabled: false);
        b.Listen();
        b.SetEnabled(true);
        b.SetEnabled(false);                      // Yes then No before Begin
        var asked = new List<string>();
        await a.ForgetAsync(Asking(asked), a.Forgotten);
        await b.ForgetAsync(Asking(asked), b.Forgotten);
        Assert.Empty(asked);
        Assert.Empty(sa.UsageForget);
        Assert.Empty(sb.UsageForget);
    }

    [Fact]
    public async Task An_id_the_server_would_refuse_is_dropped_without_a_request()
    {
        var (sw, s) = Begun(null, forget: [null!, "nothex", AnId], on: false);
        var asked = new List<string>();
        await sw.ForgetAsync(Asking(asked), sw.Forgotten);
        Assert.Equal(new[] { AnId }, asked);
        Assert.Empty(s.UsageForget);
    }

    // ---- the in-flight close: a round and a forget share one gate, with the POST and the DELETE held open ----

    /// <summary>A copy that is on, with two days waiting to be sent (yesterday and today).</summary>
    private static (UsageSwitch sw, Func<UsageContext> who) TwoDaysPending([CallerMemberName] string name = "")
    {
        var m = new UsageMetrics(new Meter("rd-" + name + "-" + Guid.NewGuid().ToString("N")));
        var s = new FakeSettings { UsageChosen = true, UsageEnabled = true, UsageId = AnId };
        var now = new DateTime(2026, 11, 23, 10, 0, 0);
        var sw = new UsageSwitch(s, null, m, () => AnId, () => now);
        sw.Listen();
        sw.Begin("1.0.0", firstRun: false);
        now = now.AddDays(1);
        m.Feature("today");
        return (sw, () => Who);
    }

    [Fact]
    public async Task A_No_in_the_middle_of_a_round_gets_no_second_post()
    {
        var (sw, who) = TwoDaysPending();
        var posted = new List<string>();
        var first = new TaskCompletionSource();
        var round = sw.SendRoundAsync(who, null, async p => { posted.Add(p.Day); await first.Task; });
        Assert.Single(posted);                 // the first POST is on the wire
        sw.SetEnabled(false);                  // the No arrives while it is held open
        first.SetResult();
        Assert.Equal(-1, await round);
        Assert.Single(posted);                 // no second POST after the No
    }

    [Fact]
    public async Task The_delete_starts_only_after_the_held_post_returns()
    {
        var (sw, who) = TwoDaysPending();
        var order = new List<string>();
        var release = new TaskCompletionSource();
        var round = sw.SendRoundAsync(who, null, async p => { order.Add("post"); await release.Task; order.Add("post done"); });
        sw.SetEnabled(false);
        var forget = sw.ForgetAsync(id => { order.Add("delete"); return Task.CompletedTask; }, sw.Forgotten);
        await Task.Delay(50);
        Assert.Equal(new[] { "post" }, order);          // the delete waits behind the POST on the wire
        release.SetResult();
        await round;
        await forget;
        Assert.Equal(new[] { "post", "post done", "delete" }, order);
    }

    [Fact]
    public async Task A_forget_waiting_behind_a_round_resumes_off_the_caller_thread()
    {
        var (sw, who) = TwoDaysPending();
        var held = new TaskCompletionSource();
        var round = sw.SendRoundAsync(who, null, _ => held.Task);
        sw.SetEnabled(false);   // the No puts the id in the list the forget works through
        var ctx = new UiLikeContext();
        var resumedOn = new TaskCompletionSource<SynchronizationContext?>();
        ctx.Post(_ => _ = sw.ForgetAsync(_ => { resumedOn.SetResult(SynchronizationContext.Current); return Task.CompletedTask; }, sw.Forgotten), null);
        await Task.Delay(50);
        held.SetResult();
        await round;
        // on the UI thread the delete would wait for the UI; closing then loses it (the client is disposed first)
        Assert.Null(await resumedOn.Task.WaitAsync(TimeSpan.FromSeconds(5)));
    }

    /// <summary>A stand-in for the UI's context: what is posted to it runs with it as the current context.</summary>
    private sealed class UiLikeContext : SynchronizationContext
    {
        public override void Post(SendOrPostCallback d, object? state) =>
            ThreadPool.QueueUserWorkItem(_ => { SetSynchronizationContext(this); d(state); });
    }
}
