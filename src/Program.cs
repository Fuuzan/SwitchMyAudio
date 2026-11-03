using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Security.Principal;
using System.Threading;
using System.Windows.Forms;

[assembly: System.Reflection.AssemblyTitle("Switch My Audio")]
[assembly: System.Reflection.AssemblyProduct("Switch My Audio")]
[assembly: System.Reflection.AssemblyCopyright("Copyright (c) 2026 Fuuzan")]
[assembly: System.Reflection.AssemblyVersion(SwitchMyAudio.BuildInfo.Version)]
[assembly: System.Reflection.AssemblyFileVersion(SwitchMyAudio.BuildInfo.Version)]
[assembly: System.Runtime.Versioning.TargetFramework(".NETFramework,Version=v4.8", FrameworkDisplayName = ".NET Framework 4.8")]

namespace SwitchMyAudio
{
    internal sealed class TrayContext : ApplicationContext
    {
        readonly AppController app;
        readonly MainWindow window;
        readonly NotifyIcon tray;
        Icon ownedIcon;
        bool disposed;
        internal TrayContext(AppController controller, bool silent)
        {
            app = controller; window = new MainWindow(app);
            app.Host.QuitRequested = delegate { window.Quit(); ExitThread(); };
            tray = new NotifyIcon { Text = app.Demo ? "Switch My Audio · preview" : "Switch My Audio", Visible = true };
            try { ownedIcon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); tray.Icon = ownedIcon; } catch { tray.Icon = SystemIcons.Application; }
            ContextMenuStrip menu = new ContextMenuStrip();
            menu.Items.Add("Open Switch My Audio", null, delegate { window.OpenFull(); });
            menu.Items.Add("Cycle playback", null, delegate { app.Run(delegate { app.Cycle(0); }); });
            menu.Items.Add("Cycle microphones", null, delegate { app.Run(delegate { app.Cycle(1); }); });
            menu.Items.Add("Settings", null, delegate { window.OpenFull(); window.OpenSettings(); });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Quit", null, delegate { window.Quit(); ExitThread(); });
            tray.ContextMenuStrip = menu;
            tray.MouseClick += delegate(object s, MouseEventArgs e) { if (e.Button == MouseButtons.Left) window.OpenFull(); };
            app.Feedback += Feedback;
            window.BeginInitialView(silent);
            if (!string.IsNullOrEmpty(app.LastError)) app.Host.Dispatch(delegate { Feedback(app.LastError, true); });
        }
        void Feedback(string text, bool error)
        {
            if (error)
                tray.ShowBalloonTip(error ? 5000 : 2000, "Switch My Audio", text, error ? ToolTipIcon.Warning : ToolTipIcon.Info);
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing && !disposed)
            {
                disposed = true; app.Feedback -= Feedback;
                tray.Visible = false; if (tray.ContextMenuStrip != null) tray.ContextMenuStrip.Dispose(); tray.Dispose();
                if (ownedIcon != null) ownedIcon.Dispose(); window.Dispose(); app.Dispose();
            }
            base.Dispose(disposing);
        }
    }
    internal static class Program
    {
        [STAThread] static int Main(string[] args)
        {
            Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
            string errorFile = null;
            try
            {
                if (args.Contains("--exit"))
                {
                    IntPtr running = Win32.FindWindow(null, MessageWindow.WindowName + (args.Contains("--demo") ? ".Preview" : ""));
                    if (running != IntPtr.Zero) Win32.PostMessage(running, MessageWindow.QuitMessage, IntPtr.Zero, IntPtr.Zero);
                    return 0;
                }
                if (args.Length == 2 && args[0] == "--render") { errorFile = Path.Combine(args[1], "render-error.txt"); Render(args[1]); return 0; }
                string config = SettingsStore.DefaultPath;
                int index = Array.IndexOf(args, "--config");
                if (index >= 0) { if (index + 1 >= args.Length) throw new ArgumentException("--config needs a file path."); config = Path.GetFullPath(args[index + 1]); }
                bool demo = args.Contains("--demo");
                if (!demo && Win32.FindWindow(null, "QuietSwitch.Main1.MessageWindow") != IntPtr.Zero)
                {
                    MessageBox.Show("Quit the earlier QuietSwitch app from its tray menu, then open Switch My Audio again.", "Switch My Audio");
                    return 0;
                }
                if (demo) config = Path.Combine(Path.GetTempPath(), "SwitchMyAudio-unused-preview-settings.xml");
                bool created;
                string identity = "Local\\SwitchMyAudio-" + WindowsIdentity.GetCurrent().User.Value + (demo ? "-preview" : "");
                using (Mutex mutex = new Mutex(true, identity, out created))
                {
                    if (!created)
                    {
                        IntPtr previous = Win32.FindWindow(null, MessageWindow.WindowName + (demo ? ".Preview" : ""));
                        if (previous != IntPtr.Zero) Win32.PostMessage(previous, MessageWindow.OpenMessage, IntPtr.Zero, IntPtr.Zero);
                        else MessageBox.Show("Switch My Audio is already running. Open it from its tray icon.", "Switch My Audio");
                        return 0;
                    }
                    using (AppController app = new AppController(config, demo))
                    {
                        if (demo) app.FillDemoBindings();
                        else if (string.Equals(config, SettingsStore.DefaultPath, StringComparison.OrdinalIgnoreCase))
                        {
                            try { Startup.InitializeNewUser(app.Store, app.Settings, delegate { Startup.Set(true, Application.ExecutablePath); }); }
                            catch (Exception ex) { app.LastError = "Start with Windows could not be enabled: " + ex.Message; }
                        }
                        using (TrayContext context = new TrayContext(app, args.Contains("--silent"))) Application.Run(context);
                    }
                }
                return 0;
            }
            catch (Exception ex)
            {
                if (errorFile != null) { Directory.CreateDirectory(Path.GetDirectoryName(errorFile)); File.WriteAllText(errorFile, ex.ToString()); }
                else MessageBox.Show("Switch My Audio could not start.\n\n" + ex.Message, "Switch My Audio", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return 1;
            }
        }
        static void Snapshot(Form form, string path)
        {
            form.Show(); Application.DoEvents();
            using (Bitmap bmp = new Bitmap(form.Width, form.Height)) { form.DrawToBitmap(bmp, new Rectangle(0, 0, form.Width, form.Height)); bmp.Save(path); }
        }
        static void Render(string folder)
        {
            Directory.CreateDirectory(folder);
            using (AppController app = new AppController(Path.Combine(folder, "unused-demo.xml"), true))
            {
                app.FillDemoBindings();
                using (MainWindow window = new MainWindow(app))
                {
                    window.Size = new Size(660, 760); window.SavePreview(Path.Combine(folder, "main-dark.png"));
                    window.Size = new Size(460, 620); window.SavePreview(Path.Combine(folder, "main-narrow.png"));
                    app.Settings.Theme = "Light"; window.SetFlow(1); window.Size = new Size(590, 720); window.SavePreview(Path.Combine(folder, "microphones-light.png"));
                    using (SettingsWindow settings = new SettingsWindow(app)) Snapshot(settings, Path.Combine(folder, "settings-light.png"));
                    app.Settings.Theme = "Dark"; window.RefreshView(); window.Pin();
                    Snapshot(window.PinnedWindow, Path.Combine(folder, "pinned-dark.png"));
                    app.Settings.PinnedFlow = 2; app.Notify();
                    Snapshot(window.PinnedWindow, Path.Combine(folder, "pinned-both-dark.png"));
                    window.PinnedWindow.Size = new Size(670, 290);
                    Snapshot(window.PinnedWindow, Path.Combine(folder, "pinned-both-resized.png"));
                    window.PinnedWindow.Size = new Size(336, 140);
                    Snapshot(window.PinnedWindow, Path.Combine(folder, "pinned-both-small.png"));
                    window.PinnedWindow.Size = window.PinnedWindow.MinimumSize;
                    Snapshot(window.PinnedWindow, Path.Combine(folder, "pinned-both-minimum.png"));
                    app.Settings.PinnedFlow = 0; app.Notify(); window.PinnedWindow.Size = new Size(176, 140);
                    Snapshot(window.PinnedWindow, Path.Combine(folder, "pinned-small.png"));
                    window.PinnedWindow.Size = window.PinnedWindow.MinimumSize;
                    Snapshot(window.PinnedWindow, Path.Combine(folder, "pinned-minimum.png"));
                    window.OpenFull();
                    using (SettingsWindow settings = new SettingsWindow(app))
                    {
                        Snapshot(settings, Path.Combine(folder, "settings-dark.png"));
                        settings.ShowPage(true); Snapshot(settings, Path.Combine(folder, "devices-dark.png"));
                        settings.Size = settings.MinimumSize; Snapshot(settings, Path.Combine(folder, "devices-narrow.png"));
                        app.Settings.Theme = "Light"; Palette.Set("Light"); settings.ShowPage(true); Snapshot(settings, Path.Combine(folder, "devices-light.png"));
                    }
                    window.Quit();
                }
            }
        }
    }
}
