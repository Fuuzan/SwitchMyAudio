using System;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using SwitchMyAudio;

internal static partial class TestRunner
{
    static void FirstRunDefaults(string folder)
    {
        string root = Path.Combine(folder, "first-run-" + Guid.NewGuid().ToString("N"));
        SettingsStore store = new SettingsStore(Path.Combine(root, "settings.xml"));
        Settings settings = store.Load();
        Assert(store.IsNew && settings.Theme == "System" && !settings.Meters && !settings.AutoHeadphones,
            "First launch follows the system theme with meters and automatic switching disabled");
        int enables = 0;
        Startup.InitializeNewUser(store, settings, delegate { enables++; });
        Startup.InitializeNewUser(store, settings, delegate { enables++; });
        Assert(enables == 1 && File.Exists(store.PathName) && !store.IsNew,
            "First launch persists defaults and requests Windows startup only once");
        settings.Theme = "Dark"; settings.Meters = true; settings.AutoHeadphones = true; store.Save(settings);
        settings = store.Load();
        Startup.InitializeNewUser(store, settings, delegate { enables++; });
        Assert(enables == 1 && !store.IsNew && settings.Theme == "Dark" && settings.Meters && settings.AutoHeadphones,
            "Existing preferences and a later startup opt-out survive future launches");

        SettingsStore denied = new SettingsStore(Path.Combine(root, "denied-startup.xml"));
        Settings defaults = denied.Load(); int attempts = 0; bool reported = false;
        try { Startup.InitializeNewUser(denied, defaults, delegate { attempts++; throw new InvalidOperationException("Test registration failure"); }); }
        catch (InvalidOperationException) { reported = true; }
        Startup.InitializeNewUser(denied, defaults, delegate { attempts++; });
        Assert(reported && attempts == 1 && File.Exists(denied.PathName),
            "A startup registration failure is reported without an automatic retry on every launch");

        string blocked = Path.Combine(root, "blocked"); File.WriteAllText(blocked, "not a folder");
        SettingsStore unwritable = new SettingsStore(Path.Combine(blocked, "settings.xml"));
        defaults = unwritable.Load(); bool registered = false, rejected = false;
        try { Startup.InitializeNewUser(unwritable, defaults, delegate { registered = true; }); }
        catch (IOException) { rejected = true; }
        Assert(rejected && !registered, "Startup is not registered when first-run preferences cannot be saved");

        File.WriteAllText(store.PathName, "damaged"); store.Load();
        Assert(!store.IsNew, "Recovered or damaged existing preferences are never treated as a new installation");
        using (AppController demo = new AppController(Path.Combine(root, "demo.xml"), true))
        using (SettingsWindow window = new SettingsWindow(demo))
        {
            Toggle[] toggles = Descendants(window).OfType<Toggle>().ToArray();
            Assert(toggles.First(t => t.AccessibleName == "Start with Windows").Checked &&
                !toggles.First(t => t.AccessibleName == "Show level meters").Checked &&
                !toggles.First(t => t.AccessibleName == "Auto-switch headphones").Checked &&
                Descendants(window).OfType<ActionButton>().Any(b => b.Text == "System" && b.Selected),
                "New-user Settings show startup on, System theme, meters off and auto-switch off");
            Assert(!File.Exists(demo.Store.PathName), "The demo preview does not initialize or save real first-run settings");
        }
    }
}
