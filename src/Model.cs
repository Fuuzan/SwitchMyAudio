using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Serialization;
using Microsoft.Win32;

namespace SwitchMyAudio
{
    public class Binding
    {
        public string Id = "", Name = "", Alias = "";
        public int Flow, Order;
        public uint Key, Mods;
        public bool Cycle, Hidden;
    }

    // Field names from the first release are retained for settings migration.
    public class Settings
    {
        public int Version = 5;
        public List<Binding> Devices = new List<Binding>();
        public uint OutputKey, OutputMods, InputKey, InputMods;
        public string Theme = "System";
        public bool Meters, AutoHeadphones;
        public int PinnedFlow;
        public bool Pinned;
        public int WindowX = int.MinValue, WindowY, WindowWidth = 660, WindowHeight = 740;
        public int PinX = int.MinValue, PinY;
        public int PinWidth = 256, PinHeight = 208, PinBothWidth = 520, PinBothHeight = 240;

        public Binding For(Device d)
        {
            Binding b = Devices.FirstOrDefault(x => x.Id == d.Id);
            if (b == null)
            {
                b = new Binding { Id = d.Id, Name = d.Name, Flow = d.Flow,
                    Order = Devices.Where(x => x.Flow == d.Flow).Select(x => x.Order).DefaultIfEmpty(-1).Max() + 1 };
                Devices.Add(b);
            }
            b.Name = d.Name;
            return b;
        }
        public string Display(Device d)
        {
            Binding b = Devices.FirstOrDefault(x => x.Id == d.Id);
            return b != null && !string.IsNullOrWhiteSpace(b.Alias) ? b.Alias : d.Name;
        }
        public List<Device> Ordered(IEnumerable<Device> source, int flow, bool includeHidden)
        {
            return source.Where(d => d.Flow == flow && (includeHidden || !For(d).Hidden))
                .OrderBy(d => For(d).Order).ThenBy(d => d.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
        }
        public Settings Copy()
        {
            using (MemoryStream s = new MemoryStream())
            {
                XmlSerializer xml = new XmlSerializer(typeof(Settings));
                xml.Serialize(s, this); s.Position = 0;
                return (Settings)xml.Deserialize(s);
            }
        }
    }

    public class Device
    {
        public string Id, Name, HardwareGroup = "";
        public int Flow, FormFactor = -1;
        public bool Online, Current, HeadphoneCategory;
        public int HeadphoneRank = 1;
        public bool IsHeadphones { get { return Flow == 0 && (FormFactor == 3 || FormFactor == 5 || HeadphoneCategory); } }
    }

