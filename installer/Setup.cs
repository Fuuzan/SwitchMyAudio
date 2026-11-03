using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Principal;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

#if INSTALLER
[assembly: System.Reflection.AssemblyTitle("Switch My Audio Setup")]
[assembly: System.Reflection.AssemblyProduct("Switch My Audio")]
[assembly: System.Reflection.AssemblyCopyright("Copyright (c) 2026 Fuuzan")]
[assembly: System.Reflection.AssemblyVersion(SwitchMyAudio.BuildInfo.Version)]
[assembly: System.Reflection.AssemblyFileVersion(SwitchMyAudio.BuildInfo.Version)]
[assembly: System.Runtime.Versioning.TargetFramework(".NETFramework,Version=v4.8")]
#endif

internal static class InstallFiles
{
    internal static readonly string[] Names = { "SwitchMyAudio.exe", "SwitchMyAudio.exe.config", "LICENSE" };
    // Stage all payloads before touching the installed version. Keep rollback
    // copies until both replacements succeed; preferences live elsewhere.
    internal static void Apply(string directory, Func<string, byte[]> payload)
    {
        Directory.CreateDirectory(directory);
        string transaction = Guid.NewGuid().ToString("N");
        var stages = new Dictionary<string, string>();
        var backups = new Dictionary<string, string>();
        var applied = new List<string>();
        bool committed = false;
        try
        {
            foreach (string name in Names)
            {
                string stage = Path.Combine(directory, name + "." + transaction + ".new");
                stages[name] = stage; File.WriteAllBytes(stage, payload(name));
            }
            foreach (string name in Names)
            {
                string target = Path.Combine(directory, name);
                if (File.Exists(target))
                {
                    string backup = target + "." + transaction + ".old";
                    File.Replace(stages[name], target, backup); backups[name] = backup;
                }
                else File.Move(stages[name], target);
                applied.Add(name);
            }
            committed = true;
        }
        catch
        {
            foreach (string name in applied.AsEnumerable().Reverse())
            {
                string target = Path.Combine(directory, name), backup;
                if (backups.TryGetValue(name, out backup)) { File.Copy(backup, target, true); File.Delete(backup); backups.Remove(name); }
                else File.Delete(target);
            }
            throw;
        }
        finally
        {
            foreach (string stage in stages.Values) if (File.Exists(stage)) File.Delete(stage);
            if (committed) foreach (string backup in backups.Values) if (File.Exists(backup)) File.Delete(backup);
        }
    }
}

