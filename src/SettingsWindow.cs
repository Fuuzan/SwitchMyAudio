using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace SwitchMyAudio
{
    internal sealed class SettingsWindow : ThemedForm
    {
        readonly AppController app;
        readonly Panel navigation = new BufferedPanel(), content = new VerticalPanel();
        readonly ActionButton general = new ActionButton { Text = "General" }, devices = new ActionButton { Text = "Devices" };
        readonly ToolTip tips = new ToolTip();
        bool devicePage, rebuilding, syncing;
        readonly List<Control> owned = new List<Control>();
        readonly Dictionary<bool, List<Control>> pages = new Dictionary<bool, List<Control>>();
        readonly Dictionary<string, Panel> deviceRows = new Dictionary<string, Panel>();
        readonly string dragFormat = "SwitchMyAudio.DeviceOrder." + Guid.NewGuid().ToString("N");
        readonly Timer dragScroll = new Timer { Interval = 50 };
        string draggedId, markerId;
        int markerSide, scrollDirection;
        Point pointerDown;
        bool pageReady, renderedPage, revealing;
        internal Panel Content { get { return content; } }
        internal void ShowPage(bool value) { devicePage = value; Build(); }
        internal SettingsWindow(AppController controller)
        {
            app = controller; Text = "Switch My Audio Settings"; Font = new Font("Segoe UI", 10);
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
            AutoScaleMode = AutoScaleMode.Dpi; ClientSize = new Size(500, 500); MinimumSize = new Size(440, 450);
            StartPosition = FormStartPosition.CenterParent; ShowInTaskbar = false;
            navigation.Dock = DockStyle.Top; navigation.Height = 62;
            general.SetBounds(20, 14, 120, 36); devices.SetBounds(150, 14, 120, 36);
            navigation.Controls.AddRange(new Control[] { general, devices });
            content.Dock = DockStyle.Fill; content.AutoScroll = true;
            content.HandleCreated += delegate { Palette.Scrollbar(content); };
            Controls.Add(content); Controls.Add(navigation);
            general.Click += delegate { devicePage = false; Build(); };
            devices.Click += delegate { devicePage = true; Build(); };
            content.Resize += delegate { if (!rebuilding) Arrange(); };
            content.AllowDrop = true; BindDrop(content);
            // AutoScroll already moves the child windows. Repositioning them
            // again during a scroll can compete with the native scroll paint.
            dragScroll.Tick += delegate {
                if (draggedId == null || scrollDirection == 0) { dragScroll.Stop(); return; }
                content.AutoScrollPosition = new Point(0, Math.Max(0, -content.AutoScrollPosition.Y + scrollDirection * S(18)));
                UpdateDropTarget(content.PointToClient(Cursor.Position));
            };
            Build();
        }
        int S(int x) { return (int)Math.Round(x * DeviceDpi / 96.0); }
        protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); Palette.Window(this); }
        protected override void SetVisibleCore(bool value)
        {
            if (!value || Visible || revealing) { base.SetVisibleCore(value); return; }
            revealing = true;
            bool cloaked = false;
            try
            {
                Palette.Window(this); cloaked = Cloak(true);
                base.SetVisibleCore(true);
                if (Visible && !IsDisposed) PaintBeforeReveal();
            }
            finally
            {
                if (cloaked && !IsDisposed) Cloak(false);
                revealing = false;
            }
        }
        void Build()
        {
            using (new PaintBatch(content))
            {
                rebuilding = true;
                List<Control> cached;
                bool pageChanged = pageReady && renderedPage != devicePage;
                if (!pages.TryGetValue(devicePage, out cached))
                {
                    owned.Clear();
                    if (devicePage) BuildDevices(); else BuildGeneral();
                    cached = new List<Control>(owned); pages[devicePage] = cached;
                }
                owned.Clear(); owned.AddRange(cached);
                foreach (Control row in content.Controls) { bool show = owned.Contains(row); if (row.Visible != show) row.Visible = show; }
                general.Selected = !devicePage; devices.Selected = devicePage;
                general.Invalidate(); devices.Invalidate();
                if (pageChanged) content.AutoScrollPosition = Point.Empty;
                SyncValues(); ApplyTheme(); rebuilding = false; Arrange();
                pageReady = true; renderedPage = devicePage;
            }
        }
        void ApplyTheme()
        {
            Palette.Set(app.Settings.Theme);
            Palette.Window(this); navigation.BackColor = content.BackColor = Palette.Current.Background; Palette.Scrollbar(content);
            foreach (Control row in content.Controls)
            {
                row.BackColor = Palette.Current.Background;
                foreach (Control child in row.Controls)
                {
                    child.BackColor = child is ComboBox ? Palette.Current.Field : Palette.Current.Background;
                    child.ForeColor = child is Label && child.Top >= S(30) ? Palette.Current.Muted : Palette.Current.Text;
                    ActionButton button = child as ActionButton;
                    string tag = child.Tag as string;
                    if (button != null && tag != null && tag.StartsWith("theme")) button.Selected = button.Text == app.Settings.Theme;
                    child.Invalidate();
                }
                row.Invalidate();
            }
        }
        void SyncValues()
        {
            syncing = true;
            try
            {
                foreach (Control row in content.Controls)
                    foreach (Control child in row.Controls)
                    {
                        Toggle toggle = child as Toggle;
                        if (toggle != null)
                        {
                            if (toggle.AccessibleName == "Show level meters") toggle.Checked = app.Settings.Meters;
                            else if (toggle.AccessibleName == "Auto-switch headphones") toggle.Checked = app.Settings.AutoHeadphones;
                            else if (toggle.AccessibleName == "Start with Windows")
                            { if (app.Demo) toggle.Checked = app.DemoStartup; else try { toggle.Checked = Startup.Enabled(Application.ExecutablePath); } catch { } }
                            else if ((string)toggle.Tag == "show")
                            {
                                string id = deviceRows.FirstOrDefault(pair => pair.Value == row).Key;
                                Device d = app.Devices.FirstOrDefault(x => x.Id == id);
                                if (d != null) toggle.Checked = !app.Settings.For(d).Hidden;
                            }
                        }
                        ComboBox combo = child as ComboBox;
                        if (combo != null && combo.AccessibleName == "Pinned sources") combo.SelectedIndex = app.Settings.PinnedFlow;
                    }
            }
            finally { syncing = false; }
            UpdateOrderButtons();
        }
        void UpdateOrderButtons()
        {
            for (int flow = 0; flow < 2; flow++)
            {
                List<Device> order = app.Settings.Ordered(app.Devices, flow, true);
                for (int i = 0; i < order.Count; i++)
                {
                    Panel row; if (!deviceRows.TryGetValue(order[i].Id, out row)) continue;
                    foreach (ActionButton button in row.Controls.OfType<ActionButton>())
                    {
                        if ((string)button.Tag == "up") button.Enabled = i > 0;
                        else if ((string)button.Tag == "down") button.Enabled = i < order.Count - 1;
                    }
                }
            }
        }
        void Nudge(string id, int flow, int delta)
        {
            List<Device> order = app.Settings.Ordered(app.Devices, flow, true);
            int index = order.FindIndex(d => d.Id == id);
            if (index >= 0) MoveDevice(id, flow, index + delta);
        }
        void RebuildDevices()
        {
            List<Control> page;
            if (pages.TryGetValue(true, out page))
            {
                using (new PaintBatch(content))
                {
                    foreach (Control row in page) { content.Controls.Remove(row); row.Dispose(); }
                    pages.Remove(true); deviceRows.Clear(); Build();
                }
            }
            else Build();
        }

        Panel Row(string name, string explanation, int height)
        {
            Panel row = new BufferedPanel { Height = S(height), BackColor = Palette.Current.Background, Tag = "row" };
            if (!string.IsNullOrEmpty(name))
            {
                Label label = new Label { Text = name, ForeColor = Palette.Current.Text, Location = new Point(0, S(10)), Size = new Size(S(290), S(24)), AutoEllipsis = true };
                row.Controls.Add(label);
                if (!string.IsNullOrEmpty(explanation)) { tips.SetToolTip(label, explanation); label.AccessibleDescription = explanation; }
            }
            if (!string.IsNullOrEmpty(explanation))
                row.Controls.Add(new Label { Text = explanation, ForeColor = Palette.Current.Muted, Font = new Font("Segoe UI", 8.7f),
                    Location = new Point(0, S(36)), Size = new Size(S(380), S(Math.Min(34, height - 38))), AutoEllipsis = true });
            if (!string.IsNullOrEmpty(name))
                row.Paint += delegate(object s, PaintEventArgs e) { using (Pen p = new Pen(Palette.Current.Border)) e.Graphics.DrawLine(p, 0, row.Height - 1, row.Width, row.Height - 1); };
            owned.Add(row); content.Controls.Add(row); return row;
        }
        void AddToggle(string label, string help, bool value, Action<bool> setter)
        {
            Panel row = Row(label, help, help == null ? 55 : 72);
            Toggle toggle = new Toggle { Checked = value, AccessibleName = label, Tag = "right" };
            row.Controls.Add(toggle); tips.SetToolTip(toggle, help ?? label); toggle.AccessibleDescription = help ?? label;
            toggle.CheckedChanged += delegate { if (!syncing) setter(toggle.Checked); };
        }
        void BuildGeneral()
        {
            bool startup = app.Demo && app.DemoStartup;
            if (!app.Demo) try { startup = Startup.Enabled(Application.ExecutablePath); } catch { }
            AddToggle("Start with Windows", "Open quietly when you sign in.", startup, delegate(bool value)
            {
                if (app.Demo) { app.DemoStartup = value; return; }
                try { Startup.Set(value, Application.ExecutablePath); }
                catch (Exception ex) { MessageBox.Show(this, "Startup could not be changed: " + ex.Message, "Switch My Audio", MessageBoxButtons.OK, MessageBoxIcon.Warning); Build(); }
            });
            Panel theme = Row("Theme", null, 96);
            theme.Controls[0].Width = S(100);
            string[] modes = { "Light", "Dark", "System" };
            for (int i = 0; i < 3; i++)
            {
                string captured = modes[i];
                ActionButton button = new ActionButton { Text = captured, Selected = app.Settings.Theme == captured, Tag = "theme" + i };
                tips.SetToolTip(button, i == 2 ? "Follow Windows' colors." : "Use " + captured.ToLowerInvariant() + " colors.");
                button.Click += delegate { if (app.Change(s => s.Theme = captured)) ApplyTheme(); };
                theme.Controls.Add(button);
            }
            Panel pin = Row("Pinned sources", null, 62);
            ThemeCombo pinned = new ThemeCombo { Tag = "combo", AccessibleName = "Pinned sources" };
            pinned.Items.AddRange(new object[] { "Playback", "Microphones", "Both" }); pinned.SelectedIndex = app.Settings.PinnedFlow;
            pinned.SetHint(tips, "Choose what appears in the pinned window."); pinned.AccessibleDescription = "Choose what appears in the pinned window.";
            pinned.SelectedIndexChanged += delegate { if (!syncing && !app.Change(s => s.PinnedFlow = pinned.SelectedIndex)) Build(); };
            pin.Controls.Add(pinned);
            AddToggle("Show level meters", "Shows live sound activity. Stops when hidden.", app.Settings.Meters,
                value => { if (!app.Change(s => s.Meters = value)) Build(); });
            AddToggle("Auto-switch headphones", "Prefer headphones when connected. Windows may also switch automatically.", app.Settings.AutoHeadphones,
                value => { if (!app.Change(s => s.AutoHeadphones = value)) Build(); });
        }
        void BuildDevices()
        {
            Panel info = Row("Show and order sources", "Drag the grip or use the arrows to reorder.", 78);
            info.Controls[0].Font = new Font("Segoe UI Semibold", 12);
            for (int flow = 0; flow < 2; flow++)
            {
                int capturedFlow = flow;
                Panel heading = Row(flow == 0 ? "Playback" : "Microphones", null, 45);
                heading.Controls[0].Font = new Font("Segoe UI Semibold", 11);
                List<Device> ordered = app.Settings.Ordered(app.Devices, flow, true);
                foreach (Device device in ordered)
                {
                    Device d = device; Binding binding = app.Settings.For(d);
                    string display = app.Settings.Display(d);
                    string detail = !d.Online ? "Disconnected · preferences retained" : display == d.Name ? null : d.Name;
                    Panel row = Row(display, detail, detail == null ? 56 : 82);
                    row.Controls[0].Tag = "deviceName";
                    Toggle visible = new Toggle { Checked = !binding.Hidden, AccessibleName = "Show " + app.Settings.Display(d), Tag = "show" };
                    tips.SetToolTip(visible, "Show this source in the list. Its shortcut always works.");
                    visible.CheckedChanged += delegate { if (!syncing && !app.Change(s => s.For(d).Hidden = !visible.Checked)) Build(); };
                    DragGrip grip = new DragGrip { Tag = "grip", AccessibleName = "Reorder " + app.Settings.Display(d),
                        AccessibleDescription = "Drag up or down. Alt plus arrow keys also reorders." };
                    tips.SetToolTip(grip, "Drag to reorder. Alt + ↑ / ↓ also works.");
                    deviceRows[d.Id] = row;
                    grip.MouseDown += delegate(object sender, MouseEventArgs e) { if (e.Button == MouseButtons.Left) pointerDown = e.Location; };
                    grip.MouseMove += delegate(object sender, MouseEventArgs e)
                    {
                        if (e.Button != MouseButtons.Left || draggedId != null) return;
                        Size t = SystemInformation.DragSize;
                        if (new Rectangle(pointerDown.X - t.Width / 2, pointerDown.Y - t.Height / 2, t.Width, t.Height).Contains(e.Location)) return;
                        draggedId = d.Id;
                        try { grip.DoDragDrop(new DataObject(dragFormat, d.Id), DragDropEffects.Move); }
                        finally { draggedId = null; ClearDrag(); }
                    };
                    grip.KeyDown += delegate(object sender, KeyEventArgs e)
                    {
                        if (e.Alt && (e.KeyCode == Keys.Up || e.KeyCode == Keys.Down))
                        {
                            List<Device> order = app.Settings.Ordered(app.Devices, capturedFlow, true);
                            MoveDevice(d.Id, capturedFlow, order.FindIndex(x => x.Id == d.Id) + (e.KeyCode == Keys.Up ? -1 : 1));
                            e.Handled = e.SuppressKeyPress = true;
                        }
                    };
                    ActionButton up = new ActionButton { Glyph = "MoveUp", Tag = "up", AccessibleName = "Move " + app.Settings.Display(d) + " up" };
                    ActionButton down = new ActionButton { Glyph = "MoveDown", Tag = "down", AccessibleName = "Move " + app.Settings.Display(d) + " down" };
                    tips.SetToolTip(up, "Move this source up."); tips.SetToolTip(down, "Move this source down.");
                    up.Click += delegate { Nudge(d.Id, capturedFlow, -1); };
                    down.Click += delegate { Nudge(d.Id, capturedFlow, 1); };
                    row.Controls.AddRange(new Control[] { visible, grip, up, down });
                    row.Paint += delegate(object sender, PaintEventArgs e)
                    {
                        if (markerId == d.Id) using (Pen pen = new Pen(Palette.Current.Accent, S(3)))
                            e.Graphics.DrawLine(pen, 0, markerSide < 0 ? S(2) : row.Height - S(2), row.Width, markerSide < 0 ? S(2) : row.Height - S(2));
                    };
                    row.AllowDrop = true; BindDrop(row);
                    foreach (Control child in row.Controls) { child.AllowDrop = true; BindDrop(child); }
                }
            }
            Panel refresh = Row("", null, 64);
            ActionButton scan = new ActionButton { Text = "Refresh sources", Tag = "refresh", AccessibleName = "Refresh sources" };
            scan.Click += delegate { app.Refresh(false); RebuildDevices(); }; refresh.Controls.Add(scan);
        }
        void BindDrop(Control control)
        {
            control.DragEnter += HandleDragOver; control.DragOver += HandleDragOver; control.DragDrop += Drop;
            control.DragLeave += delegate { if (!content.ClientRectangle.Contains(content.PointToClient(Cursor.Position))) ClearDrag(); };
        }
        void HandleDragOver(object sender, DragEventArgs e)
        {
            e.Effect = DragDropEffects.None;
            if (draggedId == null || e.Data == null || !e.Data.GetDataPresent(dragFormat)) { ClearDrag(); return; }
            if (UpdateDropTarget(content.PointToClient(new Point(e.X, e.Y)))) e.Effect = DragDropEffects.Move;
        }
        internal bool UpdateDropTarget(Point point)
        {
            Device source = app.Devices.FirstOrDefault(d => d.Id == draggedId);
            string targetId = null;
            if (source != null)
                foreach (Device d in app.Settings.Ordered(app.Devices, source.Flow, true))
                {
                    Panel row;
                    if (deviceRows.TryGetValue(d.Id, out row) && point.Y >= row.Top && point.Y < row.Bottom) { targetId = d.Id; break; }
                }
            if (targetId == null && source != null)
            {
                List<Panel> rows = app.Settings.Ordered(app.Devices, source.Flow, true).Where(d => deviceRows.ContainsKey(d.Id)).Select(d => deviceRows[d.Id]).ToList();
                List<Control> page;
                if (rows.Count > 0 && pages.TryGetValue(true, out page))
                {
                    int first = page.IndexOf(rows[0]);
                    int top = first > 0 ? page[first - 1].Top : rows[0].Top;
                    int bottom = rows[rows.Count - 1].Bottom + (source.Flow == 1 ? S(12) : 0);
                    if (point.Y >= top && point.Y <= bottom)
                    {
                        Panel nearest = rows.OrderBy(row => Math.Abs(point.Y - row.Top - row.Height / 2)).First();
                        targetId = deviceRows.First(pair => pair.Value == nearest).Key;
                    }
                }
            }
            if (targetId == null) { ClearDrag(); return false; }
            Panel target = deviceRows[targetId];
            int side = point.Y < target.Top + target.Height / 2 ? -1 : 1;
            if (markerId != targetId || markerSide != side)
            {
                if (markerId != null && deviceRows.ContainsKey(markerId)) deviceRows[markerId].Invalidate();
                markerId = targetId; markerSide = side; target.Invalidate();
            }
            scrollDirection = point.Y < S(40) ? -1 : point.Y > content.ClientSize.Height - S(40) ? 1 : 0;
            dragScroll.Enabled = scrollDirection != 0;
            return true;
        }
        void Drop(object sender, DragEventArgs e)
        {
            HandleDragOver(sender, e);
            if (draggedId != null && markerId != null && e.Data != null && e.Data.GetDataPresent(dragFormat))
            {
                Device source = app.Devices.FirstOrDefault(d => d.Id == draggedId);
                if (source != null)
                {
                    List<Device> order = app.Settings.Ordered(app.Devices, source.Flow, true);
                    int old = order.FindIndex(d => d.Id == source.Id), slot = order.FindIndex(d => d.Id == markerId) + (markerSide > 0 ? 1 : 0);
                    MoveDevice(source.Id, source.Flow, slot > old ? slot - 1 : slot);
                }
            }
            ClearDrag();
        }
        void ClearDrag()
        {
            dragScroll.Stop(); scrollDirection = 0;
            if (markerId != null && deviceRows.ContainsKey(markerId)) deviceRows[markerId].Invalidate();
            markerId = null;
        }
        internal bool MoveDevice(string id, int flow, int index)
        {
            List<Device> order = app.Settings.Ordered(app.Devices, flow, true);
            int old = order.FindIndex(d => d.Id == id);
            if (old < 0 || index < 0 || index >= order.Count || old == index) return false;
            if (order.Any(d => !deviceRows.ContainsKey(d.Id))) return false;
            Device moved = order[old]; order.RemoveAt(old); order.Insert(index, moved);
            if (!app.Change(s => { for (int i = 0; i < order.Count; i++) s.For(order[i]).Order = i; })) return false;
            List<Control> page;
            if (pages.TryGetValue(true, out page))
            {
                using (new PaintBatch(content))
                {
                    List<int> positions = page.Select((row, i) => new { row, i })
                        .Where(x => order.Any(d => deviceRows.ContainsKey(d.Id) && deviceRows[d.Id] == x.row)).Select(x => x.i).ToList();
                    for (int i = 0; i < order.Count; i++) page[positions[i]] = deviceRows[order[i].Id];
                    if (devicePage) { owned.Clear(); owned.AddRange(page); Arrange(); }
                    UpdateOrderButtons();
                }
            }
            return true;
        }
        void Arrange()
        {
            if (rebuilding) return; rebuilding = true;
            content.AutoScrollMinSize = new Size(0, owned.Sum(c => c.Height) + S(20));
            int y = content.AutoScrollPosition.Y + S(8);
            int width = Math.Max(S(100), content.ClientSize.Width - S(40));
            foreach (Control row in owned)
            {
                row.SetBounds(S(20), y, width, row.Height); y += row.Height;
                foreach (Control c in row.Controls)
                {
                    string tag = c.Tag as string;
                    if (tag == "right") c.SetBounds(width - S(46), S(9), S(44), S(26));
                    else if (tag == "combo") c.SetBounds(Math.Max(S(160), width - S(180)), S(9), S(180), S(30));
                    else if (tag != null && tag.StartsWith("theme")) { int i = int.Parse(tag.Substring(5)); c.SetBounds(i * ((width - S(8)) / 3 + S(4)), S(45), (width - S(8)) / 3, S(36)); }
                    else if (tag == "export") c.SetBounds(0, S(66), S(115), S(34));
                    else if (tag == "import") c.SetBounds(S(125), S(66), S(115), S(34));
                    else if (tag == "show") c.SetBounds(width - S(46), S(8), S(44), S(26));
                    else if (tag == "grip") c.SetBounds(0, S(7), S(26), S(32));
                    else if (tag == "up") c.SetBounds(width - S(132), S(7), S(32), S(30));
                    else if (tag == "down") c.SetBounds(width - S(92), S(7), S(32), S(30));
                    else if (tag == "deviceName") c.SetBounds(S(34), S(10), Math.Max(S(40), width - S(174)), S(24));
                    else if (tag == "refresh") c.SetBounds(0, S(12), Math.Min(S(170), width), S(36));
                    else if (c is Label)
                    {
                        int reserve = row.Controls.OfType<ComboBox>().Any() ? 192 : 60;
                        c.Width = c.Top < S(20) ? width - S(reserve) : width;
                    }
                }
            }
            content.AutoScrollMinSize = new Size(0, owned.Sum(c => c.Height) + S(20));
            rebuilding = false;
        }
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        { if (keyData == Keys.Escape && draggedId == null) { Close(); return true; } return base.ProcessCmdKey(ref msg, keyData); }
        protected override void Dispose(bool disposing) { if (disposing) { tips.Dispose(); dragScroll.Dispose(); } base.Dispose(disposing); }
    }

    internal sealed class DragGrip : Control
    {
        internal DragGrip()
        { TabStop = true; Cursor = Cursors.SizeNS; AccessibleRole = AccessibleRole.Grip; SetStyle(ControlStyles.Selectable | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true); }
        protected override void OnMouseDown(MouseEventArgs e) { Focus(); base.OnMouseDown(e); }
        protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
        protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(Palette.Current.Background);
            int dot = Math.Max(2, DeviceDpi / 48), gap = dot + Math.Max(2, DeviceDpi / 48);
            using (Brush b = new SolidBrush(Palette.Current.Muted))
                for (int x = 0; x < 2; x++) for (int y = 0; y < 3; y++)
                    e.Graphics.FillEllipse(b, (Width - gap - dot) / 2 + x * gap, (Height - 2 * gap - dot) / 2 + y * gap, dot, dot);
            if (Focused && ShowFocusCues) ControlPaint.DrawFocusRectangle(e.Graphics, Rectangle.Inflate(ClientRectangle, -1, -1), Palette.Current.Text, Palette.Current.Background);
        }
    }
}

