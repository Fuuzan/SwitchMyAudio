using System;
using System.Drawing;
using System.Windows.Forms;

namespace SwitchMyAudio
{
    internal sealed class ShortcutDialog : ThemedForm
    {
        readonly AppController app;
        readonly string target;
        readonly int flow;
        readonly Label hint;
        uint pendingKey, pendingMods;
        bool closing, capturing;
        internal ShortcutDialog(AppController controller, string id, int dataFlow)
        {
            app = controller; target = id; flow = dataFlow;
            Text = "Assign shortcut"; Font = new Font("Segoe UI", 10); AutoScaleMode = AutoScaleMode.Dpi;
            ClientSize = new Size(430, 240); FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = MinimizeBox = false; StartPosition = FormStartPosition.CenterParent;
            Palette.Window(this); KeyPreview = true;
            Label title = new Label { Text = "Press your shortcut", Font = new Font("Segoe UI Semibold", 17), AutoSize = true, Location = new Point(24, 22) };
            hint = new Label { Text = "Press a key or a key combination.\nEsc cancels. Delete clears. F12 is unavailable.", Location = new Point(26, 70), Size = new Size(380, 58), ForeColor = Palette.Current.Muted };
            ActionButton clear = new ActionButton { Text = "Clear shortcut", Location = new Point(24, 166), Size = new Size(140, 36) };
            ActionButton cancel = new ActionButton { Text = "Cancel", Location = new Point(296, 166), Size = new Size(110, 36) };
            clear.Click += delegate { Save(0, 0); }; cancel.Click += delegate { Close(); };
            Controls.AddRange(new Control[] { title, hint, clear, cancel });
            FormClosed += delegate { EndCapture(); };
        }
        protected override void OnShown(EventArgs e) { capturing = true; app.Keys.Suspend(); base.OnShown(e); }
        void EndCapture() { if (!capturing) return; capturing = false; app.RegisterKeys(); app.Notify(); }
        protected override void Dispose(bool disposing) { if (disposing) EndCapture(); base.Dispose(disposing); }
        protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); Palette.Window(this); }
        void Save(uint key, uint mods)
        {
            if (closing) return;
            string error = app.Demo ? null : app.Keys.Validate(app.Settings, target, key, mods);
            if (error != null) { hint.Text = error; hint.ForeColor = Palette.Current.Error; return; }
            bool saved = app.Change(delegate(Settings s)
            {
                if (target == "cycle0") { s.OutputKey = key; s.OutputMods = mods; }
                else if (target == "cycle1") { s.InputKey = key; s.InputMods = mods; }
                else
                {
                    Binding b = s.Devices.Find(d => d.Id == target);
                    if (b == null) throw new InvalidOperationException("Source no longer exists.");
                    b.Key = key; b.Mods = mods;
                }
            });
            if (saved) { closing = true; DialogResult = DialogResult.OK; Close(); }
            else { hint.Text = "The shortcut could not be saved. Check the main window for details."; hint.ForeColor = Palette.Current.Error; }
        }
        protected override bool ProcessCmdKey(ref Message msg, Keys data)
        {
            uint key = (uint)(data & Keys.KeyCode);
            if (key == 27 && (data & Keys.Modifiers) == 0) { Close(); return true; }
            if (key == 46 && (data & Keys.Modifiers) == 0) { Save(0, 0); return true; }
            if (key == 16 || key == 17 || key == 18 || key == 91 || key == 92) return true;
            if (key == 123) { hint.Text = "F12 is reserved by Windows. Choose another key."; return true; }
            pendingKey = key;
            pendingMods = ((data & Keys.Control) != 0 ? 2u : 0) | ((data & Keys.Alt) != 0 ? 1u : 0) |
                ((data & Keys.Shift) != 0 ? 4u : 0) | ((Win32.GetKeyState(91) & 0x8000) != 0 || (Win32.GetKeyState(92) & 0x8000) != 0 ? 8u : 0);
            Save(pendingKey, pendingMods); return true;
        }
    }
}
