using System;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using SwitchMyAudio;

internal static partial class TestRunner
{
    static void SettingsPaint(string folder)
    {
        using (AppController app = new AppController(System.IO.Path.Combine(folder, "settings-paint.xml"), true))
        using (MainWindow main = new MainWindow(app))
        {
            app.Settings.Meters = false; main.Show();
            using (SettingsWindow settings = new SettingsWindow(app))
            {
                bool paintedWhilePrepared = false; int paints = 0;
                settings.Content.Paint += delegate
                {
                    paints++; int cloaked;
                    if (DwmGetWindowAttribute(settings.Handle, 14, out cloaked, 4) >= 0 && (cloaked & 1) != 0) paintedWhilePrepared = true;
                };
                settings.Show(main); Pump(20);
                int state; int hr = DwmGetWindowAttribute(settings.Handle, 14, out state, 4);
                Assert(paints > 0 && settings.BackColor == Palette.Current.Background && (hr < 0 || (state & 1) == 0),
                    "Settings is themed and painted on opening and never left hidden by the compositor");
                Results.Add("SETTINGS prepared_paint_observed=" + paintedWhilePrepared + " compositor_available=" + (hr >= 0));
                settings.Hide(); settings.Show(main); Pump(10);
                Assert(settings.Visible && settings.BackColor == Palette.Current.Background, "Reopening the same Settings window retains its themed surface");

                ThemeCombo combo = Descendants(settings).OfType<ThemeCombo>().First();
                foreach (string theme in new[] { "Dark", "Light", "Dark" })
                {
                    app.Settings.Theme = theme; app.Notify(); settings.ShowPage(false); Pump(10);
                    using (Bitmap bmp = new Bitmap(combo.Width, combo.Height))
                    using (Graphics g = Graphics.FromImage(bmp))
                    {
                        g.Clear(Color.Magenta); IntPtr dc = g.GetHdc();
                        try { Win32.SendMessage(combo.Handle, 0x14, dc, IntPtr.Zero); } finally { g.ReleaseHdc(dc); }
                        Assert(bmp.GetPixel(5, 5).ToArgb() == Palette.Current.Field.ToArgb(), "Dropdown erase uses the theme field color in " + theme);
                        dc = g.GetHdc();
                        try { Win32.SendMessage(combo.Handle, 0x318, dc, new IntPtr(4)); } finally { g.ReleaseHdc(dc); }
                        Assert(bmp.GetPixel(combo.Width - 20, 4).ToArgb() == Palette.Current.Field.ToArgb(), "Dropdown prints a complete themed frame in " + theme);
                    }
                }
                Control face = combo.Controls.Cast<Control>().First();
                Assert(face.Bounds == combo.ClientRectangle && !face.TabStop, "Buffered dropdown face covers native hover painting without taking keyboard focus");
                // A popup belongs to the active window. Earlier checks also show
                // other forms, so explicitly restore that native precondition.
                settings.Activate(); combo.Focus(); Pump(10);
                Results.Add("DROPDOWN before_click focused=" + combo.ContainsFocus + " active=" + (Form.ActiveForm == settings));
                Win32.SendMessage(face.Handle, 0x201, new IntPtr(1), new IntPtr((8 << 16) | 8));
                Win32.SendMessage(face.Handle, 0x202, IntPtr.Zero, new IntPtr((8 << 16) | 8));
                Results.Add("DROPDOWN after_click open=" + combo.DroppedDown + " focused=" + combo.ContainsFocus);
                Assert(combo.DroppedDown && combo.ContainsFocus, "Clicking the buffered dropdown face opens the native keyboard-accessible list");
                Win32.ComboInfo info = new Win32.ComboInfo { Size = System.Runtime.InteropServices.Marshal.SizeOf(typeof(Win32.ComboInfo)) };
                Assert(Win32.GetComboBoxInfo(combo.Handle, ref info) && info.List != IntPtr.Zero, "Themed dropdown retains the real native popup and accessibility semantics");
                using (Bitmap bmp = new Bitmap(20, 20))
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.Clear(Color.White); IntPtr dc = g.GetHdc();
                    try { Win32.SendMessage(info.List, 0x14, dc, IntPtr.Zero); } finally { g.ReleaseHdc(dc); }
                    Assert(bmp.GetPixel(5, 5).ToArgb() == Palette.Current.Field.ToArgb(), "Open dropdown popup erases with dark field color rather than white");
                }
                combo.DroppedDown = false; combo.SelectedIndex = 2;
                Assert(app.Settings.PinnedFlow == 2 && combo.AccessibilityObject.Role == AccessibleRole.ComboBox, "Themed selection still commits the pinned source mode through the native combo");
                Win32.SendMessage(combo.Handle, 0x100, new IntPtr((int)Keys.F4), new IntPtr(1)); Pump(10);
                Assert(combo.DroppedDown, "Native F4 keyboard navigation opens the themed popup");
                Win32.SendMessage(combo.Handle, 0x100, new IntPtr((int)Keys.Escape), new IntPtr(1)); Pump(10);
                Assert(!combo.DroppedDown, "Escape closes the dropdown without closing Settings");
                combo.SelectedIndex = 0;
                Win32.SendMessage(combo.Handle, 0x100, new IntPtr((int)Keys.Down), new IntPtr(1));
                Assert(combo.SelectedIndex == 1 && app.Settings.PinnedFlow == 1, "Native arrow-key selection still changes the pinned source mode");

                settings.ShowPage(true); settings.Size = settings.MinimumSize; Pump(10);
                var order = app.Settings.Ordered(app.Devices, 0, true);
                string firstId = order[0].Id;
                Control row = Descendants(settings).OfType<DragGrip>().First(g => g.AccessibleName == "Reorder " + app.Settings.Display(order[0])).Parent;
                ActionButton up = row.Controls.OfType<ActionButton>().First(b => (string)b.Tag == "up");
                ActionButton down = row.Controls.OfType<ActionButton>().First(b => (string)b.Tag == "down");
                int hides = 0; row.VisibleChanged += delegate { if (!row.Visible) hides++; }; IntPtr handle = row.Handle;
                Assert(!up.Enabled && down.Enabled, "First source disables its up arrow and enables its down arrow");
                down.PerformClick();
                Assert(app.Settings.Ordered(app.Devices, 0, true)[1].Id == firstId && up.Enabled && down.Enabled,
                    "Down arrow reorders one position and updates arrow availability");
                up.PerformClick();
                Assert(app.Settings.Ordered(app.Devices, 0, true)[0].Id == firstId && !up.Enabled && row.Handle == handle && hides == 0,
                    "Up arrow restores order while retaining the same visible row and handle");
                Label name = row.Controls.OfType<Label>().First(l => (string)l.Tag == "deviceName");
                Assert(name.Right < up.Left && up.Right < down.Left && down.Right < row.Controls.OfType<Toggle>().First().Left,
                    "Source name, reorder arrows and visibility switch do not overlap at minimum width");
                settings.MoveDevice(firstId, 0, order.Count - 1);
                Assert(up.Enabled && !down.Enabled, "Drag reordering also refreshes both arrow boundary states");
            }
            using (SettingsWindow modal = new SettingsWindow(app))
            using (Timer close = new Timer { Interval = 40 })
            {
                bool painted = false; modal.Content.Paint += delegate { painted = true; };
                close.Tick += delegate { close.Stop(); modal.Close(); }; close.Start(); modal.ShowDialog(main);
                Assert(painted, "The actual modal Settings path paints before closing");
            }
            main.Quit();
        }
    }
}
