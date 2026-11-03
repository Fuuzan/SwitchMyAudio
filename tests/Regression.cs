using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using SwitchMyAudio;

internal sealed class FakeAudio : IAudioBackend
{
    internal readonly List<Device> Connected = new List<Device>();
    internal readonly List<string> Switches = new List<string>();
    internal readonly HashSet<string> MeterIds = new HashSet<string>();
    internal Action Changed;
    internal int PrepareCalls, PeakReads, SwitchAttempts, SwitchFailures, ListFailures, Disposals;
    internal bool Animate;
    readonly Stopwatch clock = Stopwatch.StartNew();
    internal FakeAudio()
    {
        Connected.Add(new Device { Id = "speakers", Name = "Speakers", Flow = 0, FormFactor = 1, Online = true, Current = true });
        Connected.Add(new Device { Id = "mic", Name = "Microphone", Flow = 1, FormFactor = 4, Online = true, Current = true });
    }
    internal IAudioBackend Factory(Action changed) { Changed = changed; return this; }
    internal void Signal() { Action changed = Changed; if (changed != null) changed(); }
    internal static Device Maxwell(bool game)
    {
        int rank; bool known = DeviceRules.KnownHeadphoneHardware(new string[] { "USB\\VID_3329&PID_4B19&MI_" + (game ? "04" : "01") }, out rank);
        return new Device { Id = game ? "maxwell-game" : "maxwell-chat", Name = game ? "Speakers (Game-Audeze Maxwell)" : "Speakers (Chat-Audeze Maxwell)",
            Online = true, FormFactor = 1, HeadphoneCategory = known, HeadphoneRank = rank, HardwareGroup = "maxwell-receiver" };
    }
    public List<Device> List()
    {
        if (ListFailures-- > 0) throw new IOException("Device snapshot temporarily unavailable.");
        return Connected.Select(d => new Device { Id = d.Id, Name = d.Name, Flow = d.Flow, FormFactor = d.FormFactor,
            Online = d.Online, Current = d.Current, HeadphoneCategory = d.HeadphoneCategory, HeadphoneRank = d.HeadphoneRank, HardwareGroup = d.HardwareGroup }).ToList();
    }
    public void Switch(Device target)
    {
        SwitchAttempts++;
        if (SwitchFailures-- > 0) throw new IOException("Receiver is not ready yet.");
        if (!Connected.Any(d => d.Id == target.Id && d.Online)) throw new IOException("Receiver disconnected.");
        foreach (Device d in Connected.Where(d => d.Flow == target.Flow)) d.Current = d.Id == target.Id;
        Switches.Add(target.Id);
    }
    public void PrepareMeters(IEnumerable<string> ids) { PrepareCalls++; MeterIds.Clear(); MeterIds.UnionWith(ids); }
    public float Peak(string id) { PeakReads++; return Animate ? (float)(.5 + .48 * Math.Sin(clock.Elapsed.TotalSeconds * 9)) : .8f; }
    public void Dispose() { Disposals++; Changed = null; MeterIds.Clear(); }
}

