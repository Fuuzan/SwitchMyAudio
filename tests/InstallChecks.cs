using System;
using System.IO;
using System.Linq;

internal static partial class TestRunner
{
    static void InstallChecks(string folder)
    {
        string target = Path.Combine(folder, "install-check"); Directory.CreateDirectory(target);
        File.WriteAllText(Path.Combine(target, "keep.txt"), "unrelated user file");
        InstallFiles.Apply(target, name => System.Text.Encoding.UTF8.GetBytes("old:" + name));
        Assert(InstallFiles.Names.All(name => File.ReadAllText(Path.Combine(target, name)) == "old:" + name), "Installer writes application, runtime config and license to the selected test directory");
        InstallFiles.Apply(target, name => System.Text.Encoding.UTF8.GetBytes("new:" + name));
        Assert(InstallFiles.Names.All(name => File.ReadAllText(Path.Combine(target, name)) == "new:" + name), "Updating replaces the application and its runtime config together");
        bool failed = false;
        using (FileStream locked = new FileStream(Path.Combine(target, InstallFiles.Names[1]), FileMode.Open, FileAccess.Read, FileShare.Read))
            try { InstallFiles.Apply(target, name => System.Text.Encoding.UTF8.GetBytes("broken:" + name)); } catch (IOException) { failed = true; }
        Assert(failed && InstallFiles.Names.All(name => File.ReadAllText(Path.Combine(target, name)) == "new:" + name), "A blocked second update file rolls the first file back to the previous working version");
        failed = false;
        try { InstallFiles.Apply(target, name => { if (name.EndsWith("config")) throw new IOException("test missing payload"); return new byte[] { 1 }; }); } catch (IOException) { failed = true; }
        Assert(failed && InstallFiles.Names.All(name => File.ReadAllText(Path.Combine(target, name)) == "new:" + name), "Incomplete installer payloads leave the installed version untouched");
        Assert(File.ReadAllText(Path.Combine(target, "keep.txt")) == "unrelated user file" && Directory.GetFiles(target).Length == InstallFiles.Names.Length + 1,
            "Installation preserves unrelated files and removes successful transaction staging files");
    }
}
