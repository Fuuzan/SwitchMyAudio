using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows.Forms;
using SwitchMyAudio;

internal static partial class TestRunner
{
    [DllImport("user32.dll")] static extern uint GetGuiResources(IntPtr process, uint kind);
    sealed class ResourceSample
    {
        internal long Managed, Private, Working;
        internal int Handles;
        internal uint Gdi, User;
        public override string ToString() { return "managed=" + Managed + " private=" + Private + " working=" + Working + " handles=" + Handles + " GDI=" + Gdi + " USER=" + User; }
    }
    static ResourceSample Resources()
    {
        Application.DoEvents(); GC.Collect(); GC.WaitForPendingFinalizers(); Application.DoEvents(); GC.Collect();
        using (Process p = Process.GetCurrentProcess())
        {
            p.Refresh();
            return new ResourceSample { Managed = GC.GetTotalMemory(false), Private = p.PrivateMemorySize64, Working = p.WorkingSet64,
                Handles = p.HandleCount, Gdi = GetGuiResources(p.Handle, 0), User = GetGuiResources(p.Handle, 1) };
        }
    }
    static void Stress(string folder, int rounds)
    {
        FakeAudio fake = new FakeAudio { Animate = true };
        fake.Connected.Add(FakeAudio.Maxwell(true)); fake.Connected.Add(FakeAudio.Maxwell(false));
        fake.Connected.Add(new Device { Id = "screen", Name = "Display audio", Online = true, FormFactor = 9 });
        fake.Connected.Add(new Device { Id = "webcam", Name = "Webcam microphone", Online = true, Flow = 1, FormFactor = 4 });
        List<WeakReference> pinnedInstances = new List<WeakReference>();
        PinnedWindow cachedPin = null;
        using (AppController app = new AppController(Path.Combine(folder, "stress-unused.xml"), true, fake.Factory))
        using (MainWindow window = new MainWindow(app))
        {
            window.Location = new Point(-20000, -20000); window.Size = new Size(620, 660); window.Show();
            for (int i = 0; i < 9; i++) StressRound(app, window, i, pinnedInstances);
            RestoreStress(app, window); Pump(80); pinnedInstances.Clear(); ResourceSample before = Resources();
            cachedPin = window.PinnedWindow;
            Dictionary<string, SourceCard> originalCards = Descendants(window).OfType<SourceCard>().ToDictionary(c => c.Device.Id);
            Control[] originalPinControls = Descendants(cachedPin).ToArray();
            for (int i = 0; i < rounds; i++) StressRound(app, window, i, pinnedInstances);
            RestoreStress(app, window); Pump(80); ResourceSample after = Resources();
            Results.Add("STRESS before " + before); Results.Add("STRESS after" + rounds + " " + after);
            Assert(after.Managed - before.Managed < 3 * 1024 * 1024 && after.Gdi <= before.Gdi + 20 && after.User <= before.User + 20 && after.Handles <= before.Handles + 15,
                rounds + " mixed UI rounds retain bounded managed memory and GDI/USER/process handles");
            Assert(pinnedInstances.All(w => object.ReferenceEquals(w.Target, cachedPin)) && originalPinControls.All(c => Descendants(cachedPin).Contains(c)),
                rounds + " pin/theme/resize cycles reuse one window and the same source controls");
            Assert(originalCards.All(pair => Descendants(window).OfType<SourceCard>().Any(c => c.Device.Id == pair.Key && object.ReferenceEquals(c, pair.Value))),
                "Playback/microphone tab and visibility changes reuse known source cards");
            using (Process process = Process.GetCurrentProcess())
            {
            TimeSpan startCpu = process.TotalProcessorTime;
            int reads = fake.PeakReads, prepares = fake.PrepareCalls; Stopwatch watch = Stopwatch.StartNew(); Pump(5000);
            double cpu = (process.TotalProcessorTime - startCpu).TotalMilliseconds;
            Results.Add("PERF smooth-visible elapsed_ms=" + watch.ElapsedMilliseconds + " cpu_ms=" + cpu.ToString("F2") + " percent_one_core=" + (cpu / watch.Elapsed.TotalMilliseconds * 100).ToString("F3") +
                " peak_reads=" + (fake.PeakReads - reads) + " prepare_calls=" + (fake.PrepareCalls - prepares));
            }
            int hiddenReads = fake.PeakReads;
            window.Hide(); Pump(1000);
            Assert(fake.PeakReads == hiddenReads && !window.MeterRunning, "Stress run confirms no sampling or animation while hidden");
        }
        Assert(cachedPin.IsDisposed && !cachedPin.IsHandleCreated, "Application shutdown disposes the cached pinned window and destroys its native handle");
        Results.Add("STRESS disposed " + Resources());
        ResourceSample lifetimeBefore = Resources();
        List<WeakReference> lifetime = new List<WeakReference>();
        for (int i = 0; i < 50; i++) lifetime.AddRange(CreateDisposedUi(folder, i));
        Pump(50); ResourceSample lifetimeAfter = Resources();
        Assert(lifetime.All(reference => !reference.IsAlive), "50 complete application/window lifetimes release their managed owners and event subscriptions");
        Assert(lifetimeAfter.Gdi <= lifetimeBefore.Gdi + 15 && lifetimeAfter.User <= lifetimeBefore.User + 15 && lifetimeAfter.Handles <= lifetimeBefore.Handles + 15,
            "50 complete application/window lifetimes leave bounded GDI, USER and process handles");
        Results.Add("LIFETIME before " + lifetimeBefore); Results.Add("LIFETIME after50 " + lifetimeAfter);
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    static WeakReference[] CreateDisposedUi(string folder, int iteration)
    {
        using (AppController app = new AppController(Path.Combine(folder, "lifetime-unused-" + iteration + ".xml"), true))
        using (MainWindow window = new MainWindow(app))
        using (SettingsWindow settings = new SettingsWindow(app))
        {
            window.Location = settings.Location = new Point(-20000, -20000);
            window.Show(); window.Pin(); window.OpenFull();
            settings.StartPosition = FormStartPosition.Manual; settings.Show(); settings.ShowPage(true); settings.Close();
            return new[] { new WeakReference(app), new WeakReference(window), new WeakReference(settings), new WeakReference(window.PinnedWindow) };
        }
    }
    static void RestoreStress(AppController app, MainWindow window)
    {
        app.Change(delegate(Settings s) { s.Theme = "Dark"; s.Meters = true; foreach (SwitchMyAudio.Binding b in s.Devices) b.Hidden = false; });
        window.Size = new Size(620, 660); window.SetFlow(0); window.Show(); window.SampleMeters();
    }
    static void StressRound(AppController app, MainWindow window, int i, List<WeakReference> pins)
    {
        app.Change(delegate(Settings s)
        {
            s.Theme = i % 2 == 0 ? "Dark" : "Light"; s.Meters = i % 3 != 0; s.PinnedFlow = i % 3;
            s.For(app.Devices.First(d => d.Id == "screen")).Hidden = i % 4 == 0;
        });
        window.SetFlow(i % 2); window.Size = new Size(460 + (i % 4) * 70, 560 + (i % 3) * 35);
        SourceCard card = Descendants(window).OfType<SourceCard>().FirstOrDefault(c => c.Visible);
        if (card != null && i % 5 == 0)
        {
            string id = card.Device.Id; card.BeginRename(); Descendants(card).OfType<TextBox>().First().Text = "Alias " + i;
            card.FinishRename(true);
            Assert(app.Settings.For(card.Device).Alias == "Alias " + i && card.Device.Id == id, "Stress inline rename keeps source identity, iteration " + i);
        }
        window.Pin(); PinnedWindow pinned = window.PinnedWindow; pinned.Size = pinned.MinimumSize; window.SampleMeters();
        pinned.Size = new Size(i % 3 == 2 ? 510 : 270, 220); window.SampleMeters(); Application.DoEvents();
        window.OpenFull(); pins.Add(new WeakReference(pinned));
        using (SettingsWindow settings = new SettingsWindow(app))
        {
            settings.StartPosition = FormStartPosition.Manual; settings.Location = new Point(-20000, -20000); settings.Show();
            Descendants(settings).OfType<Button>().First(b => b.Text == "Devices").PerformClick();
            Descendants(settings).OfType<Button>().First(b => b.Text == "General").PerformClick();
            settings.Close();
        }
        using (ShortcutDialog capture = new ShortcutDialog(app, "cycle0", 0))
        { capture.StartPosition = FormStartPosition.Manual; capture.Location = new Point(-20000, -20000); capture.Show(); capture.Close(); }
        window.Hide(); window.Show(); Application.DoEvents();
    }
}