    internal sealed class SettingsStore
    {
        internal readonly string PathName;
        internal string Notice;
        internal bool IsNew;
        internal static string DefaultPath
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SwitchMyAudio", "settings.xml"); }
        }
        internal SettingsStore(string path) { PathName = path; }
        internal static Settings Read(string path)
        {
            XmlReaderSettings safe = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null,
                MaxCharactersInDocument = 8 * 1024 * 1024 };
            using (XmlReader reader = XmlReader.Create(path, safe))
            {
                Settings s = (Settings)new XmlSerializer(typeof(Settings)).Deserialize(reader);
                Validate(s);
                return s;
            }
        }
        internal static void Validate(Settings s)
        {
            if (s == null || s.Devices == null || s.Devices.Count > 2048) throw new InvalidDataException("Invalid settings file.");
            if (s.Devices.Any(b => b == null || string.IsNullOrWhiteSpace(b.Id) || b.Flow < 0 || b.Flow > 1))
                throw new InvalidDataException("Invalid device settings.");
            if (s.Devices.GroupBy(b => b.Id).Any(g => g.Count() > 1)) throw new InvalidDataException("Duplicate device IDs.");
            foreach (Binding b in s.Devices)
            {
                b.Name = b.Name ?? "Audio device"; b.Alias = b.Alias ?? "";
                if (b.Alias.Length > 80 || b.Key > 254 || (b.Mods & ~15u) != 0) throw new InvalidDataException("Invalid device name or shortcut.");
            }
            if (s.OutputKey > 254 || s.InputKey > 254 || ((s.OutputMods | s.InputMods) & ~15u) != 0)
                throw new InvalidDataException("Invalid cycle shortcut.");
            if (s.Theme != "Light" && s.Theme != "Dark" && s.Theme != "System") s.Theme = "System";
            if (s.PinnedFlow < 0 || s.PinnedFlow > 2) s.PinnedFlow = 0;
            s.WindowWidth = Math.Max(440, Math.Min(1600, s.WindowWidth));
            s.WindowHeight = Math.Max(440, Math.Min(1400, s.WindowHeight));
            s.PinWidth = Math.Max(176, Math.Min(1800, s.PinWidth));
            s.PinHeight = Math.Max(116, Math.Min(1400, s.PinHeight));
            s.PinBothWidth = Math.Max(336, Math.Min(2200, s.PinBothWidth));
            s.PinBothHeight = Math.Max(116, Math.Min(1400, s.PinBothHeight));
            s.Version = 5;
        }
        internal Settings Load()
        {
            IsNew = false;
            if (!File.Exists(PathName) && string.Equals(PathName, DefaultPath, StringComparison.OrdinalIgnoreCase))
            {
                string legacy = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "QuietSwitch", "settings.xml");
                if (File.Exists(legacy))
                {
                    try { Settings migrated = Read(legacy); Save(migrated); return migrated; }
                    catch { Notice = "Earlier settings could not be copied. The original is unchanged."; }
                }
            }
            if (!File.Exists(PathName))
            {
                IsNew = Notice == null && !File.Exists(PathName + ".bak");
                return new Settings();
            }
            try { return Read(PathName); }
            catch (Exception)
            {
                string damaged = PathName + ".damaged-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff");
                try { File.Copy(PathName, damaged, false); } catch { }
                try
                {
                    Settings backup = Read(PathName + ".bak");
                    // Repair the primary so the next save does not replace the valid
                    // backup with the damaged file we just recovered from.
                    try { File.Copy(PathName + ".bak", PathName, true); } catch { }
                    Notice = "Settings recovered from the previous backup.";
                    return backup;
                }
                catch { Notice = "Saved settings were unreadable. A copy was kept; defaults are loaded."; return new Settings(); }
            }
        }
        internal void Save(Settings s)
        {
            Validate(s);
            Directory.CreateDirectory(Path.GetDirectoryName(PathName));
            string temp = PathName + ".tmp";
            using (FileStream stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
            { new XmlSerializer(typeof(Settings)).Serialize(stream, s); stream.Flush(true); }
            if (File.Exists(PathName)) File.Replace(temp, PathName, PathName + ".bak");
            else File.Move(temp, PathName);
        }
    }

    internal static class Startup
    {
        const string RunKey = "Software\\Microsoft\\Windows\\CurrentVersion\\Run";
        internal static void InitializeNewUser(SettingsStore store, Settings settings, Action enable)
        {
            if (!store.IsNew) return;
            // Save first so a later startup opt-out is not reset on the next launch.
            store.Save(settings); store.IsNew = false; enable();
        }
        internal static bool Enabled(string executable)
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKey))
                return key != null && string.Equals(key.GetValue("SwitchMyAudio") as string,
                    "\"" + executable + "\" --silent", StringComparison.OrdinalIgnoreCase);
        }
        internal static void Set(bool enabled, string executable)
        {
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(RunKey))
            {
                if (enabled) key.SetValue("SwitchMyAudio", "\"" + executable + "\" --silent");
                else key.DeleteValue("SwitchMyAudio", false);
                key.DeleteValue("QuietSwitch", false);
            }
        }
    }

    internal static class DeviceRules
    {
        internal static Device Next(List<Device> all, Settings s, int flow)
        {
            List<Device> list = s.Ordered(all, flow, false).Where(d => d.Online && s.For(d).Cycle).ToList();
            if (list.Count < 2) throw new InvalidOperationException("Select at least two connected sources for this cycle.");
            int i = list.FindIndex(d => d.Current);
            return list[(i + 1) % list.Count];
        }
        internal static Device ArrivingHeadphones(List<Device> current, HashSet<string> previous)
        {
            List<Device> candidates = new List<Device>();
            foreach (Device arrived in current.Where(d => d.Online && d.IsHeadphones && !previous.Contains(d.Id)))
            {
                Device preferred = PreferredHeadphone(current, arrived);
                if (preferred.Id != arrived.Id && previous.Contains(preferred.Id) && preferred.Current) continue;
                candidates.Add(preferred);
            }
            return candidates.OrderBy(HeadphonePriority).ThenBy(d => d.Id, StringComparer.Ordinal).FirstOrDefault();
        }
        internal static Device PreferredHeadphone(List<Device> current, Device candidate)
        {
            if (string.IsNullOrEmpty(candidate.HardwareGroup)) return candidate;
            return current.Where(d => d.Online && d.IsHeadphones && string.Equals(d.HardwareGroup, candidate.HardwareGroup, StringComparison.OrdinalIgnoreCase))
                .OrderBy(HeadphonePriority).ThenBy(d => d.Id, StringComparer.Ordinal).FirstOrDefault() ?? candidate;
        }
        internal static int HeadphonePriority(Device d) { return d.FormFactor == 3 ? 0 : d.FormFactor == 5 ? 2 : d.HeadphoneRank; }
        internal static bool KnownHeadphoneHardware(IEnumerable<string> hardwareIds, out int rank)
        {
            // Verified from the Maxwell receiver's USB hardware descriptors. No personal endpoint IDs or aliases.
            rank = 1; bool found = false;
            foreach (string id in hardwareIds)
            {
                const string product = "USB\\VID_3329&PID_4B19";
                if (!id.StartsWith(product, StringComparison.OrdinalIgnoreCase) || (id.Length > product.Length && id[product.Length] != '&')) continue;
                found = true;
                if (id.IndexOf("&MI_04", StringComparison.OrdinalIgnoreCase) >= 0) { rank = 0; return true; }
                if (id.IndexOf("&MI_01", StringComparison.OrdinalIgnoreCase) >= 0) rank = 3;
            }
            return found;
        }
        internal static bool HeadphoneCategories(IEnumerable<string> categories)
        { return categories.Any(c => string.Equals(c, "Audio.Headphone", StringComparison.OrdinalIgnoreCase) || string.Equals(c, "Communication.Headset", StringComparison.OrdinalIgnoreCase) || string.Equals(c, "Communication.Headset.Bluetooth", StringComparison.OrdinalIgnoreCase)); }
    }
}
