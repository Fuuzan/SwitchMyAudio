using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Windows.Forms;

namespace SwitchMyAudio
{
    // Batch only visible surfaces. WM_SETREDRAW must never be sent to a hidden
    // control: reenabling it would also set WS_VISIBLE on that control.
    internal sealed class PaintBatch : IDisposable
    {
        sealed class State { internal int Depth; internal bool Redraw; }
        static readonly ConditionalWeakTable<Control, State> states = new ConditionalWeakTable<Control, State>();
        readonly Control control;
        readonly State state;
        bool finished;
        internal PaintBatch(Control target)
        {
            control = target; state = states.GetOrCreateValue(target); control.SuspendLayout();
            if (state.Depth++ == 0)
            {
                state.Redraw = target.IsHandleCreated && target.Visible;
                if (state.Redraw) Win32.SendMessage(target.Handle, 0xB, IntPtr.Zero, IntPtr.Zero);
            }
        }
        public void Dispose()
        {
            if (finished) return; finished = true;
            try { if (!control.IsDisposed) control.ResumeLayout(true); }
            finally
            {
                if (--state.Depth == 0 && state.Redraw && !control.IsDisposed && control.IsHandleCreated)
                {
                    Win32.SendMessage(control.Handle, 0xB, new IntPtr(1), IntPtr.Zero);
                    Win32.RedrawWindow(control.Handle, IntPtr.Zero, IntPtr.Zero, 0x0585);
                }
            }
        }
    }

    internal class ThemedForm : Form
    {
        internal ThemedForm()
        {
            BackColor = Palette.Current.Background;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }
        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e); Palette.Window(this);
            int disable = 1;
            try { Win32.DwmSetWindowAttribute(Handle, 3, ref disable, 4); }
            catch (DllNotFoundException) { }
        }
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == 0x14 && m.WParam != IntPtr.Zero)
            {
                using (Graphics g = Graphics.FromHdc(m.WParam)) g.Clear(BackColor);
                m.Result = new IntPtr(1); return;
            }
            base.WndProc(ref m);
        }
        internal bool Cloak(bool hidden)
        {
            int value = hidden ? 1 : 0;
            try { return Win32.DwmSetWindowAttribute(Handle, 13, ref value, 4) >= 0; }
            catch (DllNotFoundException) { return false; }
        }
        internal void PaintBeforeReveal()
        {
            PerformLayout();
            Win32.RedrawWindow(Handle, IntPtr.Zero, IntPtr.Zero, 0x0585);
            try { Win32.DwmFlush(); } catch (DllNotFoundException) { }
        }
    }

    internal sealed class VerticalPanel : Panel
    {
        [StructLayout(LayoutKind.Sequential)] struct StyleChange { internal int OldStyle, NewStyle; }
        bool arranging;
        internal VerticalPanel()
        {
            AutoScroll = true;
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint |
                ControlStyles.ResizeRedraw, true);
        }
        // Layouts always fit the viewport width. Suppress the horizontal bar as
        // a second safeguard during native scrollbar/resize recalculation.
        protected override void OnLayout(LayoutEventArgs e)
        {
            base.OnLayout(e);
            if (arranging) return;
            arranging = true;
            try
            {
                if (AutoScrollPosition.X != 0) AutoScrollPosition = new Point(0, -AutoScrollPosition.Y);
                HScroll = false;
                HorizontalScroll.Enabled = false; HorizontalScroll.Visible = false;
                if (IsHandleCreated) Win32.ShowScrollBar(Handle, 0, false);
            }
            finally { arranging = false; }
        }
        protected override Point ScrollToControl(Control activeControl)
        { Point p = base.ScrollToControl(activeControl); return new Point(0, p.Y); }
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == 0x7C && m.WParam.ToInt64() == -16 && m.LParam != IntPtr.Zero)
            {
                StyleChange styles = (StyleChange)Marshal.PtrToStructure(m.LParam, typeof(StyleChange));
                styles.NewStyle &= ~0x00100000;
                Marshal.StructureToPtr(styles, m.LParam, false);
            }
            if (m.Msg == 0x114) { m.Result = IntPtr.Zero; return; }
            base.WndProc(ref m);
        }
    }
}
