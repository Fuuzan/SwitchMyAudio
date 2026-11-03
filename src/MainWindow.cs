using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Microsoft.Win32;
using System.Diagnostics;

namespace SwitchMyAudio
{
    internal sealed class MainWindow : ThemedForm
    {
        internal readonly AppController App;
        readonly Panel header = new BufferedPanel(), tabs = new BufferedPanel(), sourceArea = new VerticalPanel();
        readonly Label title = new Label(), cycleTitle = new Label(), cycleHint = new Label(), status = new Label();
        readonly Label emptySources = new Label();
        readonly ActionButton settingsButton = new ActionButton(), pinButton = new ActionButton(), playback = new ActionButton(), microphones = new ActionButton(), cycleKey = new ActionButton();
        readonly TilePanel cyclePanel = new TilePanel();
        readonly List<SourceCard> cards = new List<SourceCard>();
        readonly Dictionary<string, SourceCard> sourceCards = new Dictionary<string, SourceCard>();
        readonly Timer meterTimer = new Timer { Interval = 30 };
        readonly Timer geometryTimer = new Timer { Interval = 700 };
        readonly ToolTip tips = new ToolTip();
        string signature = "";
        readonly Dictionary<string, MeterBar> meterTargets = new Dictionary<string, MeterBar>();
        readonly Stopwatch meterClock = Stopwatch.StartNew();
        long lastMeterTick, nextPeakSample;
        int preparedMeterVersion = -1;
        bool meterTargetsDirty = true;
        bool ready, layingOut, quitting, disposeOnce, restoring;
        int flow;
        internal PinnedWindow PinnedWindow;
        internal bool MeterRunning { get { return meterTimer.Enabled; } }

