using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using SwitchMyAudio;
using Binding = SwitchMyAudio.Binding;

internal static partial class TestRunner
{
    static readonly List<string> Results = new List<string>();
    static void Assert(bool condition, string name) { if (!condition) throw new Exception(name); Results.Add("PASS " + name); }
    [STAThread] static int Main(string[] args)
    {
        string folder = Path.GetFullPath(args[0]); Directory.CreateDirectory(folder);
        try
        {
            Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
            Results.Add("ENV Windows=" + Environment.OSVersion.Version + " process_bits=" + (IntPtr.Size * 8));
            Assert(AppDomain.CurrentDomain.SetupInformation.TargetFrameworkName == ".NETFramework,Version=v4.8", "Compiler and project builds declare the same .NET Framework 4.8 target");
            if (args.Contains("--ui")) { SmoothUi(folder); SettingsPaint(folder); SettingsScroll(folder); }
            else { FirstRunDefaults(folder); Core(folder); Regressions(folder); SmoothUi(folder); SettingsPaint(folder); SettingsScroll(folder); InstallChecks(folder); Compatibility(folder); }
            if (args.Contains("--native")) { Native(); NativeRecovery(); }
            if (args.Contains("--stress"))
            {
                string roundsArg = args.FirstOrDefault(a => a.StartsWith("--rounds="));
                int rounds = roundsArg == null ? 100 : int.Parse(roundsArg.Substring(9));
                if (rounds < 100 || rounds > 5000) throw new ArgumentException("Stress rounds must be between 100 and 5000.");
                Stress(folder, rounds);
            }
            if (args.Contains("--switch")) Integration();
            File.WriteAllLines(Path.Combine(folder, "test-results.txt"), Results); foreach (string line in Results) Console.WriteLine(line); return 0;
        }
        catch (Exception ex) { Results.Add("FAIL " + ex); File.WriteAllLines(Path.Combine(folder, "test-results.txt"), Results); Console.WriteLine(ex); return 1; }
    }
    static void Core(string folder)
    {
        List<Device> devices = AppController.DemoDevices(); Settings s = new Settings();
        foreach (Device d in devices) s.For(d).Cycle = true;
        s.For(devices[2]).Hidden = true;
        s.For(devices[1]).Order = 0; s.For(devices[0]).Order = 1;
        Assert(DeviceRules.Next(devices, s, 0).Id == devices[1].Id, "Cycle obeys explicit order, wraps and excludes hidden sources");
        s.For(devices[0]).Alias = "My cans";
        Assert(s.Display(devices[0]) == "My cans" && s.For(devices[0]).Id == "demo-headphones", "Rename preserves device identity");
        s.For(devices[1]).Cycle = false;
        bool refused = false; try { DeviceRules.Next(devices, s, 0); } catch (InvalidOperationException) { refused = true; }
        Assert(refused, "Cycle requires two connected visible selected sources");
        Assert(DeviceRules.Next(devices, s, 1).Flow == 1, "Playback and microphone cycles are independent");
        HashSet<string> baseline = new HashSet<string>(devices.Select(d => d.Id));
        Assert(DeviceRules.ArrivingHeadphones(devices, baseline) == null, "Existing headphones are not a new connection");
        baseline.Remove("demo-headphones");
        Assert(DeviceRules.ArrivingHeadphones(devices, baseline).Id == "demo-headphones", "Native headphone form factor drives new-connection selection");
        devices[0].FormFactor = 1; devices[0].Name = "Headphones by name only";
        Assert(DeviceRules.ArrivingHeadphones(devices, baseline) == null, "Misleading names and aliases do not classify speakers as headphones");
        devices[0].FormFactor = 5;
        Assert(DeviceRules.ArrivingHeadphones(devices, baseline) != null, "Headset form factor is supported");
        Assert(DeviceRules.HeadphoneCategories(new string[] { "Audio.Headphone" }) && DeviceRules.HeadphoneCategories(new string[] { "Communication.Headset" }) && DeviceRules.HeadphoneCategories(new string[] { "Communication.Headset.Bluetooth" }) &&
            !DeviceRules.HeadphoneCategories(new string[] { "Audio", "Audio.Speakers", "Bluetooth" }), "Canonical headphone metadata accepts headsets and excludes ordinary wireless speakers");
        List<Device> wireless = new List<Device> {
            new Device { Id = "bt-hfp", Flow = 0, Online = true, FormFactor = 5 },
            new Device { Id = "bt-stereo", Flow = 0, Online = true, FormFactor = 3 },
            new Device { Id = "usb-wireless", Flow = 0, Online = true, FormFactor = 1, HeadphoneCategory = true },
            new Device { Id = "bt-speaker", Flow = 0, Online = true, FormFactor = 1 },
            new Device { Id = "bt-mic", Flow = 1, Online = true, FormFactor = 5 } };
        HashSet<string> wirelessBefore = new HashSet<string>();
        Assert(DeviceRules.ArrivingHeadphones(wireless, wirelessBefore).Id == "bt-stereo", "Wireless arrivals prefer stereo headphones over a simultaneous hands-free endpoint");
        wirelessBefore.UnionWith(new string[] { "bt-stereo", "bt-hfp" });
        Assert(DeviceRules.ArrivingHeadphones(wireless, wirelessBefore).Id == "usb-wireless", "Headphone-category wireless receiver can switch even when its endpoint is called Speakers");
        wirelessBefore.Add("usb-wireless");
        Assert(DeviceRules.ArrivingHeadphones(wireless, wirelessBefore) == null, "Repeated wireless events do not switch again or select a speaker/microphone");
        string path = Path.Combine(folder, "test-settings.xml"); SettingsStore store = new SettingsStore(path);
        s.AutoHeadphones = true; s.Theme = "Light"; s.For(devices[0]).Key = 49; s.For(devices[0]).Mods = 3;
        store.Save(s); Settings copy = store.Load();
        Assert(copy.AutoHeadphones && copy.Theme == "Light" && copy.For(devices[0]).Alias == "My cans" && copy.For(devices[0]).Key == 49, "Aliases, shortcuts, ordering and preferences survive save/reload");
        copy.Theme = "Dark"; store.Save(copy);
        File.WriteAllText(path, "not valid XML"); Settings recovered = store.Load();
        Assert(recovered.Theme == "Light" && store.Notice != null && Directory.GetFiles(folder, "*.damaged-*").Length > 0, "Corrupt settings recover from backup and preserve damaged data");
        string legacy = Path.Combine(folder, "legacy.xml");
        File.WriteAllText(legacy, "<Settings><Devices><Binding><Id>legacy</Id><Name>USB Audio</Name><Flow>0</Flow><Key>49</Key><Mods>3</Mods><Cycle>true</Cycle></Binding></Devices><OutputKey>32</OutputKey><OutputMods>3</OutputMods></Settings>");
        Settings old = SettingsStore.Read(legacy);
        Assert(old.Devices.Count == 1 && old.OutputKey == 32 && !old.Meters && old.Theme == "System", "First-version XML migrates with shortcuts retained and new defaults");
        string previousRelease = Path.Combine(folder, "version2.xml");
        File.WriteAllText(previousRelease, "<Settings><Version>2</Version><Compact>false</Compact><Notifications>true</Notifications><PinnedFlow>2</PinnedFlow><Devices><Binding><Id>saved</Id><Name>USB Audio</Name><Alias>My source</Alias><Flow>0</Flow><Key>49</Key><Mods>3</Mods><Cycle>true</Cycle></Binding></Devices></Settings>");
        Settings migrated = SettingsStore.Read(previousRelease);
        Assert(migrated.Version == 5 && migrated.PinnedFlow == 2 && migrated.For(new Device { Id = "saved", Name = "USB Audio" }).Alias == "My source" && migrated.Devices[0].Key == 49,
            "Version 2 settings retain aliases and shortcuts while removed options are ignored");
        string dtd = Path.Combine(folder, "unsafe.xml"); File.WriteAllText(dtd, "<!DOCTYPE Settings [<!ENTITY x SYSTEM 'file:///C:/Windows/win.ini'>]><Settings>&x;</Settings>");
        bool denied = false; try { SettingsStore.Read(dtd); } catch { denied = true; }
        Assert(denied, "Settings reader prohibits external XML entities");
        Assert(KeyText.Label(49, 3) == "Ctrl + Alt + 1", "Shortcut labels use friendly digit names");
        using (AppController demo = new AppController(Path.Combine(folder, "never-written.xml"), true))
        {
            demo.FillDemoBindings();
            using (MainWindow window = new MainWindow(demo))
            {
                window.Show(); Application.DoEvents();
                Assert(window.MeterRunning, "Meters start in a visible window");
                window.Hide(); Application.DoEvents();
                Assert(!window.MeterRunning, "Meters stop when hidden to tray");
                window.Show(); window.WindowState = FormWindowState.Minimized; Application.DoEvents();
                Assert(!window.MeterRunning, "Meters stop when minimized");
                window.WindowState = FormWindowState.Normal; window.Pin(); Application.DoEvents();
                Assert(window.PinnedWindow.TopMost && window.PinnedWindow.FormBorderStyle == FormBorderStyle.None && window.MeterRunning, "Pinned mode is on top, source-only, and meters resume");
                Assert(window.PinnedWindow.UnpinButton.Visible && window.PinnedWindow.UnpinButton.Enabled &&
                    window.PinnedWindow.UnpinButton.Parent.GetChildAtPoint(window.PinnedWindow.UnpinButton.Location) == window.PinnedWindow.UnpinButton &&
                    window.PinnedWindow.UnpinBounds.Width > 40, "Pinned view has an unobscured visible Unpin button");
                int[] expectedHits = { 13, 12, 14, 10, 11, 16, 15, 17, 1 };
                System.Drawing.Point[] points = { new System.Drawing.Point(1, 1), new System.Drawing.Point(100, 1), new System.Drawing.Point(299, 1),
                    new System.Drawing.Point(1, 70), new System.Drawing.Point(299, 70), new System.Drawing.Point(1, 159),
                    new System.Drawing.Point(100, 159), new System.Drawing.Point(299, 159), new System.Drawing.Point(100, 70) };
                Assert(points.Select((p, i) => PinnedWindow.ResizeHit(p, new System.Drawing.Size(300, 160), 7) == expectedHits[i]).All(x => x), "Pinned resize hit targets cover every edge/corner without stealing source clicks");
                demo.Change(p => p.PinnedFlow = 2); Application.DoEvents();
                Assert(window.PinnedWindow.PlaybackBounds.Right < window.PinnedWindow.MicrophoneBounds.Left &&
                    window.PinnedWindow.PlaybackBounds.Top == window.PinnedWindow.MicrophoneBounds.Top, "Both mode places separately contained playback and microphones side by side");
                window.PinnedWindow.Size = new System.Drawing.Size(640, 280); window.PinnedWindow.SaveGeometry();
                demo.Notify();
                Assert(window.PinnedWindow.Width == 640 && window.PinnedWindow.Height == 280 && demo.Settings.PinBothWidth == 640,
                    "Pinned dimensions are saved and source refresh does not reset user sizing");
                int beforeMeterCount = window.PinnedWindow.VisibleMeters().Count;
                demo.Switch("demo-speakers");
                Assert(demo.Devices.Find(d => d.Id == "demo-speakers").Current && demo.Devices.Find(d => d.Id == "demo-mic").Current &&
                    beforeMeterCount > 0 && window.PinnedWindow.VisibleMeters().Count > 0, "Pinned switching preserves the other flow and meter controls");
                demo.Change(p => p.Meters = false);
                Assert(!window.MeterRunning, "Disabling meters stops the shared timer including pinned mode");
                window.PinnedWindow.UnpinButton.PerformClick(); Application.DoEvents();
                Assert(window.Visible && !demo.Settings.Pinned, "Clicking Unpin restores full window");
                window.Pin(); Application.DoEvents();
                Assert(window.PinnedWindow.Width == 640 && window.PinnedWindow.Height == 280, "Pinned sizing survives unpin and repin");
                PinnedWindow pin = window.PinnedWindow;
                pin.Size = pin.MinimumSize; Application.DoEvents();
                Assert(pin.Width == 336 && pin.Height == 116 && pin.ContentScale < .8 && pin.SourceFontSize >= 7.5 && pin.SourceFontSize < 9,
                    "Both mode shrinks to the new minimum and scales content with a readable font floor");
                Assert(pin.UnpinButton.Visible && pin.UnpinBounds.Width >= 24 && pin.ClientRectangle.Contains(pin.UnpinBounds), "Compact Unpin target remains accessible inside the smallest window");
                Assert(pin.Region != null && !pin.Region.IsVisible(0, 0) && pin.Region.IsVisible(pin.Width / 2, pin.Height / 2), "Pinned outer window has genuinely rounded corners");
                using (Form other = new Form())
                {
                    other.Show(); other.Activate(); Application.DoEvents();
                    Assert((Win32.GetWindowLong(pin.Handle, -16) & 0x00C00000) == 0 && pin.ClientSize == pin.Size,
                        "Losing focus preserves caption-free styling and a full-client pinned window");
                    pin.Activate(); Application.DoEvents();
                    Assert(pin.ClientSize == pin.Size, "Refocusing a pinned window does not introduce a native frame");
                }
                demo.Change(p => p.PinnedFlow = 0); pin.Size = pin.MinimumSize; pin.SaveGeometry(); Application.DoEvents();
                Assert(pin.Width == 176 && pin.Height == 116 && demo.Settings.PinWidth == 176, "Single-flow minimum is smaller and persists correctly");
                window.OpenFull();
                Assert(!Descendants(window).Any(c => c.Text.Contains("Close to tray")), "Removed tray-instruction footer is absent");
                using (SettingsWindow settings = new SettingsWindow(demo))
                {
                    settings.Show(); Application.DoEvents();
                    List<Control> controls = Descendants(settings).ToList();
                    string[] removed = { "Switch notifications", "Compact spacing", "Pin on top", "Preferences", "Export…", "Import…" };
                    Assert(!controls.Any(c => removed.Contains(c.Text) || removed.Contains(c.AccessibleName)), "Removed settings and import/export controls are absent");
                    List<Label> labels = controls.OfType<Label>().Where(l => !string.IsNullOrEmpty(l.Text)).ToList();
                    Assert(labels.First().Text == "Start with Windows", "Start with Windows is the first general setting");
                    settings.Close();
                }
                window.Quit();
            }
        }
    }
    static IEnumerable<Control> Descendants(Control parent)
    {
        foreach (Control child in parent.Controls)
        { yield return child; foreach (Control nested in Descendants(child)) yield return nested; }
    }
    static void Native()
    {
        using (AudioService audio = new AudioService(delegate { }))
        {
            List<Device> devices = audio.List(); Assert(devices.Count > 0, "Real Core Audio enumeration and notification subscription");
            foreach (Device d in devices) Results.Add("DEVICE " + (d.Flow == 0 ? "Playback " : "Microphone ") + d.Name + " form-factor=" + d.FormFactor + " current=" + d.Current);
            audio.PrepareMeters(devices.Select(d => d.Id));
            Assert(devices.All(d => audio.Peak(d.Id) >= 0 && audio.Peak(d.Id) <= 1), "Native input/output meter interfaces return normalized values");
            audio.PrepareMeters(new string[0]);
        }
        using (MessageWindow host = new MessageWindow()) using (HotkeyManager keys = new HotkeyManager(host))
        using (MessageWindow blocker = new MessageWindow())
        {
            Settings s = new Settings();
            List<uint> available = new List<uint>();
            for (uint key = 0x7c; key <= 0x87 && available.Count < 3; key++)
                if (Win32.RegisterHotKey(blocker.Handle, 600 + available.Count, 0x4007, key)) available.Add(key);
                else Results.Add("HOTKEY probe error=" + System.Runtime.InteropServices.Marshal.GetLastWin32Error());
            try
            {
                Assert(available.Count == 3, "Native test shortcuts acquired");
                Win32.UnregisterHotKey(blocker.Handle, 602);
                s.Devices.Add(new Binding { Id = "a", Name = "A", Key = available[0], Mods = 7 });
                s.Devices.Add(new Binding { Id = "b", Name = "B", Key = available[1], Mods = 7 });
                keys.Register(s, delegate { }, delegate { });
                Assert(keys.Problems.Count == 2, "All startup conflicts are reported, not only the last");
                keys.Suspend();
                Assert(keys.Validate(s, "a", 0, 0) == null, "Clearing a shortcut works despite unrelated conflicts");
                Assert(keys.Validate(s, "a", available[1], 7) != null, "Duplicate assignments are rejected");
                Assert(keys.Validate(s, "a", available[2], 7) == null, "Repairing one binding works while another is conflicted");
            }
            finally { for (int i = 0; i < available.Count; i++) Win32.UnregisterHotKey(blocker.Handle, 600 + i); }
        }
    }
    static void Integration()
    {
        using (AudioService audio = new AudioService(null))
        {
            for (int flow = 0; flow < 2; flow++)
            {
                int captured = flow;
                string[] before = Enumerable.Range(0, 3).Select(r => audio.Default(captured, r)).ToArray();
                Device target = audio.List().FirstOrDefault(d => d.Flow == flow && !d.Current);
                if (target == null) { Results.Add("SKIP actual switch: no alternate source for flow " + flow); continue; }
                IPolicy policy = (IPolicy)new PolicyClass();
                try
                {
                    audio.Switch(target);
                    Assert(Enumerable.Range(0, 3).All(r => audio.Default(captured, r) == target.Id), "Real switch across all roles, flow " + flow);
                }
                finally
                {
                    for (int r = 0; r < 3; r++) if (before[r] != null) policy.SetDefaultEndpoint(before[r], r);
                    AudioService.Release(policy);
                }
                Assert(Enumerable.Range(0, 3).All(r => audio.Default(captured, r) == before[r]), "Original audio defaults restored, flow " + flow);
            }
        }
    }
}
