using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using SwitchMyAudio;

internal static partial class TestRunner
{
    [DllImport("dwmapi.dll")] static extern int DwmGetWindowAttribute(IntPtr handle, int attribute, out int value, int size);
    static void SmoothUi(string folder)
    {
        using (AppController app = new AppController(System.IO.Path.Combine(folder, "smooth-ui.xml"), true))
        using (MainWindow main = new MainWindow(app))
        {
            main.Location = new Point(30, 30); main.Show(); Pump(30);
            app.Settings.Meters = false; app.Notify();
            SourceCard[] before = Descendants(main).OfType<SourceCard>().ToArray();
            int hidden = 0;
            foreach (SourceCard card in before) card.VisibleChanged += delegate(object s, EventArgs e) { if (!((Control)s).Visible) hidden++; };
            for (int i = 0; i < 5; i++) app.Switch("demo-speakers");
            Assert(hidden == 0, "Source selection never hides and reshow the existing device cards");
            using (SettingsWindow settings = new SettingsWindow(app))
            {
                settings.Location = new Point(50, 50); settings.Show(); settings.ShowPage(true); Pump(30);
                DragGrip grip = Descendants(settings).OfType<DragGrip>().First();
                Control row = grip.Parent;
                IntPtr handle = row.Handle; int rowHidden = 0;
                row.VisibleChanged += delegate { if (!row.Visible) rowHidden++; };
                app.Settings.For(app.Devices[0]).Alias = "A very long output name that should be truncated instead of horizontally scrolling";
                List<Device> order = app.Settings.Ordered(app.Devices, 0, true);
                string moved = order[0].Id;
                Assert(settings.MoveDevice(moved, 0, order.Count - 1) && app.Settings.Ordered(app.Devices, 0, true).Last().Id == moved,
                    "Drag-drop move commits the chosen final position and cycle order");
                Assert(!row.IsDisposed && row.Handle == handle && rowHidden == 0 && Descendants(settings).OfType<DragGrip>().Contains(grip),
                    "Reordering preserves device rows and native handles without hiding or recreating them");
                Assert(!settings.MoveDevice(moved, 1, 0) && !settings.MoveDevice(moved, 0, -1) && !settings.MoveDevice("missing", 0, 0),
                    "Reorder rejects a wrong flow, missing source and invalid position");
                string[] microphones = app.Settings.Ordered(app.Devices, 1, true).Select(d => d.Id).ToArray();
                Assert(microphones.SequenceEqual(new[] { "demo-mic", "demo-webcam" }), "Playback ordering leaves microphone order unchanged");
                settings.ShowPage(false); Control[] general = Descendants(settings).ToArray();
                settings.ShowPage(true); settings.ShowPage(false);
                Assert(general.All(c => !c.IsDisposed && Descendants(settings).Contains(c)), "Settings tabs reuse both pages instead of rebuilding controls");
                settings.ShowPage(true); settings.Size = settings.MinimumSize; Pump(20);
                Assert((Win32.GetWindowLong(settings.Content.Handle, -16) & 0x00100000) == 0 && settings.Content.AutoScrollPosition.X == 0 && settings.Content.VerticalScroll.Visible,
                    "Narrow Devices settings retains vertical scrolling and has no horizontal scrollbar");
                settings.Content.AutoScrollPosition = new Point(0, 100); Pump(20); int offset = settings.Content.AutoScrollPosition.Y;
                order = app.Settings.Ordered(app.Devices, 0, true); settings.MoveDevice(order[0].Id, 0, order.Count - 1); Pump(20);
                Assert(settings.Content.AutoScrollPosition.Y == offset, "Reordering retains the current vertical scroll position");
                settings.Content.AutoScrollPosition = Point.Empty; Pump(10);
                order = app.Settings.Ordered(app.Devices, 0, true);
                string dragId = order[0].Id, lastId = order.Last().Id;
                var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                typeof(SettingsWindow).GetField("draggedId", flags).SetValue(settings, dragId);
                string format = (string)typeof(SettingsWindow).GetField("dragFormat", flags).GetValue(settings);
                Control targetRow = Descendants(settings).OfType<DragGrip>().First(g => g.AccessibleName == "Reorder " + app.Settings.Display(app.Devices.First(d => d.Id == lastId))).Parent;
                Control startRow = Descendants(settings).OfType<DragGrip>().First(g => g.AccessibleName == "Reorder " + app.Settings.Display(app.Devices.First(d => d.Id == dragId))).Parent;
                Point hoverPoint = settings.Content.PointToScreen(new Point(startRow.Left + 40, startRow.Top + 2));
                Point dropPoint = settings.Content.PointToScreen(new Point(targetRow.Left + 40, targetRow.Bottom - 2));
                var args = new DragEventArgs(new DataObject(format, dragId), 0, hoverPoint.X, hoverPoint.Y, DragDropEffects.Move, DragDropEffects.None);
                typeof(SettingsWindow).GetMethod("HandleDragOver", flags).Invoke(settings, new object[] { targetRow, args });
                Assert(args.Effect == DragDropEffects.Move, "Drag-over handler accepts the current form's source data and computes an insertion marker");
                args = new DragEventArgs(new DataObject(format, dragId), 0, dropPoint.X, dropPoint.Y, DragDropEffects.Move, DragDropEffects.None);
                typeof(SettingsWindow).GetMethod("Drop", flags).Invoke(settings, new object[] { targetRow, args });
                Assert(app.Settings.Ordered(app.Devices, 0, true).Last().Id == dragId, "Drop handler recomputes the release position and applies source order");
                var foreign = new DragEventArgs(new DataObject(DataFormats.Text, "external"), 0, dropPoint.X, dropPoint.Y, DragDropEffects.Move, DragDropEffects.None);
                typeof(SettingsWindow).GetMethod("HandleDragOver", flags).Invoke(settings, new object[] { targetRow, foreign });
                Assert(foreign.Effect == DragDropEffects.None, "External drag data cannot reorder sources");
                typeof(SettingsWindow).GetField("draggedId", flags).SetValue(settings, null);
                settings.ShowPage(false); Pump(20);
                Assert((Win32.GetWindowLong(settings.Content.Handle, -16) & 0x00100000) == 0, "General settings never exposes horizontal scrolling");
            }
            main.Size = main.MinimumSize; main.SetFlow(0); Pump(20);
            Assert(Descendants(main).OfType<VerticalPanel>().All(p => (Win32.GetWindowLong(p.Handle, -16) & 0x00100000) == 0), "Main source list does not expose horizontal scrolling at minimum width");
            for (int i = 0; i < 5; i++)
            {
                main.Pin(); main.PinnedWindow.Size = main.PinnedWindow.MinimumSize; main.OpenFull();
                int cloaked; int hr = DwmGetWindowAttribute(main.Handle, 14, out cloaked, 4);
                Assert(main.Visible && !main.PinnedWindow.Visible && (hr < 0 || (cloaked & 1) == 0), "Restored main surface is visible and never left cloaked (cycle " + i + ")");
            }
            using (Bitmap bmp = new Bitmap(12, 12))
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.White); IntPtr dc = g.GetHdc();
                try { Win32.SendMessage(main.Handle, 0x14, dc, IntPtr.Zero); } finally { g.ReleaseHdc(dc); }
                Assert(bmp.GetPixel(2, 2).ToArgb() == Palette.Current.Background.ToArgb(), "Native erase messages paint the current theme rather than a white silhouette");
            }
            main.Quit();
        }
        string blocked = System.IO.Path.Combine(folder, "ui-save-blocked"); System.IO.File.WriteAllText(blocked, "not a folder");
        FakeAudio fake = new FakeAudio();
        using (AppController app = new AppController(System.IO.Path.Combine(blocked, "settings.xml"), false, fake.Factory))
        using (SettingsWindow settings = new SettingsWindow(app))
        {
            settings.Show(); Pump(10);
            Toggle meters = Descendants(settings).OfType<Toggle>().First(t => t.AccessibleName == "Show level meters");
            Toggle automatic = Descendants(settings).OfType<Toggle>().First(t => t.AccessibleName == "Auto-switch headphones");
            meters.Checked = false; automatic.Checked = true;
            ComboBox mode = Descendants(settings).OfType<ComboBox>().First(); mode.SelectedIndex = 2;
            Assert(meters.Checked == app.Settings.Meters && automatic.Checked == app.Settings.AutoHeadphones && mode.SelectedIndex == app.Settings.PinnedFlow,
                "Rejected saves restore cached meter, automatic-switch and pinned-mode controls without recursive writes");
            settings.ShowPage(true);
            Toggle visible = Descendants(settings).OfType<Toggle>().First(t => (string)t.Tag == "show"); visible.Checked = false;
            Assert(visible.Checked && !app.Settings.Devices.Any(b => b.Hidden), "Rejected visibility changes restore the same cached device toggle");
            string first = app.Settings.Ordered(app.Devices, 0, true).First().Id;
            Assert(!settings.MoveDevice(first, 0, 1) && app.Settings.Ordered(app.Devices, 0, true).First().Id == first,
                "Rejected reorder saves leave the original device order in place");
        }
    }
}