        internal MainWindow(AppController app)
        {
            App = app; Text = "Switch My Audio"; Font = new Font("Segoe UI", 10);
            AutoScaleMode = AutoScaleMode.Dpi; StartPosition = FormStartPosition.Manual;
            MinimumSize = new Size(460, 500); ClientSize = new Size(640, 700);
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.ResizeRedraw, true); DoubleBuffered = true;
            title.Text = "Switch My Audio"; title.Font = new Font("Segoe UI Semibold", 16); title.AutoSize = false; title.AutoEllipsis = true;
            settingsButton.Text = "Settings"; settingsButton.AccessibleName = "Open Settings";
            settingsButton.Glyph = "Settings";
            pinButton.Text = "Pin"; pinButton.AccessibleName = "Pin sources on top";
            pinButton.Glyph = "Pin";
            tips.SetToolTip(pinButton, "Keep sources in a small window on top.");
            tips.SetToolTip(settingsButton, "Change appearance and behavior.");
            tips.SetToolTip(playback, "Choose where sound plays."); tips.SetToolTip(microphones, "Choose which microphone is used.");
            settingsButton.Click += delegate { OpenSettings(); };
            pinButton.Click += delegate { Pin(); };
            playback.Text = "Playback"; microphones.Text = "Microphones";
            playback.Click += delegate { SetFlow(0); }; microphones.Click += delegate { SetFlow(1); };
            header.Controls.AddRange(new Control[] { title, pinButton, settingsButton });
            tabs.Controls.AddRange(new Control[] { playback, microphones });
            sourceArea.AutoScroll = true; sourceArea.Scroll += delegate { meterTargetsDirty = true; };
            emptySources.Text = "No visible sources. Connect a device or check Settings → Devices.";
            emptySources.TextAlign = ContentAlignment.MiddleCenter; emptySources.Visible = false;
            sourceArea.Controls.Add(emptySources);
            sourceArea.HandleCreated += delegate { Palette.Scrollbar(sourceArea); };
            sourceArea.Resize += delegate { LayoutCards(); };
            cycleTitle.Text = "Cycle selected sources"; cycleTitle.Font = new Font("Segoe UI Semibold", 11);
            cycleKey.Click += delegate { using (ShortcutDialog d = new ShortcutDialog(App, "cycle" + flow, flow)) d.ShowDialog(this); };
            cyclePanel.Controls.AddRange(new Control[] { cycleTitle, cycleKey, cycleHint });
            cycleHint.Font = new Font("Segoe UI", 9);
            status.Font = new Font("Segoe UI", 9); status.AutoEllipsis = true;
            Controls.AddRange(new Control[] { header, tabs, sourceArea, cyclePanel, status });
            ContextMenuStrip menu = new ContextMenuStrip(); menu.Items.Add("Refresh sources", null, delegate { App.Refresh(false); });
            menu.Items.Add("Settings", null, delegate { OpenSettings(); }); ContextMenuStrip = menu;
            App.Changed += RefreshView; App.Feedback += ShowFeedback;
            App.OpenRequested += OpenFull;
            meterTimer.Tick += delegate { SampleMeters(); };
            geometryTimer.Tick += delegate { geometryTimer.Stop(); RememberBounds(); App.SaveGeometry(); };
            Move += delegate { RememberSoon(); };
            Resize += delegate { PerformLayout(); RememberSoon(); UpdateMeterState(); };
            VisibleChanged += delegate { UpdateMeterState(); };
            Shown += delegate { ready = true; RefreshView(); UpdateMeterState(); };
            SystemEvents.UserPreferenceChanged += SystemThemeChanged;
            RestoreBoundsSaved(); RefreshView();
            status.Text = App.LastError ?? ""; status.Visible = App.LastError != null;
            if (App.LastError != null) status.ForeColor = Palette.Current.Error;
        }
        void SystemThemeChanged(object sender, UserPreferenceChangedEventArgs e)
        { if (App.Settings.Theme == "System") App.Host.Dispatch(delegate { App.Notify(); }); }
        internal int S(int value) { return (int)Math.Round(value * DeviceDpi / 96.0); }
        void RestoreBoundsSaved()
        {
            Rectangle desired = new Rectangle(App.Settings.WindowX, App.Settings.WindowY, App.Settings.WindowWidth, App.Settings.WindowHeight);
            if (desired.X == int.MinValue || !Screen.AllScreens.Any(s => Rectangle.Intersect(s.WorkingArea, desired).Width >= 100 && Rectangle.Intersect(s.WorkingArea, desired).Height >= 100))
            {
                Rectangle work = Screen.PrimaryScreen.WorkingArea;
                Size = new Size(Math.Min(660, work.Width), Math.Min(760, work.Height));
                Location = new Point(work.X + (work.Width - Width) / 2, work.Y + (work.Height - Height) / 2);
            }
            else Bounds = desired;
        }
        void RememberSoon()
        {
            if (!ready || WindowState != FormWindowState.Normal || !Visible) return;
            geometryTimer.Stop(); geometryTimer.Start();
        }
        void RememberBounds()
        {
            if (WindowState != FormWindowState.Normal) return;
            App.Settings.WindowX = Left; App.Settings.WindowY = Top;
            App.Settings.WindowWidth = Width; App.Settings.WindowHeight = Height;
        }
        internal void SetFlow(int value) { flow = value; signature = ""; RefreshView(); }
        internal void RefreshView()
        {
            if (IsDisposed) return;
            Palette.Set(App.Settings.Theme); Palette.Window(this); Palette p = Palette.Current;
            header.BackColor = tabs.BackColor = sourceArea.BackColor = p.Background;
            Palette.Scrollbar(sourceArea);
            title.ForeColor = p.Text; playback.Selected = flow == 0; microphones.Selected = flow == 1;
            cyclePanel.BackColor = p.Surface;
            cycleTitle.ForeColor = p.Text; cycleTitle.BackColor = p.Surface; cycleHint.ForeColor = p.Muted; cycleHint.BackColor = p.Surface;
            status.ForeColor = App.LastError == null ? p.Muted : p.Error;
            status.Text = App.LastError ?? ""; status.Visible = App.LastError != null;
            List<Device> visible = App.Settings.Ordered(App.Devices, flow, false);
            string next = string.Join("|", visible.Select(d => d.Id));
            if (next != signature && !cards.Any(c => c.Editing))
            {
                signature = next;
                using (new PaintBatch(sourceArea))
                {
                foreach (string id in sourceCards.Keys.Where(id => !App.Devices.Any(d => d.Id == id)).ToArray())
                { SourceCard removed = sourceCards[id]; sourceArea.Controls.Remove(removed); removed.Dispose(); sourceCards.Remove(id); }
                HashSet<string> shown = new HashSet<string>(visible.Select(d => d.Id));
                foreach (SourceCard c in sourceCards.Values) if (!shown.Contains(c.Device.Id) && c.Visible) c.Visible = false;
                cards.Clear();
                foreach (Device d in visible)
                {
                    SourceCard c;
                    if (!sourceCards.TryGetValue(d.Id, out c))
                    {
                        c = new SourceCard(App, d); c.EditFinished += delegate { RefreshView(); };
                        sourceCards[d.Id] = c; sourceArea.Controls.Add(c);
                    }
                    c.TabIndex = cards.Count; cards.Add(c); if (!c.Visible) c.Visible = true;
                }
                LayoutCards();
                }
                meterTargetsDirty = true;
            }
            foreach (SourceCard c in cards)
            {
                Device d = App.Devices.FirstOrDefault(x => x.Id == c.Device.Id);
                if (d != null) c.UpdateDevice(d);
            }
            emptySources.Visible = cards.Count == 0; emptySources.ForeColor = p.Muted;
            cycleKey.Text = KeyText.Label(flow == 0 ? App.Settings.OutputKey : App.Settings.InputKey, flow == 0 ? App.Settings.OutputMods : App.Settings.InputMods);
            string problem;
            if (App.Keys.Problems.TryGetValue("cycle" + flow, out problem)) { cycleKey.Text = "! " + cycleKey.Text; tips.SetToolTip(cycleKey, problem); }
            else tips.SetToolTip(cycleKey, "Set one shortcut to cycle through the checked sources.");
            int selected = visible.Count(d => App.Settings.For(d).Cycle);
            cycleHint.Text = selected < 2 ? "Check Cycle on at least two sources." : selected + " selected · skips disconnected";
            if (cards.Count == 0)
            {
                sourceArea.AccessibleName = "No sources. Connect a device or show hidden sources in Settings.";
                sourceArea.Invalidate();
            }
            if (PinnedWindow != null && !PinnedWindow.IsDisposed) PinnedWindow.RefreshSources();
            PerformLayout(); UpdateMeterState(); Invalidate(true);
        }
        protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); Palette.Window(this); }
        protected override void OnLayout(LayoutEventArgs e)
        {
            base.OnLayout(e); if (layingOut || header == null) return; layingOut = true;
            int pad = S(22), w = ClientSize.Width - pad * 2;
            header.SetBounds(pad, S(15), w, S(43));
            title.SetBounds(0, 0, Math.Max(S(100), w - S(206)), S(40));
            pinButton.SetBounds(w - S(192), S(3), S(72), S(35)); settingsButton.SetBounds(w - S(110), S(3), S(110), S(35));
            tabs.SetBounds(pad, S(74), w, S(40));
            playback.SetBounds(0, 0, (w - S(8)) / 2, S(40));
            microphones.SetBounds((w + S(8)) / 2, 0, (w - S(8)) / 2, S(40));
            int cycleHeight = w < S(540) ? S(112) : S(90);
            int statusY = ClientSize.Height - S(29);
            int bottom = status.Visible ? statusY - S(13) : ClientSize.Height - S(16);
            cyclePanel.SetBounds(pad, bottom - cycleHeight, w, cycleHeight);
            cycleTitle.SetBounds(S(16), S(12), Math.Min(S(240), w - S(32)), S(25));
            if (w < S(540))
            {
                cycleKey.SetBounds(S(16), S(45), w - S(32), S(33));
                cycleHint.SetBounds(S(16), S(83), w - S(32), S(22));
            }
            else
            {
                cycleKey.SetBounds(w - S(236), S(24), S(220), S(36));
                cycleHint.SetBounds(S(16), S(46), Math.Max(S(180), w - S(265)), S(29));
            }
            sourceArea.SetBounds(pad, S(130), w, Math.Max(S(70), cyclePanel.Top - S(145)));
            status.SetBounds(pad, statusY, w, S(23));
            layingOut = false; LayoutCards();
        }
        void LayoutCards()
        {
            if (layingOut || sourceArea == null) return; layingOut = true;
            int gap = S(10), h = S(122);
            int total = cards.Count * (h + gap);
            sourceArea.AutoScrollMinSize = new Size(0, Math.Max(0, total - gap));
            int width = Math.Max(S(80), sourceArea.ClientSize.Width - S(2));
            int y = sourceArea.AutoScrollPosition.Y;
            foreach (SourceCard c in cards) { c.SetBounds(0, y, width, h); y += h + gap; }
            emptySources.SetBounds(S(12), S(15), Math.Max(S(80), width - S(24)), S(100));
            meterTargetsDirty = true; layingOut = false;
        }
        void ShowFeedback(string text, bool error)
        {
            if (IsDisposed) return;
            status.Text = error ? text : ""; status.Visible = error; status.ForeColor = Palette.Current.Error;
            tips.SetToolTip(status, error ? text : null); PerformLayout();
        }
        internal void OpenSettings()
        {
            using (SettingsWindow settings = new SettingsWindow(App)) settings.ShowDialog(this);
            RefreshView();
        }
        internal void Pin()
        {
            if (PinnedWindow != null && PinnedWindow.Visible) return;
            RememberBounds();
            if (!App.Change(s => s.Pinned = true)) return;
            if (PinnedWindow == null || PinnedWindow.IsDisposed) PinnedWindow = new PinnedWindow(this);
            bool cloaked = PinnedWindow.Cloak(true);
            try { PinnedWindow.Show(); PinnedWindow.PaintBeforeReveal(); }
            finally { if (cloaked) PinnedWindow.Cloak(false); }
            PinnedWindow.Activate(); Hide(); UpdateMeterState();
        }
        internal void OpenFull()
        {
            if (restoring || IsDisposed) return; restoring = true;
            PinnedWindow pinned = PinnedWindow;
            try
            {
                if (App.Settings.Pinned) App.Change(s => s.Pinned = false);
                if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
                bool cloaked = !Visible && Cloak(true);
                try { RefreshView(); Show(); PaintBeforeReveal(); }
                finally { if (cloaked) Cloak(false); }
                if (pinned != null && !pinned.IsDisposed) pinned.SaveAndHide();
                Activate(); UpdateMeterState();
            }
            finally { restoring = false; }
        }
        internal void BeginInitialView(bool silent)
        {
            ready = true;
            if (App.Settings.Pinned) Pin();
            else if (!silent) Show();
            UpdateMeterState();
        }
        internal void UpdateMeterState()
        {
            if (disposeOnce) return;
            meterTargetsDirty = true;
            bool pinVisible = PinnedWindow != null && !PinnedWindow.IsDisposed && PinnedWindow.Visible;
            int shownFlow = pinVisible ? PinnedWindow.Mode : flow;
            bool hasSources = App.Devices.Any(d => d.Online && !App.Settings.For(d).Hidden && (shownFlow == 2 || d.Flow == shownFlow));
            bool active = App.Settings.Meters && hasSources && (pinVisible || (Visible && WindowState != FormWindowState.Minimized));
            if (active && !meterTimer.Enabled) { meterClock.Restart(); lastMeterTick = 0; nextPeakSample = 0; }
            meterTimer.Enabled = active;
            if (!active)
            {
                foreach (MeterBar bar in meterTargets.Values) if (!bar.IsDisposed) bar.Reset();
                meterTargets.Clear(); preparedMeterVersion = -1; App.PrepareMeters(new string[0]);
            }
        }
        internal void SampleMeters()
        {
            if (!meterTimer.Enabled || !App.Settings.Meters) return;
            long now = meterClock.ElapsedMilliseconds;
            double elapsed = lastMeterTick == 0 ? .030 : Math.Max(.001, (now - lastMeterTick) / 1000.0); lastMeterTick = now;
            bool targetsChanged = meterTargetsDirty || preparedMeterVersion != App.MeterVersion;
            if (targetsChanged)
            {
                meterTargets.Clear();
                if (PinnedWindow != null && PinnedWindow.Visible)
                    foreach (KeyValuePair<string, MeterBar> pair in PinnedWindow.VisibleMeters()) meterTargets[pair.Key] = pair.Value;
                else if (Visible && WindowState != FormWindowState.Minimized)
                    foreach (SourceCard c in cards)
                        if (c.Device.Online && c.Visible && sourceArea.ClientRectangle.IntersectsWith(c.Bounds)) meterTargets[c.Device.Id] = c.Meter;
                App.PrepareMeters(meterTargets.Keys); preparedMeterVersion = App.MeterVersion; meterTargetsDirty = false;
            }
            // Native drivers vary in peak-read cost. Sample at 10 Hz and animate
            // at the shared frame rate, using the last reading between samples.
            bool sample = targetsChanged || now >= nextPeakSample;
            if (sample) nextPeakSample = now + 100;
            foreach (KeyValuePair<string, MeterBar> pair in meterTargets)
            { if (!pair.Value.IsDisposed) { if (sample) pair.Value.Peak = App.Peak(pair.Key); pair.Value.Advance(elapsed); } }
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (cards.Count == 0 && sourceArea != null)
                TextRenderer.DrawText(e.Graphics, "No visible sources. Connect a device or check Settings → Devices.", Font,
                    sourceArea.Bounds, Palette.Current.Muted, TextFormatFlags.HorizontalCenter | TextFormatFlags.WordBreak);
        }
        internal void Quit()
        {
            quitting = true; RememberBounds();
            if (PinnedWindow != null) { PinnedWindow.SaveAndHide(); PinnedWindow.Dispose(); }
            App.SaveGeometry(); Close(); Application.ExitThread();
        }
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!quitting && e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; RememberBounds(); App.SaveGeometry(); Hide(); UpdateMeterState(); }
            else { RememberBounds(); App.SaveGeometry(); }
            base.OnFormClosing(e);
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing && !disposeOnce)
            {
                disposeOnce = true; SystemEvents.UserPreferenceChanged -= SystemThemeChanged;
                meterTimer.Dispose(); geometryTimer.Dispose(); tips.Dispose();
                if (ContextMenuStrip != null) ContextMenuStrip.Dispose();
                App.Changed -= RefreshView; App.Feedback -= ShowFeedback; App.OpenRequested -= OpenFull;
                if (PinnedWindow != null) PinnedWindow.Dispose();
            }
            base.Dispose(disposing);
        }
        internal void SavePreview(string path)
        {
            Show(); RefreshView(); Application.DoEvents();
            using (Bitmap bmp = new Bitmap(Width, Height)) { DrawToBitmap(bmp, new Rectangle(0, 0, Width, Height)); bmp.Save(path); }
        }
    }

}

