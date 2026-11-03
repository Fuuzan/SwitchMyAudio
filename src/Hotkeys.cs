using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace SwitchMyAudio
{
    internal static class Win32
    {
        [DllImport("user32.dll", SetLastError = true)] internal static extern bool RegisterHotKey(IntPtr window, int id, uint mods, uint key);
        [DllImport("user32.dll")] internal static extern bool UnregisterHotKey(IntPtr window, int id);
        [DllImport("user32.dll")] internal static extern short GetKeyState(int key);
        [DllImport("user32.dll")] internal static extern int GetWindowLong(IntPtr window, int index);
        [DllImport("user32.dll")] internal static extern bool RedrawWindow(IntPtr window, IntPtr rect, IntPtr region, uint flags);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] internal static extern IntPtr FindWindow(string className, string title);
        [DllImport("user32.dll")] internal static extern bool PostMessage(IntPtr window, int message, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll")] internal static extern bool ReleaseCapture();
        [DllImport("user32.dll")] internal static extern IntPtr SendMessage(IntPtr window, int message, IntPtr wParam, IntPtr lParam);
        [DllImport("dwmapi.dll")] internal static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
        [DllImport("dwmapi.dll")] internal static extern int DwmFlush();
        [DllImport("user32.dll")] internal static extern bool ShowScrollBar(IntPtr window, int bar, bool show);
        [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)] internal static extern int SetWindowTheme(IntPtr window, string appName, string idList);
        [StructLayout(LayoutKind.Sequential)] internal struct PaintInfo
        {
            internal IntPtr Dc;
            internal int Erase, Left, Top, Right, Bottom, Restore, Incremental;
            internal int Reserved0, Reserved1, Reserved2, Reserved3, Reserved4, Reserved5, Reserved6, Reserved7;
        }
        [StructLayout(LayoutKind.Sequential)] internal struct ComboInfo
        {
            internal int Size;
            internal int ItemLeft, ItemTop, ItemRight, ItemBottom;
            internal int ButtonLeft, ButtonTop, ButtonRight, ButtonBottom, ButtonState;
            internal IntPtr Combo, Item, List;
        }
        [DllImport("user32.dll")] internal static extern IntPtr BeginPaint(IntPtr window, out PaintInfo info);
        [DllImport("user32.dll")] internal static extern bool EndPaint(IntPtr window, ref PaintInfo info);
        [DllImport("user32.dll")] internal static extern bool GetComboBoxInfo(IntPtr window, ref ComboInfo info);
    }

    internal sealed class MessageWindow : Form
    {
        internal const string WindowName = "SwitchMyAudio.Main1.MessageWindow";
        internal const int OpenMessage = 0x8001;
        internal const int QuitMessage = 0x8002;
        internal Action<int> Hotkey;
        internal Action OpenRequested;
        internal Action QuitRequested;
        internal MessageWindow(string suffix = "")
        {
            Text = WindowName + suffix; ShowInTaskbar = false;
            IntPtr unused = Handle;
        }
        internal void Dispatch(Action action)
        {
            if (IsDisposed) return;
            try { BeginInvoke(new Action(delegate { if (!IsDisposed) action(); })); } catch (InvalidOperationException) { }
        }
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == 0x312 && Hotkey != null) Hotkey(m.WParam.ToInt32());
            if (m.Msg == OpenMessage && OpenRequested != null) OpenRequested();
            if (m.Msg == QuitMessage && QuitRequested != null) Dispatch(QuitRequested);
            base.WndProc(ref m);
        }
    }

    internal static class KeyText
    {
        internal static string Name(uint key)
        {
            if (key >= 48 && key <= 57) return ((char)key).ToString();
            if (key >= 96 && key <= 105) return "Num " + (key - 96);
            switch ((Keys)key)
            {
                case Keys.Oemcomma: return ",";
                case Keys.OemPeriod: return ".";
                case Keys.OemQuestion: return "/";
                case Keys.OemSemicolon: return ";";
                case Keys.OemQuotes: return "'";
                case Keys.OemMinus: return "-";
                case Keys.Oemplus: return "=";
                case Keys.Prior: return "Page Up";
                case Keys.Next: return "Page Down";
                case Keys.Back: return "Backspace";
                case Keys.Return: return "Enter";
            }
            return ((Keys)key).ToString();
        }
        internal static string Label(uint key, uint mods)
        {
            if (key == 0) return "Set shortcut";
            return ((mods & 2) != 0 ? "Ctrl + " : "") + ((mods & 1) != 0 ? "Alt + " : "") +
                ((mods & 4) != 0 ? "Shift + " : "") + ((mods & 8) != 0 ? "Win + " : "") + Name(key);
        }
    }

    internal sealed class HotkeyManager : IDisposable
    {
        readonly MessageWindow host;
        readonly Dictionary<int, Action> actions = new Dictionary<int, Action>();
        internal readonly Dictionary<string, string> Problems = new Dictionary<string, string>();
        internal HotkeyManager(MessageWindow window)
        {
            host = window; host.Hotkey = delegate(int id) { Action action; if (actions.TryGetValue(id, out action)) action(); };
        }
        internal void Suspend()
        {
            foreach (int id in actions.Keys) Win32.UnregisterHotKey(host.Handle, id);
            actions.Clear();
        }
        internal void Register(Settings s, Action<string> device, Action<int> cycle)
        {
            Suspend(); Problems.Clear(); int id = 1;
            foreach (Binding b in s.Devices.Where(b => b.Key != 0))
            {
                string captured = b.Id;
                Add(id++, b.Id, b.Key, b.Mods, delegate { device(captured); });
            }
            Add(id++, "cycle0", s.OutputKey, s.OutputMods, delegate { cycle(0); });
            Add(id, "cycle1", s.InputKey, s.InputMods, delegate { cycle(1); });
        }
        void Add(int id, string name, uint key, uint mods, Action action)
        {
            if (key == 0) return;
            if (Win32.RegisterHotKey(host.Handle, id, mods | 0x4000, key)) actions[id] = action;
            else Problems[name] = KeyText.Label(key, mods) + " is used by Windows or another app.";
        }
        internal string Validate(Settings s, string edited, uint key, uint mods)
        {
            if (key == 0) return null;
            if (s.Devices.Any(b => b.Id != edited && b.Key == key && b.Mods == mods)) return "This shortcut is already assigned to a source.";
            if (edited != "cycle0" && s.OutputKey == key && s.OutputMods == mods) return "This shortcut is assigned to the playback cycle.";
            if (edited != "cycle1" && s.InputKey == key && s.InputMods == mods) return "This shortcut is assigned to the microphone cycle.";
            if (!Win32.RegisterHotKey(host.Handle, 9000, mods | 0x4000, key)) return "This shortcut is reserved or used by another app. Choose another.";
            Win32.UnregisterHotKey(host.Handle, 9000); return null;
        }
        public void Dispose() { Suspend(); host.Hotkey = null; }
    }
}
