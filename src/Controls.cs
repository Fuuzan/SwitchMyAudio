using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using Microsoft.Win32;

namespace SwitchMyAudio
{
    internal sealed class Palette
    {
        internal bool Dark;
        internal Color Background, Surface, Field, Border, Text, Muted, Accent, AccentText, Selected, Error;
        internal static Palette Current = Create(true);
        internal static Palette Create(bool dark)
        {
            return dark ? new Palette { Dark = true, Background = Color.FromArgb(29, 33, 36), Surface = Color.FromArgb(40, 46, 50),
                Field = Color.FromArgb(48, 55, 60), Border = Color.FromArgb(66, 75, 81), Text = Color.FromArgb(241, 245, 246),
                Muted = Color.FromArgb(170, 183, 188), Accent = Color.FromArgb(157, 232, 204), AccentText = Color.FromArgb(17, 45, 36),
                Selected = Color.FromArgb(43, 66, 60), Error = Color.FromArgb(255, 171, 160) }
                : new Palette { Dark = false, Background = Color.FromArgb(246, 248, 249), Surface = Color.White,
                    Field = Color.FromArgb(239, 242, 244), Border = Color.FromArgb(214, 222, 226), Text = Color.FromArgb(29, 39, 45),
                    Muted = Color.FromArgb(93, 110, 119), Accent = Color.FromArgb(24, 153, 115), AccentText = Color.White,
                    Selected = Color.FromArgb(218, 245, 235), Error = Color.FromArgb(167, 47, 37) };
        }
        internal static void Set(string mode)
        {
            bool dark = mode != "Light";
            if (mode == "System") try
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Themes\\Personalize"))
                    dark = k != null && Convert.ToInt32(k.GetValue("AppsUseLightTheme", 1)) == 0;
            }
            catch { dark = false; }
            Current = Create(dark);
        }
        internal static void Window(Form window)
        {
            window.BackColor = Current.Background; window.ForeColor = Current.Text;
            if (window.IsHandleCreated) try
            {
                int dark = Current.Dark ? 1 : 0;
                Win32.DwmSetWindowAttribute(window.Handle, 20, ref dark, 4);
                int background = ColorTranslator.ToWin32(Current.Background), text = ColorTranslator.ToWin32(Current.Text);
                // Windows 11 supports explicit native caption and border colors.
                // Older Windows versions safely retain their standard frame.
                Win32.DwmSetWindowAttribute(window.Handle, 35, ref background, 4);
                Win32.DwmSetWindowAttribute(window.Handle, 36, ref text, 4);
                Win32.DwmSetWindowAttribute(window.Handle, 34, ref background, 4);
            }
            catch (DllNotFoundException) { }
        }
        internal static void Scrollbar(Control control)
        {
            if (!control.IsHandleCreated) return;
            try { Win32.SetWindowTheme(control.Handle, Current.Dark ? "DarkMode_Explorer" : "Explorer", null); } catch (DllNotFoundException) { }
        }
    }

    internal static class Art
    {
        internal static GraphicsPath Round(RectangleF r, float radius)
        {
            GraphicsPath path = new GraphicsPath(); float d = radius * 2;
            if (d <= 1) { path.AddRectangle(r); return path; }
            path.AddArc(r.X, r.Y, d, d, 180, 90); path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90); path.AddArc(r.X, r.Bottom - d, d, d, 90, 90); path.CloseFigure(); return path;
        }
        internal static void Box(Graphics g, Rectangle r, Color fill, Color border, int radius)
        {
            if (r.Width < 2 || r.Height < 2) return;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (GraphicsPath path = Round(new RectangleF(r.X + .5f, r.Y + .5f, r.Width - 1, r.Height - 1), radius))
            using (Brush b = new SolidBrush(fill)) using (Pen pen = new Pen(border))
            { g.FillPath(b, path); g.DrawPath(pen, path); }
        }
        internal static void Icon(Graphics g, Rectangle r, int kind, Color color)
        {
            GraphicsState saved = g.Save(); g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TranslateTransform(r.X, r.Y); g.ScaleTransform(r.Width / 32f, r.Height / 32f);
            using (Pen p = new Pen(color, 2.3f))
            {
                p.StartCap = p.EndCap = LineCap.Round; p.LineJoin = LineJoin.Round;
                if (kind == 1) // microphone
                {
                    using (GraphicsPath capsule = Round(new RectangleF(12, 3, 8, 17), 4)) g.DrawPath(p, capsule);
                    g.DrawArc(p, 7, 8, 18, 18, 0, 180); g.DrawLine(p, 16, 26, 16, 30); g.DrawLine(p, 11, 30, 21, 30);
                }
                else if (kind == 2) // display
                { g.DrawRectangle(p, 3, 4, 26, 20); g.DrawLine(p, 16, 24, 16, 29); g.DrawLine(p, 10, 29, 22, 29); }
                else if (kind == 3) // speakers
                { g.DrawPolygon(p, new Point[] { new Point(4, 12), new Point(10, 12), new Point(18, 5), new Point(18, 27), new Point(10, 20), new Point(4, 20) }); g.DrawArc(p, 16, 8, 12, 16, -65, 130); }
                else // headphones
                { g.DrawArc(p, 4, 4, 24, 25, 180, 180); g.DrawLine(p, 4, 16, 4, 26); g.DrawLine(p, 28, 16, 28, 26); g.DrawRectangle(p, 5, 17, 4, 10); g.DrawRectangle(p, 23, 17, 4, 10); }
            }
            g.Restore(saved);
        }
        internal static void ButtonIcon(Graphics g, Rectangle r, string kind, Color color)
        {
            GraphicsState state = g.Save();
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TranslateTransform(r.X, r.Y); g.ScaleTransform(r.Width / 24f, r.Height / 24f);
            using (Pen pen = new Pen(color, 1.7f))
            {
                pen.LineJoin = LineJoin.Round; pen.StartCap = pen.EndCap = LineCap.Round;
                if (kind == "Settings")
                {
                    PointF[] gear = new PointF[32];
                    for (int i = 0; i < gear.Length; i++)
                    {
                        double a = Math.PI * 2 * i / gear.Length - Math.PI / 2;
                        float radius = i % 4 == 0 || i % 4 == 3 ? 9.6f : 7.4f;
                        gear[i] = new PointF(12 + (float)Math.Cos(a) * radius, 12 + (float)Math.Sin(a) * radius);
                    }
                    g.DrawPolygon(pen, gear); g.DrawEllipse(pen, 8.7f, 8.7f, 6.6f, 6.6f);
                }
                else if (kind == "MoveUp" || kind == "MoveDown")
                {
                    float top = kind == "MoveUp" ? 5 : 19, bottom = kind == "MoveUp" ? 19 : 5;
                    g.DrawLine(pen, 12, bottom, 12, top);
                    float wing = kind == "MoveUp" ? 11 : 13;
                    g.DrawLines(pen, new PointF[] { new PointF(6, wing), new PointF(12, top), new PointF(18, wing) });
                }
                else
                {
                    g.DrawLines(pen, new PointF[] { new PointF(9, 3), new PointF(18, 3), new PointF(18, 5), new PointF(16, 6), new PointF(16, 11), new PointF(20, 15), new PointF(7, 15), new PointF(11, 11), new PointF(11, 6), new PointF(9, 5), new PointF(9, 3) });
                    g.DrawLine(pen, 13.5f, 15, 13.5f, 22);
                    if (kind == "Unpin") g.DrawLine(pen, 3, 4, 21, 21);
                }
            }
            g.Restore(state);
        }
    }

    internal class ActionButton : Button
    {
        internal bool Selected, Plain;
        internal string Glyph;
        internal float VisualScale = 1;
        protected bool Hovered;
        internal ActionButton()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            FlatStyle = FlatStyle.Flat; FlatAppearance.BorderSize = 0; Cursor = Cursors.Hand;
            Font = new Font("Segoe UI", 10); Height = 36; UseVisualStyleBackColor = false;
        }
        protected virtual string Caption { get { return Text; } }
        protected override void OnMouseEnter(EventArgs e) { Hovered = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { Hovered = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
        protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }
        protected override void OnPaint(PaintEventArgs e)
        {
            Palette p = Palette.Current;
            e.Graphics.Clear(Parent == null ? p.Background : Parent.BackColor);
            Color fill = Selected ? p.Accent : Plain ? (Parent == null ? p.Surface : Parent.BackColor) : Hovered ? p.Border : p.Field;
            if (!Plain || Hovered) Art.Box(e.Graphics, ClientRectangle, fill, Selected ? p.Accent : Plain ? fill : p.Border, Math.Max(5, (int)(8 * VisualScale)));
            Color text = !Enabled ? p.Muted : Selected ? p.AccentText : p.Text;
            Rectangle textBounds = Rectangle.Inflate(ClientRectangle, -9, -2);
            if (!string.IsNullOrEmpty(Glyph))
            {
                int iconSize = Math.Max(12, (int)(17 * DeviceDpi / 96f * VisualScale)), gap = string.IsNullOrEmpty(Caption) ? 0 : Math.Max(4, (int)(7 * DeviceDpi / 96f * VisualScale));
                int textWidth = string.IsNullOrEmpty(Caption) ? 0 : TextRenderer.MeasureText(Caption, Font, Size.Empty, TextFormatFlags.NoPadding).Width;
                int start = Math.Max(6, (Width - iconSize - gap - textWidth) / 2);
                Art.ButtonIcon(e.Graphics, new Rectangle(start, (Height - iconSize) / 2, iconSize, iconSize), Glyph, text);
                textBounds = new Rectangle(start + iconSize + gap, 0, Math.Max(1, Width - start - iconSize - gap - 5), Height);
            }
            TextRenderer.DrawText(e.Graphics, Caption, Font, textBounds, text,
                (string.IsNullOrEmpty(Glyph) ? TextFormatFlags.HorizontalCenter : TextFormatFlags.Left) | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);
            if (Focused && ShowFocusCues) ControlPaint.DrawFocusRectangle(e.Graphics, Rectangle.Inflate(ClientRectangle, -4, -4), text, fill);
        }
    }
    internal sealed class RenameButton : ActionButton
    {
        internal RenameButton() { Plain = true; AccessibleRole = AccessibleRole.PushButton; }
        protected override string Caption { get { return Hovered ? "Rename" : Text; } }
    }
    internal sealed class Badge : Control
    {
        internal Badge() { SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true); Font = new Font("Segoe UI", 8.5f); }
        protected override void OnPaint(PaintEventArgs e)
        {
            Palette p = Palette.Current; e.Graphics.Clear(Parent.BackColor);
            Art.Box(e.Graphics, ClientRectangle, p.Selected, p.Selected, 8);
            TextRenderer.DrawText(e.Graphics, Text, Font, ClientRectangle, p.Dark ? p.Accent : Color.FromArgb(18, 112, 79), TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }
    }
    internal sealed class MeterBar : Control
    {
        float peak, displayPeak;
        internal float Peak { get { return peak; } set { peak = float.IsNaN(value) ? 0 : Math.Max(0, Math.Min(1, value)); } }
        internal float DisplayPeak { get { return displayPeak; } }
        internal void Reset() { peak = displayPeak = 0; Invalidate(); }
        internal void Advance(double seconds)
        {
            float next = peak + (displayPeak - peak) * (float)Math.Exp(-Math.Max(0, Math.Min(1, seconds)) / (peak > displayPeak ? .04 : .16));
            if (peak == 0 && next < .002f) next = 0;
            bool changed = (int)(next * Width) != (int)(displayPeak * Width);
            displayPeak = next; if (changed) Invalidate();
        }
        internal MeterBar() { SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true); Height = 3; AccessibleRole = AccessibleRole.ProgressBar; TabStop = false; }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(Palette.Current.Border);
            using (Brush b = new SolidBrush(Palette.Current.Accent)) e.Graphics.FillRectangle(b, 0, 0, Width * displayPeak, Height);
        }
        protected override AccessibleObject CreateAccessibilityInstance() { return new MeterAccessibility(this); }
        sealed class MeterAccessibility : ControlAccessibleObject
        {
            readonly MeterBar meter;
            internal MeterAccessibility(MeterBar owner) : base(owner) { meter = owner; }
            public override AccessibleRole Role { get { return AccessibleRole.ProgressBar; } }
            public override AccessibleStates State { get { return base.State | AccessibleStates.ReadOnly; } }
            public override string Value { get { return Math.Round(meter.Peak * 100) + "%"; } set { } }
        }
    }
    internal sealed class BufferedPanel : Panel
    {
        internal BufferedPanel() { SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.ResizeRedraw, true); }
    }
    internal sealed class CycleCheck : CheckBox
    {
        internal CycleCheck() { SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true); }
        protected override void OnPaint(PaintEventArgs e)
        {
            Palette p = Palette.Current; e.Graphics.Clear(Parent.BackColor);
            int side = Math.Max(16, (int)(18 * DeviceDpi / 96f));
            Rectangle box = new Rectangle(1, (Height - side) / 2, side, side);
            Art.Box(e.Graphics, box, Checked ? p.Accent : p.Field, Checked ? p.Accent : p.Border, 4);
            if (Checked) using (Pen pen = new Pen(p.AccentText, Math.Max(1.5f, DeviceDpi / 48f)))
                e.Graphics.DrawLines(pen, new Point[] { new Point(box.Left + side / 4, box.Top + side / 2), new Point(box.Left + side * 4 / 9, box.Top + side * 3 / 4), new Point(box.Left + side * 4 / 5, box.Top + side / 4) });
            TextRenderer.DrawText(e.Graphics, Text, Font, new Rectangle(side + 7, 0, Math.Max(1, Width - side - 7), Height), p.Text, TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
            if (Focused && ShowFocusCues) ControlPaint.DrawFocusRectangle(e.Graphics, ClientRectangle, p.Text, Parent.BackColor);
        }
    }
    internal sealed class ThemeCombo : ComboBox
    {
        readonly ComboFace face;
        readonly ListBackdrop list = new ListBackdrop();
        internal ThemeCombo()
        {
            DropDownStyle = ComboBoxStyle.DropDownList; DrawMode = DrawMode.OwnerDrawFixed; FlatStyle = FlatStyle.Flat;
            BackColor = Palette.Current.Field; ForeColor = Palette.Current.Text;
            ItemHeight = 25;
            face = new ComboFace(this); Controls.Add(face); face.Bounds = ClientRectangle;
        }
        protected override CreateParams CreateParams
        { get { CreateParams p = base.CreateParams; p.Style |= 0x02000000; return p; } }
        protected override void OnHandleCreated(EventArgs e)
        { base.OnHandleCreated(e); PrepareList(); }
        protected override void OnHandleDestroyed(EventArgs e)
        { list.Detach(); base.OnHandleDestroyed(e); }
        protected override void OnLayout(LayoutEventArgs e)
        { base.OnLayout(e); if (face != null) { face.Bounds = ClientRectangle; face.BringToFront(); } }
        void RefreshFace() { Invalidate(); if (face != null) face.Invalidate(); }
        internal void SetHint(ToolTip tips, string text)
        { tips.SetToolTip(this, text); tips.SetToolTip(face, text); }
        void PrepareList()
        {
            if (!IsHandleCreated) return;
            Win32.ComboInfo info = new Win32.ComboInfo { Size = System.Runtime.InteropServices.Marshal.SizeOf(typeof(Win32.ComboInfo)) };
            if (Win32.GetComboBoxInfo(Handle, ref info) && info.List != IntPtr.Zero) list.Attach(info.List);
        }
        protected override void OnSelectedIndexChanged(EventArgs e) { base.OnSelectedIndexChanged(e); RefreshFace(); }
        protected override void OnDropDown(EventArgs e) { PrepareList(); base.OnDropDown(e); RefreshFace(); }
        protected override void OnDropDownClosed(EventArgs e) { base.OnDropDownClosed(e); RefreshFace(); }
        protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); RefreshFace(); }
        protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); RefreshFace(); }
        protected override void OnEnabledChanged(EventArgs e) { base.OnEnabledChanged(e); RefreshFace(); }
        protected override void OnBackColorChanged(EventArgs e) { base.OnBackColorChanged(e); PrepareList(); RefreshFace(); }
        protected override void WndProc(ref Message m)
        {
            // Own the paint cycle, rather than painting dark over a native light
            // frame after it has already reached the screen.
            if (m.Msg == 0x14)
            {
                if (m.WParam != IntPtr.Zero) using (Graphics g = Graphics.FromHdc(m.WParam)) g.Clear(Palette.Current.Field);
                m.Result = new IntPtr(1); return;
            }
            if ((m.Msg == 0x317 || m.Msg == 0x318 || m.Msg == 0xF) && m.WParam != IntPtr.Zero)
            {
                using (Graphics g = Graphics.FromHdc(m.WParam)) PaintBuffered(g);
                m.Result = IntPtr.Zero; return;
            }
            if (m.Msg == 0xF && IsHandleCreated)
            {
                Win32.PaintInfo paint; IntPtr dc = Win32.BeginPaint(Handle, out paint);
                try { if (dc != IntPtr.Zero) using (Graphics g = Graphics.FromHdc(dc)) PaintBuffered(g); }
                finally { Win32.EndPaint(Handle, ref paint); }
                m.Result = IntPtr.Zero; return;
            }
            base.WndProc(ref m);
        }
        void PaintBuffered(Graphics target)
        {
            if (ClientSize.Width < 1 || ClientSize.Height < 1) return;
            using (BufferedGraphics buffer = BufferedGraphicsManager.Current.Allocate(target, ClientRectangle))
            { PaintFace(buffer.Graphics); buffer.Render(target); }
        }
        void PaintFace(Graphics g)
        {
            Palette p = Palette.Current;
            using (Brush background = new SolidBrush(Parent == null ? p.Background : Parent.BackColor)) g.FillRectangle(background, ClientRectangle);
            Art.Box(g, ClientRectangle, p.Field, Focused || DroppedDown ? p.Accent : p.Border, 7);
            int arrow = Math.Max(28, (int)(30 * DeviceDpi / 96f));
            TextRenderer.DrawText(g, Text, Font, new Rectangle(10, 0, Math.Max(1, Width - arrow - 10), Height), Enabled ? p.Text : p.Muted,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
            int cx = Width - arrow / 2, cy = Height / 2, d = Math.Max(3, (int)(4 * DeviceDpi / 96f));
            using (Pen pen = new Pen(p.Muted, 1.5f)) g.DrawLines(pen, new Point[] { new Point(cx - d, cy - 2), new Point(cx, cy + 2), new Point(cx + d, cy - 2) });
        }
        protected override void OnDrawItem(DrawItemEventArgs e)
        {
            if ((e.State & DrawItemState.ComboBoxEdit) != 0) { if (face != null) face.Invalidate(); return; }
            if (e.Index < 0) return;
            Palette p = Palette.Current; bool selected = (e.State & DrawItemState.Selected) != 0;
            using (Brush brush = new SolidBrush(selected ? p.Selected : p.Field)) e.Graphics.FillRectangle(brush, e.Bounds);
            TextRenderer.DrawText(e.Graphics, GetItemText(Items[e.Index]), Font, Rectangle.Inflate(e.Bounds, -5, 0), p.Text,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            if ((e.State & DrawItemState.Focus) != 0) e.DrawFocusRectangle();
        }
        protected override void Dispose(bool disposing)
        { if (disposing) list.Detach(); base.Dispose(disposing); }

        // A buffered face receives pointer hover instead of the native themed
        // arrow. The real ComboBox still owns focus, keys, values and the popup.
        sealed class ComboFace : Control
        {
            readonly ThemeCombo owner;
            internal ComboFace(ThemeCombo combo)
            {
                owner = combo; TabStop = false; AccessibleRole = AccessibleRole.None; Cursor = Cursors.Hand;
                SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            }
            protected override void OnPaintBackground(PaintEventArgs e) { }
            protected override void OnPaint(PaintEventArgs e) { owner.PaintFace(e.Graphics); }
            protected override void OnMouseDown(MouseEventArgs e)
            {
                if (e.Button == MouseButtons.Left && owner.Enabled) { owner.Focus(); owner.DroppedDown = !owner.DroppedDown; }
                base.OnMouseDown(e);
            }
        }
        sealed class ListBackdrop : NativeWindow
        {
            int theme = -1;
            internal void Attach(IntPtr handle)
            {
                if (Handle != handle) { Detach(); AssignHandle(handle); }
                int next = Palette.Current.Dark ? 1 : 0;
                if (theme != next)
                {
                    theme = next;
                    try { Win32.SetWindowTheme(Handle, next == 1 ? "DarkMode_Explorer" : "Explorer", null); } catch (DllNotFoundException) { }
                }
            }
            internal void Detach() { if (Handle != IntPtr.Zero) ReleaseHandle(); theme = -1; }
            protected override void WndProc(ref Message m)
            {
                if (m.Msg == 0x14 && m.WParam != IntPtr.Zero)
                {
                    using (Graphics g = Graphics.FromHdc(m.WParam)) g.Clear(Palette.Current.Field);
                    m.Result = new IntPtr(1); return;
                }
                base.WndProc(ref m);
            }
        }
    }
    internal sealed class Toggle : CheckBox
    {
        internal Toggle() { AutoSize = false; Size = new Size(44, 26); Text = ""; Cursor = Cursors.Hand; SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true); }
        protected override void OnPaint(PaintEventArgs e)
        {
            Palette p = Palette.Current; e.Graphics.Clear(Parent.BackColor);
            Rectangle track = new Rectangle(1, 3, Width - 3, Height - 6);
            Art.Box(e.Graphics, track, Checked ? p.Accent : p.Border, Checked ? p.Accent : p.Border, 10);
            int diameter = Height - 12;
            using (Brush b = new SolidBrush(Checked ? p.AccentText : p.Surface))
                e.Graphics.FillEllipse(b, Checked ? Width - diameter - 5 : 5, 6, diameter, diameter);
            if (Focused && ShowFocusCues) ControlPaint.DrawFocusRectangle(e.Graphics, ClientRectangle, p.Text, Parent.BackColor);
        }
    }
    internal class TilePanel : Panel
    {
        internal bool Selected;
        internal int CornerRadius = 13;
        internal TilePanel() { DoubleBuffered = true; ResizeRedraw = true; }
        protected override void OnPaintBackground(PaintEventArgs e)
        {
            Palette p = Palette.Current; e.Graphics.Clear(Parent == null ? p.Background : Parent.BackColor);
            Art.Box(e.Graphics, ClientRectangle, p.Surface, Selected ? p.Accent : p.Border, CornerRadius);
        }
    }

    internal sealed class SourceCard : TilePanel
    {
        internal Device Device;
        internal readonly MeterBar Meter = new MeterBar();
        internal bool Editing { get { return editing; } }
        readonly AppController app;
        readonly RenameButton name = new RenameButton();
        readonly TextBox editor = new TextBox();
        readonly ActionButton use = new ActionButton(), shortcut = new ActionButton();
        readonly Badge current = new Badge();
        readonly CheckBox cycle = new CycleCheck();
        readonly ToolTip tip = new ToolTip();
        bool editing, finishing, updating;
        internal event Action EditFinished;
        internal SourceCard(AppController controller, Device device)
        {
            app = controller; Device = device; BackColor = Palette.Current.Surface;
            name.Font = new Font("Segoe UI Semibold", 12); name.AccessibleName = "Rename " + app.Settings.Display(device);
            name.Click += delegate { BeginRename(); }; name.KeyDown += delegate(object s, KeyEventArgs e) { if (e.KeyCode == Keys.F2) { BeginRename(); e.Handled = true; } };
            editor.Visible = false; editor.MaxLength = 80; editor.Font = name.Font; editor.BorderStyle = BorderStyle.FixedSingle;
            editor.KeyDown += delegate(object s, KeyEventArgs e)
            { if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Escape) { e.SuppressKeyPress = true; FinishRename(e.KeyCode == Keys.Enter); } };
            editor.Leave += delegate { FinishRename(true); };
            use.Text = "Use"; use.AccessibleName = "Switch to " + app.Settings.Display(device);
            use.Click += delegate { app.Run(delegate { app.Switch(Device.Id); }); };
            shortcut.Click += delegate { using (ShortcutDialog dialog = new ShortcutDialog(app, Device.Id, Device.Flow)) dialog.ShowDialog(FindForm()); };
            cycle.AutoSize = false; cycle.Text = "Cycle"; cycle.Cursor = Cursors.Hand;
            cycle.CheckedChanged += delegate { if (!updating) app.Change(s => s.For(Device).Cycle = cycle.Checked); };
            Meter.AccessibleName = app.Settings.Display(device) + " audio activity";
            Controls.AddRange(new Control[] { name, editor, use, current, Meter, cycle, shortcut });
            UpdateDevice(device);
        }
        internal void UpdateDevice(Device d)
        {
            Device = d; updating = true; Palette p = Palette.Current; Binding b = app.Settings.For(d);
            Selected = d.Current; BackColor = p.Surface;
            AccessibleName = app.Settings.Display(d) + " source"; AccessibleRole = AccessibleRole.Grouping;
            name.Text = app.Settings.Display(d); name.BackColor = p.Surface;
            name.AccessibleName = "Rename " + name.Text;
            tip.SetToolTip(name, "Rename this source.");
            tip.SetToolTip(use, d.Flow == 1 ? "Use this microphone." : "Play sound through this source.");
            tip.SetToolTip(current, "This source is selected."); tip.SetToolTip(Meter, "Live sound activity.");
            editor.BackColor = p.Field; editor.ForeColor = p.Text;
            use.Enabled = d.Online; use.Visible = !d.Current;
            use.Text = d.Online ? "Use" : "Offline";
            current.Text = "Current"; current.Visible = d.Current;
            cycle.Checked = b.Cycle; cycle.ForeColor = p.Text; cycle.BackColor = p.Surface;
            cycle.AccessibleName = "Include " + name.Text + " in cycle";
            shortcut.Text = KeyText.Label(b.Key, b.Mods);
            string problem;
            if (app.Keys.Problems.TryGetValue(d.Id, out problem)) { shortcut.Text = "! " + shortcut.Text; tip.SetToolTip(shortcut, problem); }
            else tip.SetToolTip(shortcut, "Set a shortcut to select this source.");
            tip.SetToolTip(cycle, "Include this source when cycling.");
            Meter.Visible = app.Settings.Meters; if (!d.Online) Meter.Reset();
            Meter.AccessibleName = name.Text + " audio activity";
            updating = false; PerformLayout(); Invalidate(true);
        }
        internal void BeginRename()
        {
            if (Editing) return;
            editing = true;
            editor.Text = app.Settings.Display(Device); editor.Bounds = name.Bounds; name.Visible = false;
            editor.Visible = true; editor.BringToFront(); editor.Focus(); editor.SelectAll();
        }
        internal void FinishRename(bool save)
        {
            if (!Editing || finishing) return; finishing = true;
            string entered = editor.Text.Trim();
            editing = false; editor.Visible = false; name.Visible = true;
            if (save) app.Change(s => s.For(Device).Alias = entered == Device.Name ? "" : entered);
            finishing = false;
            // Saving may rebuild the list after a device arrival and dispose this card.
            if (IsDisposed || Disposing) return;
            UpdateDevice(Device);
            if (EditFinished != null) EditFinished();
        }
        protected override void OnLayout(LayoutEventArgs e)
        {
            base.OnLayout(e);
            Func<int, int> s = value => (int)Math.Round(value * DeviceDpi / 96.0);
            int titleY = s(17), left = s(68), right = Width - s(18);
            use.SetBounds(right - s(68), titleY, s(68), s(31)); current.SetBounds(right - s(70), titleY + s(3), s(70), s(25));
            name.SetBounds(left - s(8), titleY - s(2), Math.Max(s(80), right - left - s(76)), s(32));
            if (editor.Visible) editor.Bounds = name.Bounds;
            Meter.SetBounds(left, titleY + s(42), Math.Max(s(60), right - left), s(3));
            int bottom = Height - s(47);
            cycle.SetBounds(left, bottom + s(6), s(80), s(24));
            shortcut.SetBounds(left + s(92), bottom, Math.Max(s(110), right - left - s(92)), s(34));
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            int kind = Device.Flow == 1 ? 1 : Device.IsHeadphones ? 0 : Device.FormFactor == 9 ? 2 : 3;
            float scale = DeviceDpi / 96f;
            Art.Icon(e.Graphics, new Rectangle((int)(19 * scale), (int)(24 * scale), (int)(32 * scale), (int)(32 * scale)), kind, Device.Online ? Palette.Current.Text : Palette.Current.Muted);
        }
        protected override void Dispose(bool disposing) { if (disposing) tip.Dispose(); base.Dispose(disposing); }
    }
}