internal static class Setup
{
    const string Product = "Switch My Audio", UninstallKey = "Software\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\SwitchMyAudio";
    const string RunKey = "Software\\Microsoft\\Windows\\CurrentVersion\\Run";
    static readonly string Folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "SwitchMyAudio");
    static string AppFile { get { return Path.Combine(Folder, "SwitchMyAudio.exe"); } }
    static string Shortcut { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "Switch My Audio.lnk"); } }
    [STAThread] static int Main(string[] args)
    {
        Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
        bool remove = args.Contains("--uninstall");
        try
        {
            if (remove && string.Equals(Application.ExecutablePath, Path.Combine(Folder, "Uninstall.exe"), StringComparison.OrdinalIgnoreCase))
            {
                string copy = Path.Combine(Path.GetTempPath(), "SwitchMyAudio-Uninstall-" + Guid.NewGuid().ToString("N") + ".exe");
                File.Copy(Application.ExecutablePath, copy); Process.Start(copy, "--uninstall"); return 0;
            }
            bool created;
            using (Mutex installerMutex = new Mutex(true, "Local\\SwitchMyAudio.Setup-" + WindowsIdentity.GetCurrent().User.Value, out created))
            {
            if (!created) { MessageBox.Show("Setup is already open.", Product); return 0; }
            using (Form form = new Form { Text = Product + (remove ? " — Uninstall" : " — Setup"), ClientSize = new Size(440, 220),
                StartPosition = FormStartPosition.CenterScreen, FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, MinimizeBox = false,
                BackColor = Color.FromArgb(29,33,36), ForeColor = Color.FromArgb(241,245,246), Font = new Font("Segoe UI", 10) })
            {
                try { form.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
                Label title = new Label { Text = Product, Font = new Font("Segoe UI Semibold", 19), Bounds = new Rectangle(24, 20, 390, 40) };
                Label help = new Label { Text = remove ? "Remove the app and its shortcuts. Your saved settings stay on this computer." : "Install for your Windows account. Updates keep your shortcuts and saved settings.", Bounds = new Rectangle(24, 74, 390, 60) };
                Button action = new Button { Text = remove ? "Uninstall" : File.Exists(AppFile) ? "Update" : "Install", Bounds = new Rectangle(286, 158, 130, 38),
                    BackColor = Color.FromArgb(157,232,204), ForeColor = Color.FromArgb(17,45,36), FlatStyle = FlatStyle.Flat };
                bool done = false;
                action.Click += delegate
                {
                    if (done) { if (!remove) Process.Start(AppFile); form.Close(); return; }
                    action.Enabled = false;
                    try
                    {
                        StopApp();
                        if (remove) { Remove(); help.Text = "The app was removed. Your settings were kept."; }
                        else { Install(); help.Text = "Ready. Open the app now or use its Start menu shortcut."; }
                        done = true; action.Text = remove ? "Done" : "Open app"; form.AcceptButton = action;
                    }
                    catch (Exception ex) { MessageBox.Show(form, ex.Message, Product, MessageBoxButtons.OK, MessageBoxIcon.Warning); }
                    finally { action.Enabled = true; }
                };
                form.Controls.AddRange(new Control[] { title, help, action }); Application.Run(form);
            }
            }
            return 0;
        }
        catch (Exception ex) { MessageBox.Show(ex.Message, Product, MessageBoxButtons.OK, MessageBoxIcon.Error); return 1; }
    }
    static void StopApp()
    {
            foreach (Process legacy in Process.GetProcessesByName("QuietSwitch"))
        using (legacy)
            if (!legacy.HasExited && legacy.SessionId == Process.GetCurrentProcess().SessionId)
                throw new IOException("Quit the earlier QuietSwitch app from its tray menu, then try again.");
        Process[] processes = Process.GetProcessesByName("SwitchMyAudio");
        foreach (Process process in processes)
        using (process)
        {
            if (process.HasExited) continue;
            if (process.SessionId != Process.GetCurrentProcess().SessionId) continue;
            string path = process.MainModule.FileName;
            if (!string.Equals(path, AppFile, StringComparison.OrdinalIgnoreCase)) throw new IOException("Quit the portable copy of Switch My Audio, then try again.");
            using (Process exit = Process.Start(new ProcessStartInfo(AppFile, "--exit") { UseShellExecute = false, CreateNoWindow = true })) exit.WaitForExit(5000);
            if (!process.WaitForExit(5000)) throw new IOException("Close Switch My Audio from its tray menu, then try again.");
        }
    }
    static byte[] Payload(string name)
    {
        using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("payload." + name))
        using (MemoryStream bytes = new MemoryStream()) { if (stream == null) throw new IOException("The installer is incomplete."); stream.CopyTo(bytes); return bytes.ToArray(); }
    }
    static void Install()
    {
        InstallFiles.Apply(Folder, Payload);
        string uninstall = Path.Combine(Folder, "Uninstall.exe");
        if (!string.Equals(Application.ExecutablePath, uninstall, StringComparison.OrdinalIgnoreCase)) File.Copy(Application.ExecutablePath, uninstall, true);
        Type shellType = Type.GetTypeFromProgID("WScript.Shell");
        Directory.CreateDirectory(Path.GetDirectoryName(Shortcut));
        object shell = Activator.CreateInstance(shellType), link = null;
        try
        {
            link = shellType.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { Shortcut });
            Type type = link.GetType();
            type.InvokeMember("TargetPath", BindingFlags.SetProperty, null, link, new object[] { AppFile });
            type.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, link, new object[] { Folder });
            type.InvokeMember("IconLocation", BindingFlags.SetProperty, null, link, new object[] { AppFile + ",0" });
            type.InvokeMember("Save", BindingFlags.InvokeMethod, null, link, null);
        }
        finally { if (link != null) System.Runtime.InteropServices.Marshal.FinalReleaseComObject(link); System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shell); }
        using (RegistryKey key = Registry.CurrentUser.CreateSubKey(UninstallKey))
        {
            key.SetValue("DisplayName", Product); key.SetValue("DisplayVersion", FileVersionInfo.GetVersionInfo(AppFile).ProductVersion);
            key.SetValue("InstallLocation", Folder); key.SetValue("DisplayIcon", AppFile);
            key.SetValue("UninstallString", "\"" + uninstall + "\" --uninstall"); key.SetValue("NoModify", 1); key.SetValue("NoRepair", 1);
        }
        using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKey, true))
        {
            if (key != null && (key.GetValue("SwitchMyAudio") != null || key.GetValue("QuietSwitch") != null))
            { key.SetValue("SwitchMyAudio", "\"" + AppFile + "\" --silent"); key.DeleteValue("QuietSwitch", false); }
        }
    }
    static void Remove()
    {
        using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKey, true))
            if (key != null && string.Equals(key.GetValue("SwitchMyAudio") as string, "\"" + AppFile + "\" --silent", StringComparison.OrdinalIgnoreCase)) key.DeleteValue("SwitchMyAudio", false);
        Registry.CurrentUser.DeleteSubKeyTree(UninstallKey, false);
        if (File.Exists(Shortcut)) File.Delete(Shortcut);
        foreach (string name in InstallFiles.Names.Concat(new[] { "Uninstall.exe" })) { string path = Path.Combine(Folder, name); if (File.Exists(path)) File.Delete(path); }
        if (Directory.Exists(Folder) && Directory.GetFileSystemEntries(Folder).Length == 0) Directory.Delete(Folder);
    }
}
