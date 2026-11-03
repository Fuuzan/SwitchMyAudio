using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace SwitchMyAudio
{
    // A small borderless, resizable window. Every source remains a real button;
    // the unpin action has its own permanent target rather than relying on a tooltip.
    internal sealed class PinnedWindow : ThemedForm
    {
        readonly MainWindow owner;
        readonly ActionButton unpin = new ActionButton { Text = "Unpin", Glyph = "Unpin", AccessibleName = "Unpin and open full window" };
        readonly PinnedGroup playback, microphones;
        readonly Timer geometryTimer = new Timer { Interval = 600 };
        bool loading, layoutBusy, closing, disposedOnce;
        int mode = -1;
        float contentScale = 1;
        Font sourceFont, headingFont;
        internal int Mode { get { return mode; } }
        internal Rectangle PlaybackBounds { get { return playback.Bounds; } }
        internal Rectangle MicrophoneBounds { get { return microphones.Bounds; } }
        internal Rectangle UnpinBounds { get { return new Rectangle(PointToClient(unpin.PointToScreen(Point.Empty)), unpin.Size); } }
        internal ActionButton UnpinButton { get { return unpin; } }
        internal float ContentScale { get { return contentScale; } }
        internal float SourceFontSize { get { return sourceFont == null ? 9 : sourceFont.Size; } }

        internal PinnedWindow(MainWindow main)
        {
            loading = true; owner = main; Text = "Switch My Audio pinned";
            FormBorderStyle = FormBorderStyle.None; ShowInTaskbar = true; TopMost = true; Icon = owner.Icon;
            AutoScaleMode = AutoScaleMode.Dpi; StartPosition = FormStartPosition.Manual;
            Font = new Font("Segoe UI", 9); DoubleBuffered = true;
            playback = new PinnedGroup(this, 0); microphones = new PinnedGroup(this, 1);
            Controls.AddRange(new Control[] { playback, microphones, unpin });
            unpin.Font = new Font("Segoe UI", 9);
            unpin.Click += delegate { RequestUnpin(); };
            ContextMenuStrip menu = new ContextMenuStrip();
            menu.Items.Add("Unpin / open full window", null, delegate { RequestUnpin(); });
            menu.Items.Add("Settings", null, delegate { owner.App.Host.Dispatch(delegate { owner.OpenFull(); owner.OpenSettings(); }); });
            menu.Items.Add("Hide to tray", null, delegate { Hide(); owner.UpdateMeterState(); });
            menu.Items.Add("Quit", null, delegate { owner.App.Host.Dispatch(owner.Quit); });
            ContextMenuStrip = menu;
            ApplyMenu(this, menu);
            geometryTimer.Tick += delegate { geometryTimer.Stop(); SaveGeometry(); };
            Move += delegate { GeometryChanged(); };
            Resize += delegate { LayoutGroups(); RoundWindow(); GeometryChanged(); owner.UpdateMeterState(); };
            VisibleChanged += delegate { owner.UpdateMeterState(); };
            RefreshSources();
            Rectangle work = Screen.FromControl(owner).WorkingArea;
            Rectangle desired = new Rectangle(owner.App.Settings.PinX, owner.App.Settings.PinY, Width, Height);
            Location = desired.X != int.MinValue && Screen.AllScreens.Any(s => Rectangle.Intersect(s.WorkingArea, desired).Width > S(70) && Rectangle.Intersect(s.WorkingArea, desired).Height > S(50))
                ? desired.Location : new Point(work.Right - Width - S(24), work.Top + S(40));
            loading = false;
        }
        internal int S(int n) { return (int)Math.Round(n * DeviceDpi / 96.0); }
        internal int R(int n) { return Math.Max(1, (int)Math.Round(n * DeviceDpi / 96.0 * contentScale)); }
        protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); Palette.Window(this); RoundWindow(); }
        void RoundWindow()
        {
            if (ClientSize.Width < 2 || ClientSize.Height < 2) return;
            using (System.Drawing.Drawing2D.GraphicsPath path = Art.Round(new RectangleF(0, 0, Width, Height), Math.Max(S(11), R(16))))
            {
                Region old = Region; Region = new Region(path); if (old != null) old.Dispose();
            }
            Invalidate();
        }
        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams p = base.CreateParams;
                p.Style |= 0x00040000; // WS_THICKFRAME enables the native sizing loop.
                p.Style &= ~0x00C00000; // No title bar.
                return p;
            }
        }
        internal void RequestUnpin()
        {
            if (closing) return;
            // Defer disposal until the button/menu mouse event has completed.
            owner.App.Host.Dispatch(owner.OpenFull);
        }
        static void ApplyMenu(Control control, ContextMenuStrip menu)
        {
            control.ContextMenuStrip = menu;
            foreach (Control child in control.Controls) ApplyMenu(child, menu);
        }
        internal void RefreshSources()
        {
            if (IsDisposed) return;
            AppController app = owner.App; Palette p = Palette.Current;
            BackColor = p.Background;
            bool modeChanged = mode != app.Settings.PinnedFlow;
            if (modeChanged)
            {
                mode = app.Settings.PinnedFlow; bool wasLoading = loading; loading = true;
                MinimumSize = new Size(S(mode == 2 ? 336 : 176), S(116));
                Size preferred = mode == 2 ? new Size(app.Settings.PinBothWidth, app.Settings.PinBothHeight) : new Size(app.Settings.PinWidth, app.Settings.PinHeight);
                Rectangle area = Screen.FromControl(owner).WorkingArea;
                Size = new Size(Math.Max(MinimumSize.Width, Math.Min(area.Width, preferred.Width)), Math.Max(MinimumSize.Height, Math.Min(area.Height, preferred.Height)));
                loading = wasLoading;
            }
            List<Device> outputs = app.Settings.Ordered(app.Devices, 0, false).Where(d => d.Online).ToList();
            List<Device> inputs = app.Settings.Ordered(app.Devices, 1, false).Where(d => d.Online).ToList();
            playback.RefreshSources(outputs); microphones.RefreshSources(inputs);
            playback.Visible = mode != 1; microphones.Visible = mode != 0;
            unpin.BringToFront(); LayoutGroups(); owner.UpdateMeterState(); Invalidate(true);
        }
        internal Dictionary<string, MeterBar> VisibleMeters()
        {
            Dictionary<string, MeterBar> result = new Dictionary<string, MeterBar>();
            if (playback.Visible) playback.AddVisibleMeters(result);
            if (microphones.Visible) microphones.AddVisibleMeters(result);
            return result;
        }
        void LayoutGroups()
        {
            if (layoutBusy || playback == null || microphones == null) return; layoutBusy = true;
            float scale = (float)Math.Max(.68, Math.Min(1, Math.Min(ClientSize.Width / (double)S(mode == 2 ? 520 : 256), ClientSize.Height / (double)S(mode == 2 ? 240 : 208))));
            if (sourceFont == null || Math.Abs(scale - contentScale) > .015)
            {
                contentScale = scale;
                Font oldSource = sourceFont, oldHeading = headingFont;
                sourceFont = new Font("Segoe UI", Math.Max(7.5f, 9 * scale));
                headingFont = new Font("Segoe UI Semibold", Math.Max(7.5f, 9 * scale));
                unpin.Font = sourceFont; playback.SetFonts(sourceFont, headingFont); microphones.SetFonts(sourceFont, headingFont);
                if (oldSource != null) oldSource.Dispose(); if (oldHeading != null) oldHeading.Dispose();
            }
            int pad = R(8), gap = R(8), height = Math.Max(R(80), ClientSize.Height - pad * 2);
            if (mode == 2)
            {
                int width = (ClientSize.Width - pad * 2 - gap) / 2;
                playback.SetBounds(pad, pad, width, height);
                microphones.SetBounds(pad + width + gap, pad, ClientSize.Width - pad * 2 - gap - width, height);
            }
            else
            {
                (mode == 1 ? microphones : playback).SetBounds(pad, pad, ClientSize.Width - pad * 2, height);
            }
            PinnedGroup right = mode == 0 ? playback : microphones;
            if (unpin.Parent != right) right.Controls.Add(unpin);
            bool fullUnpin = right.Width >= S(230) && contentScale > .92f;
            unpin.Text = fullUnpin ? "Unpin" : ""; unpin.VisualScale = contentScale;
            int unpinWidth = fullUnpin ? R(79) : Math.Max(S(26), R(29));
            unpin.SetBounds(right.Width - R(8) - unpinWidth, R(6), unpinWidth, Math.Max(S(24), R(28)));
            unpin.BringToFront();
            playback.HeaderReserve = mode == 0 ? unpinWidth + R(6) : 0;
            microphones.HeaderReserve = mode == 1 || mode == 2 ? unpinWidth + R(6) : 0;
            playback.Arrange(); microphones.Arrange();
            layoutBusy = false;
        }
        void GeometryChanged()
        {
            if (loading || closing || !Visible) return;
            geometryTimer.Stop(); geometryTimer.Start();
        }
        internal void SaveGeometry()
        {
            if (loading || owner.App == null) return;
            Settings s = owner.App.Settings;
            if (s.PinX == Left && s.PinY == Top && (mode == 2 ? s.PinBothWidth == Width && s.PinBothHeight == Height : s.PinWidth == Width && s.PinHeight == Height)) return;
            s.PinX = Left; s.PinY = Top;
            if (mode == 2) { s.PinBothWidth = Width; s.PinBothHeight = Height; }
            else { s.PinWidth = Width; s.PinHeight = Height; }
            owner.App.SaveGeometry();
        }
        internal void SaveAndHide() { geometryTimer.Stop(); SaveGeometry(); Hide(); }
        internal static int ResizeHit(Point point, Size size, int border)
        {
            bool left = point.X < border, right = point.X >= size.Width - border;
            bool top = point.Y < border, bottom = point.Y >= size.Height - border;
            if (left && top) return 13; if (right && top) return 14;
            if (left && bottom) return 16; if (right && bottom) return 17;
            if (left) return 10; if (right) return 11; if (top) return 12; if (bottom) return 15;
            return 1;
        }
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == 0x83) { m.Result = IntPtr.Zero; return; } // Full-client frameless window, including recalculation on activation.
            if (m.Msg == 0x85) { m.Result = IntPtr.Zero; return; } // Never let Windows paint a nonclient frame.
            if (m.Msg == 0x86) { m.Result = new IntPtr(1); return; } // Accept focus changes without repainting a native caption.
            if (m.Msg == 0x84)
            {
                long packed = m.LParam.ToInt64();
                Point screen = new Point((short)(packed & 0xffff), (short)((packed >> 16) & 0xffff));
                int hit = ResizeHit(PointToClient(screen), ClientSize, S(7));
                m.Result = new IntPtr(hit); return;
            }
            base.WndProc(ref m);
        }
        internal void DragWindow()
        {
            Win32.ReleaseCapture(); Win32.SendMessage(Handle, 0xA1, new IntPtr(2), IntPtr.Zero);
        }
        protected override void OnMouseDown(MouseEventArgs e)
        { if (e.Button == MouseButtons.Left && ResizeHit(e.Location, ClientSize, S(7)) == 1) DragWindow(); base.OnMouseDown(e); }
        protected override void OnResizeEnd(EventArgs e) { base.OnResizeEnd(e); geometryTimer.Stop(); SaveGeometry(); }
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        { if (keyData == Keys.Escape) { RequestUnpin(); return true; } return base.ProcessCmdKey(ref msg, keyData); }
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!closing && e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; RequestUnpin(); return; }
            closing = true; geometryTimer.Stop(); SaveGeometry(); base.OnFormClosing(e);
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using (Pen p = new Pen(Palette.Current.Border))
            {
                Art.Box(e.Graphics, ClientRectangle, Palette.Current.Background, Palette.Current.Border, Math.Max(S(11), R(16)));
                for (int i = 0; i < 3; i++)
                { int d = S(3 + i * 3); e.Graphics.DrawLine(p, ClientSize.Width - d, ClientSize.Height - 2, ClientSize.Width - 2, ClientSize.Height - d); }
            }
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing && !disposedOnce) { disposedOnce = true; geometryTimer.Dispose(); if (ContextMenuStrip != null) ContextMenuStrip.Dispose(); if (sourceFont != null) sourceFont.Dispose(); if (headingFont != null) headingFont.Dispose(); }
            base.Dispose(disposing);
        }

        sealed class PinnedGroup : TilePanel
        {
            readonly PinnedWindow window;
            readonly int flow;
            readonly GroupHeading heading;
            readonly Panel viewport = new VerticalPanel();
            readonly Label empty = new Label();
            readonly Dictionary<string, PinnedSourceButton> buttons = new Dictionary<string, PinnedSourceButton>();
            readonly List<PinnedSourceButton> visibleButtons = new List<PinnedSourceButton>();
            bool arranging;
            internal int HeaderReserve;
            internal void SetFonts(Font source, Font title)
            { empty.Font = source; heading.Font = title; foreach (PinnedSourceButton b in buttons.Values) b.Font = source; }
            internal PinnedGroup(PinnedWindow parent, int dataFlow)
            {
                window = parent; flow = dataFlow;
                AccessibleName = flow == 0 ? "Playback sources" : "Microphone sources";
                AccessibleRole = AccessibleRole.Grouping;
                heading = new GroupHeading(parent, dataFlow);
                empty.Text = "No connected sources"; empty.TextAlign = ContentAlignment.TopLeft; empty.Font = new Font("Segoe UI", 8.5f);
                Controls.AddRange(new Control[] { heading, viewport }); viewport.Controls.Add(empty);
                viewport.HandleCreated += delegate { Palette.Scrollbar(viewport); };
                viewport.Scroll += delegate { window.owner.UpdateMeterState(); };
                viewport.Resize += delegate { Arrange(); };
                MouseDown += delegate(object s, MouseEventArgs e) { if (e.Button == MouseButtons.Left) window.DragWindow(); };
                viewport.MouseDown += delegate(object s, MouseEventArgs e) { if (e.Button == MouseButtons.Left) window.DragWindow(); };
            }
            internal void RefreshSources(List<Device> sources)
            {
                AppController app = window.owner.App; Palette p = Palette.Current;
                BackColor = viewport.BackColor = p.Surface; empty.ForeColor = p.Muted;
                // Keep each known endpoint's controls across hide/reconnect/order changes.
                // Legacy accessibility clients can retain exported disposed controls.
                foreach (string id in buttons.Keys.Where(id => !app.Devices.Any(d => d.Id == id)).ToArray())
                {
                    PinnedSourceButton removed = buttons[id]; viewport.Controls.Remove(removed); removed.Dispose(); buttons.Remove(id);
                }
                HashSet<string> shown = new HashSet<string>(sources.Select(d => d.Id));
                foreach (KeyValuePair<string, PinnedSourceButton> pair in buttons)
                    if (!shown.Contains(pair.Key) && pair.Value.Visible) pair.Value.Visible = false;
                visibleButtons.Clear();
                foreach (Device d in sources)
                {
                    PinnedSourceButton b;
                    if (!buttons.TryGetValue(d.Id, out b))
                    {
                        b = new PinnedSourceButton(window, d);
                        if (window.sourceFont != null) b.Font = window.sourceFont;
                        buttons[d.Id] = b; viewport.Controls.Add(b);
                    }
                    b.TabIndex = visibleButtons.Count; visibleButtons.Add(b); if (!b.Visible) b.Visible = true;
                }
                foreach (Device d in sources)
                {
                    PinnedSourceButton b; if (!buttons.TryGetValue(d.Id, out b)) continue;
                    b.Text = (d.Current ? "●  " : "") + app.Settings.Display(d); b.Selected = d.Current;
                    b.AccessibleName = "Switch " + (flow == 0 ? "playback" : "microphone") + " to " + app.Settings.Display(d);
                    b.Meter.Visible = app.Settings.Meters; b.Meter.AccessibleName = app.Settings.Display(d) + " audio activity";
                    b.Invalidate(true);
                }
                empty.Visible = sources.Count == 0;
                ApplyMenu(this, window.ContextMenuStrip); Palette.Scrollbar(viewport); Arrange(); Invalidate(true);
            }
            internal void Arrange()
            {
                if (arranging || window == null) return; arranging = true;
                int pad = window.R(8), gap = window.R(5), header = Math.Max(window.S(32), window.R(42));
                CornerRadius = window.R(13);
                heading.SetBounds(pad, window.R(6), Math.Max(window.R(45), Width - pad * 2 - HeaderReserve), Math.Max(window.S(24), window.R(30)));
                viewport.SetBounds(pad, header, Math.Max(window.R(70), Width - pad * 2), Math.Max(window.R(30), Height - header - pad));
                int rowHeight = Math.Max(window.S(24), window.R(window.owner.App.Settings.Meters ? 44 : 38));
                if (visibleButtons.Count > 0) rowHeight = Math.Max(window.S(24), Math.Min(rowHeight, (viewport.ClientSize.Height - gap * (visibleButtons.Count - 1)) / visibleButtons.Count));
                viewport.AutoScrollMinSize = new Size(0, Math.Max(0, visibleButtons.Count * (rowHeight + gap) - gap));
                int y = viewport.AutoScrollPosition.Y;
                foreach (PinnedSourceButton b in visibleButtons)
                {
                    b.SetBounds(0, y, Math.Max(window.S(60), viewport.ClientSize.Width - 1), rowHeight);
                    b.VisualScale = window.contentScale;
                    b.Meter.SetBounds(window.R(12), rowHeight - window.R(7), Math.Max(window.R(20), b.Width - window.R(24)), Math.Max(1, window.R(2)));
                    y += rowHeight + gap;
                }
                empty.SetBounds(0, window.S(6), Math.Max(window.S(80), viewport.ClientSize.Width), window.S(35));
                arranging = false;
            }
            internal void AddVisibleMeters(Dictionary<string, MeterBar> targets)
            {
                foreach (KeyValuePair<string, PinnedSourceButton> pair in buttons)
                    if (pair.Value.Visible && viewport.ClientRectangle.IntersectsWith(pair.Value.Bounds)) targets[pair.Key] = pair.Value.Meter;
            }
            protected override void OnLayout(LayoutEventArgs e) { base.OnLayout(e); Arrange(); }
        }
        sealed class GroupHeading : Control
        {
            readonly PinnedWindow window; readonly int flow;
            internal GroupHeading(PinnedWindow parent, int dataFlow)
            {
                window = parent; flow = dataFlow; Text = flow == 0 ? "Playback" : "Microphones";
                Font = new Font("Segoe UI Semibold", 9); AccessibleName = Text;
                SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
                MouseDown += delegate(object s, MouseEventArgs e) { if (e.Button == MouseButtons.Left) window.DragWindow(); };
            }
            protected override void OnPaint(PaintEventArgs e)
            {
                e.Graphics.Clear(Palette.Current.Surface);
                int icon = window.R(17); Art.Icon(e.Graphics, new Rectangle(1, (Height - icon) / 2, icon, icon), flow == 0 ? 3 : 1, Palette.Current.Muted);
                TextRenderer.DrawText(e.Graphics, Text, Font, new Rectangle(icon + window.R(6), 0, Math.Max(1, Width - icon - window.R(6)), Height), Palette.Current.Text,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            }
        }
        sealed class PinnedSourceButton : ActionButton
        {
            readonly PinnedWindow window; readonly Device source;
            internal readonly MeterBar Meter = new MeterBar();
            Point mouseDown; bool moved;
            internal PinnedSourceButton(PinnedWindow parent, Device device)
            {
                window = parent; source = device; Font = new Font("Segoe UI", 9);
                Controls.Add(Meter);
                Meter.MouseDown += delegate(object s, MouseEventArgs e) { BeginPointer(e); };
                Meter.MouseMove += delegate(object s, MouseEventArgs e) { MovePointer(e); };
                Meter.Click += delegate { if (!moved) PerformClick(); };
            }
            void BeginPointer(MouseEventArgs e) { if (e.Button == MouseButtons.Left) { mouseDown = Cursor.Position; moved = false; } }
            void MovePointer(MouseEventArgs e)
            {
                if (e.Button != MouseButtons.Left || moved) return;
                if (Math.Abs(Cursor.Position.X - mouseDown.X) + Math.Abs(Cursor.Position.Y - mouseDown.Y) > window.S(7))
                { moved = true; window.DragWindow(); }
            }
            protected override void OnMouseDown(MouseEventArgs e) { BeginPointer(e); base.OnMouseDown(e); }
            protected override void OnMouseMove(MouseEventArgs e) { MovePointer(e); base.OnMouseMove(e); }
            protected override void OnKeyDown(KeyEventArgs e) { moved = false; base.OnKeyDown(e); }
            protected override void OnClick(EventArgs e)
            {
                if (!moved) window.owner.App.Run(delegate { window.owner.App.Switch(source.Id); });
                base.OnClick(e);
            }
        }
    }
}