internal static partial class TestRunner
{
    static void Pump(int milliseconds)
    { Stopwatch watch = Stopwatch.StartNew(); while (watch.ElapsedMilliseconds < milliseconds) { Application.DoEvents(); Thread.Sleep(1); } Application.DoEvents(); }
    static bool Until(Func<bool> condition, int milliseconds, Action each = null)
    {
        Stopwatch watch = Stopwatch.StartNew();
        while (!condition() && watch.ElapsedMilliseconds < milliseconds) { if (each != null) each(); Application.DoEvents(); Thread.Sleep(1); }
        return condition();
    }
    static void Regressions(string folder)
    {
        int rank;
        Assert(DeviceRules.KnownHeadphoneHardware(new string[] { "USB\\VID_3329&PID_4B19&REV_0100&MI_04" }, out rank) && rank == 0,
            "Maxwell Game USB hardware is recognized despite a generic Speakers form factor");
        Assert(DeviceRules.KnownHeadphoneHardware(new string[] { "USB\\VID_3329&PID_4B19&MI_01" }, out rank) && rank == 3,
            "Maxwell Chat interface has lower auto-switch priority than Game");
        Assert(!DeviceRules.KnownHeadphoneHardware(new string[] { "USB\\VID_0951&PID_16DF", "USB\\VID_3329&PID_4B190" }, out rank),
            "Other USB devices and partial product-ID matches are not treated as Maxwell headphones");
        using (MeterBar meter = new MeterBar { Width = 300 })
        {
            meter.Peak = 1; meter.Advance(.016); float first = meter.DisplayPeak; meter.Advance(.016); float second = meter.DisplayPeak;
            Assert(first > 0 && first < second && second < 1, "Meter rise is interpolated across frames rather than jumping");
            meter.Peak = 0; meter.Advance(.016);
            Assert(meter.DisplayPeak > 0 && meter.DisplayPeak < second, "Meter falls smoothly with a slower release");
            meter.Advance(1); Assert(meter.DisplayPeak == 0, "Meter settles to exact silence without endless repainting");
            meter.Peak = float.NaN; meter.Advance(.033); Assert(meter.Peak == 0 && meter.DisplayPeak == 0, "Invalid meter samples cannot poison the animation");
        }
        FakeAudio fake = new FakeAudio();
        using (AppController app = new AppController(Path.Combine(folder, "fake-auto.xml"), true, fake.Factory))
        {
            app.Change(s => s.AutoHeadphones = true);
            fake.Connected.Add(FakeAudio.Maxwell(false)); fake.Signal(); Pump(95);
            fake.Connected.Add(FakeAudio.Maxwell(true)); fake.Signal();
            Assert(Until(() => fake.Switches.Count > 0, 600), "Receiver connection triggers automatic switching through the event queue"); Pump(260);
            Assert(fake.Switches.SequenceEqual(new string[] { "maxwell-game" }) && app.Devices.Find(d => d.Id == "mic").Current,
                "Staggered Chat/Game arrival chooses Game once and preserves the microphone");
            app.Switch("speakers"); int switches = fake.Switches.Count;
            for (int i = 0; i < 500; i++) fake.Signal(); Pump(160);
            Assert(fake.Switches.Count == switches && app.RefreshCount < 12, "Duplicate events are coalesced and respect a manual source choice");
            fake.Connected.RemoveAll(d => d.Id.StartsWith("maxwell")); fake.Signal(); Pump(100);
            fake.Connected.Add(FakeAudio.Maxwell(true)); fake.SwitchFailures = 2; fake.Signal();
            Assert(Until(() => fake.Switches.Count > switches, 1100) && fake.Switches.Last() == "maxwell-game",
                "A receiver that is briefly not ready is retried and switches successfully");
            fake.Connected.RemoveAll(d => d.Id.StartsWith("maxwell")); fake.Signal(); Pump(100);
            fake.Connected.Add(FakeAudio.Maxwell(false)); fake.Signal(); Pump(95); app.Change(s => s.AutoHeadphones = false);
            switches = fake.Switches.Count; Pump(300);
            Assert(fake.Switches.Count == switches, "Turning auto-switch off cancels a pending delayed switch");
            app.Change(s => s.AutoHeadphones = true); fake.Connected.RemoveAll(d => d.Id.StartsWith("maxwell")); fake.Signal(); Pump(100);
            fake.Connected.Add(FakeAudio.Maxwell(false)); fake.Signal(); Pump(95); fake.Connected.RemoveAll(d => d.Id.StartsWith("maxwell")); fake.Signal();
            switches = fake.Switches.Count; Pump(300);
            Assert(fake.Switches.Count == switches, "Disconnecting a receiver cancels its pending switch");
        }
        Assert(fake.Disposals > 0 && fake.Changed == null, "Audio callbacks and resources detach on shutdown");
        fake = new FakeAudio();
        using (AppController app = new AppController(Path.Combine(folder, "busy-events.xml"), true, fake.Factory))
        {
            app.Change(s => s.AutoHeadphones = true); fake.Connected.Add(FakeAudio.Maxwell(true));
            Assert(Until(() => fake.Switches.Count > 0, 500, fake.Signal), "Continuous device events cannot starve headphone auto-switching");
        }
        fake = new FakeAudio();
        using (AppController app = new AppController(Path.Combine(folder, "game-first.xml"), true, fake.Factory))
        {
            app.Change(s => s.AutoHeadphones = true); fake.Connected.Add(FakeAudio.Maxwell(true)); fake.Signal();
            Assert(Until(() => fake.Switches.Count > 0, 500), "Game-first receiver arrival selects Game");
            fake.Connected.Add(FakeAudio.Maxwell(false)); fake.Signal(); Pump(400);
            Assert(fake.Switches.SequenceEqual(new string[] { "maxwell-game" }), "A later Chat interface cannot replace the receiver's already-selected Game output");
            foreach (Device d in fake.Connected.Where(d => d.Flow == 0)) d.Current = d.Id == "maxwell-chat";
            fake.Signal(); Pump(200);
            Assert(fake.Connected.Find(d => d.Id == "maxwell-game").Current, "A separate delayed Windows default callback retains Game over newly arrived Chat");
            // If Windows changes the default to Chat on arrival, retain the receiver's preferred Game output.
            foreach (Device d in fake.Connected.Where(d => d.Flow == 0)) d.Current = d.Id == "maxwell-chat";
            fake.Connected.RemoveAll(d => d.Id == "maxwell-chat"); fake.Signal(); Pump(100);
            Device lateChat = FakeAudio.Maxwell(false); lateChat.Current = true; fake.Connected.Add(lateChat); fake.Signal(); Pump(350);
            Assert(fake.Switches.Last() == "maxwell-game" && !fake.Switches.Contains("maxwell-chat"), "A new receiver interface does not leave Windows on Chat instead of Game");
            fake.Connected.Add(new Device { Id = "other-headphones", Name = "Other headphones", Online = true, FormFactor = 3, HardwareGroup = "another-receiver" }); fake.Signal();
            Assert(Until(() => fake.Switches.Last() == "other-headphones", 500), "Connecting different headphones is not blocked by the previous receiver's priority");
        }
        fake = new FakeAudio();
        using (AppController app = new AppController(Path.Combine(folder, "receiver-ownership.xml"), true, fake.Factory))
        {
            app.Change(s => s.AutoHeadphones = true); fake.Connected.Add(FakeAudio.Maxwell(true)); fake.Signal();
            Until(() => fake.Switches.Count > 0, 500);
            fake.Connected.Add(new Device { Id = "newer-headphones", Name = "Newer headphones", Online = true, FormFactor = 3, HardwareGroup = "newer-receiver" }); fake.Signal();
            Assert(Until(() => fake.Switches.LastOrDefault() == "newer-headphones", 500), "The most recently connected receiver takes automatic ownership");
            int switches = fake.Switches.Count; fake.Connected.Add(FakeAudio.Maxwell(false)); fake.Signal(); Pump(350);
            Assert(fake.Switches.Count == switches && fake.Switches.Last() == "newer-headphones", "An older receiver's late Chat interface cannot override newer headphones");
            foreach (Device d in fake.Connected.Where(d => d.Flow == 0)) d.Current = d.Id == "maxwell-chat";
            fake.Signal(); Pump(200);
            Assert(fake.Connected.First(d => d.Id == "newer-headphones").Current,
                "A separate delayed Chat default callback cannot override a newer receiver");
            fake.Connected.RemoveAll(d => d.Id == "maxwell-chat"); fake.Signal(); Pump(100);
            Device chat = FakeAudio.Maxwell(false); chat.Current = true;
            foreach (Device d in fake.Connected.Where(d => d.Flow == 0)) d.Current = false;
            fake.Connected.Add(chat); fake.Signal(); Pump(350);
            Assert(fake.Switches.Last() == "newer-headphones" && fake.Connected.First(d => d.Id == "newer-headphones").Current,
                "An older receiver's late default-device change preserves ownership of newer headphones");
            fake.Connected.RemoveAll(d => d.Id.StartsWith("maxwell")); fake.Signal(); Pump(100);
            fake.Connected.Add(FakeAudio.Maxwell(true)); fake.Signal();
            Assert(Until(() => fake.Switches.Last() == "maxwell-game", 500), "Reconnecting an older receiver gives it a new automatic connection");
        }
        fake = new FakeAudio();
        using (AppController app = new AppController(Path.Combine(folder, "connection-settling.xml"), true, fake.Factory))
        {
            app.Change(s => s.AutoHeadphones = true); fake.Connected.Add(FakeAudio.Maxwell(true)); fake.Signal();
            Until(() => fake.Switches.Count > 0, 500); fake.Connected.Add(FakeAudio.Maxwell(false)); fake.Signal(); Pump(140);
            app.Switch("maxwell-chat"); int switches = fake.Switches.Count; fake.Signal(); Pump(250);
            Assert(fake.Switches.Count == switches && fake.Connected.Find(d => d.Id == "maxwell-chat").Current,
                "An explicit Chat selection cancels connection settling and remains selected");
            fake.Connected.RemoveAll(d => d.Id.StartsWith("maxwell")); fake.Signal(); Pump(100);
            fake.Connected.Add(FakeAudio.Maxwell(true)); fake.Signal(); Until(() => fake.Switches.Last() == "maxwell-game", 500);
            fake.Connected.Add(FakeAudio.Maxwell(false)); fake.Signal(); Pump(1200);
            switches = fake.Switches.Count;
            foreach (Device d in fake.Connected.Where(d => d.Flow == 0)) d.Current = d.Id == "maxwell-chat";
            fake.Signal(); Pump(200);
            Assert(fake.Switches.Count == switches && fake.Connected.Find(d => d.Id == "maxwell-chat").Current,
                "External default changes after connection settling are respected without a permanent route lock");
        }
        fake = new FakeAudio();
        using (AppController app = new AppController(Path.Combine(folder, "receiver-pending-ownership.xml"), true, fake.Factory))
        {
            app.Change(s => s.AutoHeadphones = true); fake.Connected.Add(FakeAudio.Maxwell(false)); fake.Signal(); Pump(95);
            fake.Connected.Add(new Device { Id = "newer-headphones", Name = "Newer headphones", Online = true, FormFactor = 3, HardwareGroup = "newer-receiver" }); fake.Signal();
            Until(() => fake.Switches.LastOrDefault() == "newer-headphones", 500);
            fake.Connected.Add(FakeAudio.Maxwell(true)); fake.Signal(); Pump(400);
            Assert(fake.Switches.SequenceEqual(new string[] { "newer-headphones" }), "A newer receiver supersedes both a pending older Chat switch and late Game arrival");
        }
        fake = new FakeAudio();
        using (AppController app = new AppController(Path.Combine(folder, "microphone-during-retry.xml"), true, fake.Factory))
        {
            app.Change(s => s.AutoHeadphones = true); fake.SwitchFailures = 1; fake.Connected.Add(FakeAudio.Maxwell(true)); fake.Signal(); Pump(100);
            app.Switch("mic");
            Assert(Until(() => fake.Switches.LastOrDefault() == "maxwell-game", 650) && fake.Connected.Find(d => d.Id == "mic").Current,
                "Selecting a microphone preserves a pending automatic playback switch");
        }
        fake = new FakeAudio();
        using (AppController app = new AppController(Path.Combine(folder, "game-retry-chat.xml"), true, fake.Factory))
        {
            app.Change(s => s.AutoHeadphones = true); fake.SwitchFailures = 2; fake.Connected.Add(FakeAudio.Maxwell(true)); fake.Signal(); Pump(95);
            fake.Connected.Add(FakeAudio.Maxwell(false)); fake.Signal();
            Assert(Until(() => fake.Switches.Count > 0, 1000), "Game readiness retries survive a later Chat interface arrival"); Pump(300);
            Assert(!fake.Switches.Contains("maxwell-chat") && fake.Switches.Last() == "maxwell-game", "Delayed receiver readiness never redirects a pending Game retry to Chat");
        }
        fake = new FakeAudio();
        using (AppController app = new AppController(Path.Combine(folder, "manual-before-chat.xml"), true, fake.Factory))
        {
            app.Change(s => s.AutoHeadphones = true); fake.Connected.Add(FakeAudio.Maxwell(true)); fake.Signal();
            Assert(Until(() => fake.Switches.Count > 0, 500), "Manual override scenario starts on receiver Game output");
            app.Switch("speakers"); fake.Connected.Add(FakeAudio.Maxwell(false)); fake.Signal(); Pump(400);
            Assert(fake.Switches.Last() == "speakers", "A late Chat interface cannot undo an explicit manual choice");
            fake.Connected.RemoveAll(d => d.Id.StartsWith("maxwell")); fake.Signal(); Pump(100);
            fake.Connected.Add(FakeAudio.Maxwell(true)); fake.Signal();
            Assert(Until(() => fake.Switches.Last() == "maxwell-game", 500), "A genuine receiver reconnection clears the prior manual override");
        }
        fake = new FakeAudio();
        using (AppController app = new AppController(Path.Combine(folder, "recovery-auto.xml"), true, fake.Factory))
        {
            app.Change(s => s.AutoHeadphones = true); fake.Connected.Add(FakeAudio.Maxwell(true)); fake.ListFailures = 1; fake.Signal();
            Assert(Until(() => fake.Switches.Count > 0, 2800) && fake.Switches.Last() == "maxwell-game", "Device-enumeration recovery retains a pending genuine headphone arrival");
        }
        fake = new FakeAudio();
        using (AppController app = new AppController(Path.Combine(folder, "manual-during-retry.xml"), true, fake.Factory))
        {
            app.Change(s => s.AutoHeadphones = true); fake.SwitchFailures = 1; fake.Connected.Add(FakeAudio.Maxwell(true)); fake.Signal(); Pump(100);
            app.Switch("speakers"); Pump(350);
            Assert(fake.Switches.SequenceEqual(new string[] { "speakers" }), "Manual source selection cancels a pending receiver-readiness retry");
        }
        fake = new FakeAudio();
        using (AppController app = new AppController(Path.Combine(folder, "startup-auto.xml"), true, delegate(Action changed) { fake.Connected.Add(FakeAudio.Maxwell(true)); return fake.Factory(changed); }))
        {
            app.Change(s => s.AutoHeadphones = true); fake.Signal(); Pump(130);
            Assert(fake.Switches.Count == 0, "Starting with already-connected headphones does not override an existing source choice");
            fake.Connected.Add(FakeAudio.Maxwell(false)); fake.Signal(); Pump(350);
            Assert(fake.Switches.Count == 0, "Late interfaces from headphones already connected at startup preserve the existing source choice");
        }
        fake = new FakeAudio();
        using (AppController app = new AppController(Path.Combine(folder, "ui-events.xml"), true, fake.Factory))
        using (MainWindow window = new MainWindow(app))
        {
            app.Change(s => s.Meters = true);
            window.Location = new Point(-20000, -20000); window.Size = new Size(620, 660); window.Show(); Pump(90);
            window.SampleMeters(); int preparations = fake.PrepareCalls, reads = fake.PeakReads; Pump(220);
            Assert(fake.PeakReads >= reads + 1 && fake.PeakReads <= reads + 3 && fake.PrepareCalls <= preparations + 1,
                "Smooth meter frames reuse native readings between samples and avoid rebuilding the meter set");
            window.Hide(); reads = fake.PeakReads; Pump(120);
            Assert(!window.MeterRunning && fake.PeakReads == reads && fake.MeterIds.Count == 0, "Hidden windows release meter interfaces and perform no meter reads");
            window.Location = new Point(Screen.PrimaryScreen.WorkingArea.Left + 16, Screen.PrimaryScreen.WorkingArea.Top + 16);
            window.Show(); Rectangle full = window.Bounds; window.Pin(); Pump(40);
            int paints = 0; window.Paint += delegate { paints++; }; bool mainReadyBeforeClose = false;
            PinnedWindow pinned = window.PinnedWindow;
            pinned.VisibleChanged += delegate { if (!pinned.Visible) mainReadyBeforeClose = window.Visible && paints > 0 && window.BackColor == Palette.Current.Background; };
            window.OpenFull(); Pump(60);
            Results.Add("TRANSITION ready_before_close=" + mainReadyBeforeClose + " main_paints=" + paints + " bounds_preserved=" + (window.Bounds == full));
            Assert(mainReadyBeforeClose && window.Bounds == full, "Unpin paints the themed main window before hiding pinned view and preserves full bounds");
            SourceCard originalCard = Descendants(window).OfType<SourceCard>().First(c => c.Device.Id == "speakers");
            window.SetFlow(1); window.SetFlow(0); window.Pin();
            Assert(object.ReferenceEquals(pinned, window.PinnedWindow) && object.ReferenceEquals(originalCard, Descendants(window).OfType<SourceCard>().First(c => c.Device.Id == "speakers")),
                "Pin and tab changes reuse existing windows and source controls without exporting discarded accessibility providers");
            window.OpenFull();
            window.Pin(); pinned.Close(); Pump(40);
            Assert(window.Visible && !pinned.Visible && !pinned.IsDisposed && !app.Settings.Pinned,
                "Closing the compact window restores the main view while retaining the reusable pin");
            SourceCard editing = Descendants(window).OfType<SourceCard>().First(c => c.Device.Id == "speakers");
            editing.BeginRename(); Descendants(editing).OfType<TextBox>().First().Text = "Desk output";
            fake.Connected.Add(new Device { Id = "new-output", Name = "USB speakers", Online = true, FormFactor = 1 }); app.Refresh(false);
            editing.FinishRename(true);
            Assert(app.Settings.Devices.First(b => b.Id == "speakers").Alias == "Desk output" && Descendants(window).OfType<SourceCard>().Any(c => c.Device.Id == "new-output"),
                "Saving a rename during device arrival safely rebuilds the source list");
            app.Change(s => s.Meters = false); reads = fake.PeakReads; Pump(100);
            Assert(!window.MeterRunning && fake.PeakReads == reads, "Turning meters off stops animation and native sampling");
            using (SettingsWindow settings = new SettingsWindow(app))
            {
                settings.StartPosition = FormStartPosition.Manual; settings.Location = new Point(-20000, -20000); settings.Show(); Pump(20);
                Control auto = Descendants(settings).First(c => c.AccessibleName == "Auto-switch headphones");
                Assert(!string.IsNullOrEmpty(auto.AccessibleDescription) && auto.AccessibleDescription.Length < 85, "Settings help is short and available to accessibility tools");
                ComboBox combo = Descendants(settings).OfType<ComboBox>().First(); combo.SelectedIndex = 2;
                Assert(app.Settings.PinnedFlow == 2, "The native themed dropdown commits selection changes"); settings.Close();
            }
        }
        Assert(fake.MeterIds.Count == 0, "Disposing the UI/audio controller releases all prepared meter devices");
        string blocked = Path.Combine(folder, "blocked-settings-parent"); File.WriteAllText(blocked, "not a folder");
        fake = new FakeAudio();
        using (AppController app = new AppController(Path.Combine(blocked, "settings.xml"), false, fake.Factory))
        {
            Assert(!app.Change(s => s.Theme = "Light") && app.Settings.Theme == "System" && app.LastError != null,
                "A settings-write failure rolls back the change and reports it");
        }
    }
    static void NativeRecovery()
    {
        using (AudioService audio = new AudioService(null))
        {
            audio.PrepareMeters(new string[] { "missing-quiet-switch-test-endpoint" }); int attempts = audio.MeterOpenAttempts;
            for (int i = 0; i < 300; i++) audio.Peak("missing-quiet-switch-test-endpoint");
            Assert(audio.MeterOpenAttempts == attempts && audio.PendingMeters == 1, "Unavailable meter retries are throttled rather than repeated every frame");
            audio.PrepareMeters(new string[0]); Assert(audio.CachedMeters == 0 && audio.PendingMeters == 0, "Clearing meter targets also removes failed-device retry state");
        }
        NativeShortcutLifetime();
    }
    static void NativeShortcutLifetime()
    {
        using (MessageWindow probe = new MessageWindow(".TestProbe"))
        {
            uint key = 0;
            for (uint candidate = 0x7c; candidate <= 0x87; candidate++) if (Win32.RegisterHotKey(probe.Handle, 7400, 0x4007, candidate)) { key = candidate; Win32.UnregisterHotKey(probe.Handle, 7400); break; }
            Assert(key != 0, "Shortcut lifetime test obtains an available key");
            FakeAudio fake = new FakeAudio();
            string path = Path.Combine(Path.GetTempPath(), "SwitchMyAudio-lifetime-" + Guid.NewGuid().ToString("N"), "settings.xml");
            using (AppController app = new AppController(path, false, fake.Factory))
            {
                SwitchMyAudio.Binding binding = app.Settings.For(app.Devices.First(d => d.Id == "speakers")); binding.Key = key; binding.Mods = 7; app.RegisterKeys();
                using (ShortcutDialog unused = new ShortcutDialog(app, "speakers", 0)) { }
                bool stolen = Win32.RegisterHotKey(probe.Handle, 7400, 0x4007, key); if (stolen) Win32.UnregisterHotKey(probe.Handle, 7400);
                Assert(!stolen, "Disposing an unopened shortcut dialog does not unregister active global shortcuts");
                using (ShortcutDialog capture = new ShortcutDialog(app, "speakers", 0))
                {
                    capture.StartPosition = FormStartPosition.Manual; capture.Location = new Point(-20000, -20000); capture.Show(); Pump(25);
                    bool suspended = Win32.RegisterHotKey(probe.Handle, 7400, 0x4007, key); if (suspended) Win32.UnregisterHotKey(probe.Handle, 7400);
                    Assert(suspended, "Global shortcuts pause only while the shortcut capture window is shown"); capture.Close();
                }
                stolen = Win32.RegisterHotKey(probe.Handle, 7400, 0x4007, key); if (stolen) Win32.UnregisterHotKey(probe.Handle, 7400);
                Assert(!stolen, "Closing shortcut capture restores global shortcuts");
                app.Keys.Suspend(); Win32.RegisterHotKey(probe.Handle, 7400, 0x4007, key); app.RegisterKeys();
                Assert(app.LastError != null, "A real shortcut conflict is surfaced"); Win32.UnregisterHotKey(probe.Handle, 7400); app.RegisterKeys();
                Assert(app.LastError == null, "Repairing the final shortcut conflict clears the old error");
                app.Error("Unrelated device error"); app.RegisterKeys(); Assert(app.LastError == "Unrelated device error", "Shortcut repair does not clear unrelated device errors");
            }
        }
    }
}
