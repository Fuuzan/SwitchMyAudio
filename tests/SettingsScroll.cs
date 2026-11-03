using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using SwitchMyAudio;

internal static partial class TestRunner
{
    static void SettingsScroll(string folder)
    {
        FakeAudio fake = new FakeAudio();
        for (int i = 0; i < 8; i++) fake.Connected.Add(new Device { Id = "scroll-source-" + i,
            Name = (i % 2 == 0 ? "USB output " : "USB microphone ") + i, Flow = i % 2, Online = true });
        using (AppController app = new AppController(Path.Combine(folder, "scroll-unused.xml"), true, fake.Factory))
        {
        app.Settings.For(app.Devices.First(d => d.Id == "speakers")).Alias = "Desk output";
        app.Settings.Devices.Add(new SwitchMyAudio.Binding { Id = "scroll-offline", Name = "Disconnected microphone", Flow = 1 }); app.Refresh(false);
        using (SettingsWindow settings = new SettingsWindow(app))
        {
            settings.Size = new Size(526, 655); settings.Show(); settings.ShowPage(true); Pump(20);
            Panel content = settings.Content;
            ActionButton refresh = Descendants(settings).OfType<ActionButton>().First(b => b.Text == "Refresh sources");
            Point center = new Point(refresh.Left + refresh.Width / 2, refresh.Top + refresh.Height / 2);
            Assert(refresh.Parent.GetChildAtPoint(center) == refresh &&
                !refresh.Parent.Controls.Cast<Control>().Any(c => c != refresh && c.Visible && c.Bounds.IntersectsWith(refresh.Bounds)),
                "Scrolled Refresh sources button is unobscured rather than cut into lines and rectangles by an empty label");
            Control disconnected = Descendants(settings).OfType<DragGrip>().First(g => g.AccessibleName == "Reorder Disconnected microphone").Parent;
            Assert(!Descendants(settings).OfType<ActionButton>().Any(b => b.Text.StartsWith("Map") || b.Text == "Forget") &&
                disconnected.Height == Descendants(settings).OfType<DragGrip>().First(g => g.AccessibleName == "Reorder Desk output").Parent.Height,
                "Disconnected rows retain their status without Map, Forget or empty action space");
            Control ordinary = Descendants(settings).OfType<DragGrip>().First(g => g.AccessibleName == "Reorder USB output 0").Parent;
            Control renamed = Descendants(settings).OfType<DragGrip>().First(g => g.AccessibleName == "Reorder Desk output").Parent;
            Assert(ordinary.Controls.OfType<Label>().Count() == 1 && renamed.Controls.OfType<Label>().Count() == 2 &&
                renamed.Controls.OfType<Label>().Any(l => l.Text == "Speakers"),
                "Device names appear once unless an alias needs its original name underneath");
            bool coherent = true, verticalOnly = true, footerVisible = true;
            foreach (string theme in new[] { "Dark", "Light" })
            foreach (Size size in new[] { new Size(526, 655), settings.MinimumSize })
            {
                app.Settings.Theme = theme; settings.Size = size; settings.ShowPage(true); Pump(10);
                Control[] rows = content.Controls.Cast<Control>().Where(c => c.Visible).ToArray();
                int[] logicalY = rows.Select(row => row.Top - content.AutoScrollPosition.Y).ToArray();
                foreach (int code in new[] { 3, 1, 2, 6, 7, 3, 2, 6 })
                {
                    Win32.SendMessage(content.Handle, 0x115, new IntPtr(code), IntPtr.Zero); Pump(5);
                    coherent &= rows.Select((row, i) => row.Top - content.AutoScrollPosition.Y == logicalY[i]).All(x => x);
                    verticalOnly &= (Win32.GetWindowLong(content.Handle, -16) & 0x00100000) == 0 && content.VerticalScroll.Visible;
                }
                Win32.SendMessage(content.Handle, 0x20A, new IntPtr(unchecked(-120 << 16)), IntPtr.Zero); Pump(5);
                coherent &= content.AutoScrollPosition.Y < 0 && rows.Select((row, i) => row.Top - content.AutoScrollPosition.Y == logicalY[i]).All(x => x);
                Win32.SendMessage(content.Handle, 0x115, new IntPtr(7), IntPtr.Zero); Pump(5);
                footerVisible &= content.ClientRectangle.Contains(new Rectangle(refresh.Parent.Left + refresh.Left, refresh.Parent.Top + refresh.Top, refresh.Width, refresh.Height));
                SaveSettingsImage(settings, Path.Combine(folder, "devices-scrolled-" + theme.ToLowerInvariant() + (size.Width == 526 ? "" : "-narrow") + ".png"));
            }
            Assert(coherent, "Native wheel, line, page and end scrolling preserve every row's document position across themes and sizes");
            Assert(verticalOnly && footerVisible, "Scrolling retains only the vertical bar and fully reveals the Refresh sources action at the bottom");
            int refreshes = app.RefreshCount; refresh.PerformClick();
            Assert(app.RefreshCount == refreshes + 1 && Descendants(settings).OfType<ActionButton>().Any(b => b.Text == "Refresh sources"),
                "The repaired Refresh sources action still refreshes and rebuilds its page");
            SwitchMyAudio.Binding retained = app.Settings.Devices.First(b => b.Id == "scroll-offline");
            retained.Alias = "Travel microphone"; retained.Key = 50; retained.Mods = 3; retained.Cycle = true;
            int retainedOrder = retained.Order;
            fake.Connected.Add(new Device { Id = retained.Id, Name = retained.Name, Flow = 1, Online = true }); app.Refresh(false);
            Assert(app.Devices.Any(d => d.Id == retained.Id && d.Online) && app.Settings.Devices.First(b => b.Id == retained.Id) == retained &&
                retained.Alias == "Travel microphone" && retained.Key == 50 && retained.Mods == 3 && retained.Cycle && retained.Order == retainedOrder,
                "A disconnected device reconnects with its alias, shortcut, cycle choice and order retained");
            settings.ShowPage(false);
            Toggle automatic = Descendants(settings).OfType<Toggle>().First(t => t.AccessibleName == "Auto-switch headphones");
            Assert(automatic.AccessibleDescription.Contains("Windows may also switch automatically") && automatic.AccessibleDescription.Length < 85,
                "Headphone help explains Windows' own switching without adding a long description");
            SaveSettingsImage(settings, Path.Combine(folder, "settings-headphone-help.png"));
        }
        }
    }
    static void SaveSettingsImage(Form settings, string path)
    {
        using (Bitmap bmp = new Bitmap(settings.Width, settings.Height))
        { settings.DrawToBitmap(bmp, new Rectangle(Point.Empty, settings.Size)); bmp.Save(path); }
    }
}
