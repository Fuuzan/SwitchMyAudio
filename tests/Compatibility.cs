using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using SwitchMyAudio;

internal static partial class TestRunner
{
    static void Compatibility(string folder)
    {
        Assert(Marshal.SizeOf(typeof(Win32.PaintInfo)) == (IntPtr.Size == 8 ? 72 : 64) &&
            Marshal.SizeOf(typeof(Win32.ComboInfo)) == (IntPtr.Size == 8 ? 64 : 52) &&
            Marshal.OffsetOf(typeof(Win32.ComboInfo), "Combo").ToInt32() == 40 &&
            Marshal.SizeOf(typeof(PropertyKey)) == 20, "Native paint, dropdown and device-property layouts match this process architecture");
        string oversized = Path.Combine(folder, "oversized-settings.xml");
        using (StreamWriter writer = new StreamWriter(oversized))
        { writer.Write("<Settings><Unused>"); writer.Write(new string('x', 9 * 1024 * 1024)); writer.Write("</Unused></Settings>"); }
        bool denied = false; try { SettingsStore.Read(oversized); } catch (InvalidOperationException) { denied = true; } catch (System.Xml.XmlException) { denied = true; }
        Assert(denied, "Oversized damaged settings are rejected by the bounded XML reader");
        File.Delete(oversized);
        Settings unicode = new Settings();
        Device named = new Device { Id = "synthetic-unicode", Name = "\u0633\u0645\u0627\u0639\u0627\u062a / \u30d8\u30c3\u30c9\u30db\u30f3 / \ud83c\udfa7", Online = true };
        unicode.For(named).Alias = named.Name;
        SettingsStore store = new SettingsStore(Path.Combine(folder, "unicode-settings.xml")); store.Save(unicode);
        Assert(store.Load().Display(named) == named.Name, "Non-English and emoji aliases round-trip without changing endpoint identity");
        FakeAudio fake = new FakeAudio(); fake.Connected.Clear();
        using (AppController app = new AppController(Path.Combine(folder, "empty-audio.xml"), true, fake.Factory))
        using (MainWindow window = new MainWindow(app))
        {
            app.Change(s => s.Meters = true);
            window.Location = new Point(-20000, -20000); window.Show(); Application.DoEvents();
            Assert(app.Devices.Count == 0 && !window.MeterRunning && fake.MeterIds.Count == 0,
                "No audio hardware is a usable empty state with no meter animation or sampling");
            window.Pin(); Application.DoEvents();
            Assert(!window.MeterRunning && window.PinnedWindow.Visible, "Empty pinned view remains usable without waking the meter timer");
            fake.Connected.Add(new Device { Id = "late-output", Name = "USB output", Online = true, Flow = 0 }); fake.Signal();
            Assert(Until(() => app.Devices.Any(d => d.Id == "late-output") && window.MeterRunning, 700),
                "A device connected after an empty start restores meters without restarting the app");
            app.Change(s => s.For(app.Devices[0]).Hidden = true);
            Assert(!window.MeterRunning, "Hiding the last visible device stops the meter timer");
        }
        fake = new FakeAudio();
        for (int i = 0; i < 60; i++) fake.Connected.Add(new Device { Id = "many-" + i, Name = "USB source " + i, Flow = i % 2, Online = true });
        using (AppController app = new AppController(Path.Combine(folder, "many-audio.xml"), true, fake.Factory))
        using (MainWindow window = new MainWindow(app))
        {
            app.Change(s => s.Meters = true);
            window.Location = new Point(-20000, -20000); window.Size = window.MinimumSize; window.Show(); Application.DoEvents(); window.SampleMeters();
            Assert(fake.MeterIds.Count > 0 && fake.MeterIds.Count < 10, "Large device lists prepare only viewport-visible meters");
            app.Change(s => s.PinnedFlow = 2); window.Pin(); window.PinnedWindow.Size = window.PinnedWindow.MinimumSize; Application.DoEvents(); window.SampleMeters();
            Assert(fake.MeterIds.Count > 0 && fake.MeterIds.Count < 10, "Small both-flow pinned view limits meter work to its two visible viewports");
        }
    }
}
