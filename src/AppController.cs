using System;
using System.Collections.Generic;
using System.Drawing;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Microsoft.Win32;
using System.Threading;
using Timer = System.Windows.Forms.Timer;

namespace SwitchMyAudio
{
    internal sealed class AppController : IDisposable
    {
        internal Settings Settings;
        internal readonly SettingsStore Store;
        internal readonly bool Demo;
        internal bool DemoStartup = true;
        internal List<Device> Devices = new List<Device>();
        internal readonly MessageWindow Host;
        internal readonly HotkeyManager Keys;
        internal event Action Changed;
        internal event Action<string, bool> Feedback;
        internal event Action OpenRequested;
        internal string LastError;
        internal int MeterVersion;
        IAudioBackend audio;
        readonly Func<Action, IAudioBackend> audioFactory;
        HashSet<string> previous = new HashSet<string>();
        HashSet<string> connectedGroups = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        readonly HashSet<string> manualGroups = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        readonly Dictionary<string, long> settlingInterfaces = new Dictionary<string, long>();
        readonly Stopwatch connectionClock = Stopwatch.StartNew();
        bool baseline, disposed;
        readonly Timer changeTimer = new Timer { Interval = 60 };
        readonly Timer retryTimer = new Timer { Interval = 2000 };
        readonly Timer settleTimer = new Timer { Interval = 220 };
        int retries, queuedRefresh, autoAttempts;
        string pendingHeadphone, shortcutError, autoGroup;

        internal AppController(string config, bool demo, Func<Action, IAudioBackend> factory = null)
        {
            Demo = demo; audioFactory = factory; Store = new SettingsStore(config); Settings = Store.Load();
            Host = new MessageWindow(demo ? ".Preview" : ""); Host.OpenRequested = delegate { if (OpenRequested != null) OpenRequested(); };
            Keys = new HotkeyManager(Host);
            changeTimer.Tick += delegate { changeTimer.Stop(); Refresh(true); };
            retryTimer.Tick += delegate { retryTimer.Stop(); Refresh(baseline); };
            settleTimer.Tick += delegate
            {
                settleTimer.Stop(); string id = pendingHeadphone; pendingHeadphone = null;
                Device target = Devices.FirstOrDefault(d => d.Id == id && d.Online && d.IsHeadphones);
                if (Settings.AutoHeadphones && target != null) AutoSwitch(target.Id);
            };
            if (!demo)
            {
                SystemEvents.PowerModeChanged += PowerChanged;
                SystemEvents.SessionSwitch += SessionChanged;
            }
            Refresh(false);
            RegisterKeys();
            if (Store.Notice != null) LastError = Store.Notice;
        }
        void PowerChanged(object sender, PowerModeChangedEventArgs e)
        { if (e.Mode == PowerModes.Resume) Host.Dispatch(delegate { baseline = false; retries = 0; Refresh(false); }); }
        void SessionChanged(object sender, SessionSwitchEventArgs e)
        { if (e.Reason == SessionSwitchReason.SessionUnlock) Host.Dispatch(delegate { retries = 0; Refresh(false); }); }
        void ScheduleRefresh()
        {
            if (disposed || Interlocked.Exchange(ref queuedRefresh, 1) != 0) return;
            Host.Dispatch(delegate
            {
                Interlocked.Exchange(ref queuedRefresh, 0);
                // Bound the delay from the first event; a busy driver cannot postpone switching forever.
                if (!disposed && !changeTimer.Enabled) changeTimer.Start();
            });
        }
        internal int RefreshCount;

        internal void Refresh(bool connectionEvent)
        {
            if (disposed) return;
            RefreshCount++;
            try
            {
                List<Device> active;
                if (Demo && audioFactory == null) active = DemoDevices();
                else { if (audio == null) audio = audioFactory != null ? audioFactory(ScheduleRefresh) : new AudioService(ScheduleRefresh); active = audio.List(); }
                HashSet<string> groups = new HashSet<string>(active.Where(d => d.Online && d.IsHeadphones).Select(GroupKey), StringComparer.OrdinalIgnoreCase);
                HashSet<string> newGroups = new HashSet<string>(groups.Where(g => !connectedGroups.Contains(g)), StringComparer.OrdinalIgnoreCase);
                if (!baseline || !Settings.AutoHeadphones || (autoGroup != null && !groups.Contains(autoGroup)))
                { autoGroup = null; pendingHeadphone = null; settleTimer.Stop(); settlingInterfaces.Clear(); }
                Device arriving = null;
                if (connectionEvent && baseline && Settings.AutoHeadphones)
                {
                    long now = connectionClock.ElapsedMilliseconds;
                    foreach (string id in settlingInterfaces.Keys.Where(id => settlingInterfaces[id] < now || !active.Any(d => d.Online && d.Id == id)).ToArray()) settlingInterfaces.Remove(id);
                    foreach (Device d in active.Where(d => d.Online && d.IsHeadphones && !previous.Contains(d.Id))) settlingInterfaces[d.Id] = now + 1000;
                    // Only a newly connected receiver can take ownership. Late Game/Chat
                    // interfaces may refine that receiver, but cannot reclaim an older one.
                    List<Device> eligible = active.Where(d => newGroups.Count > 0 ? newGroups.Contains(GroupKey(d)) : string.Equals(GroupKey(d), autoGroup, StringComparison.OrdinalIgnoreCase)).ToList();
                    arriving = DeviceRules.ArrivingHeadphones(eligible, previous);
                    if (arriving == null && autoGroup != null)
                    {
                        Device owner = active.FirstOrDefault(d => d.Online && d.IsHeadphones && string.Equals(GroupKey(d), autoGroup, StringComparison.OrdinalIgnoreCase));
                        Device current = active.FirstOrDefault(d => d.Online && d.IsHeadphones && d.Current && settlingInterfaces.ContainsKey(d.Id));
                        // Windows can make a just-announced Chat/HFP interface default
                        // in a later callback. Correct only within this connection's
                        // short settling period, never maintain a permanent route lock.
                        if (owner != null && current != null)
                        {
                            Device preferred = DeviceRules.PreferredHeadphone(active, owner);
                            if (current.Id != preferred.Id) arriving = preferred;
                        }
                    }
                }
                // Wireless endpoints can become ACTIVE before their category metadata arrives.
                manualGroups.RemoveWhere(group => !active.Any(d => d.Online && d.IsHeadphones && string.Equals(GroupKey(d), group, StringComparison.OrdinalIgnoreCase)));
                connectedGroups = groups;
                previous = new HashSet<string>(active.Where(d => d.IsHeadphones).Select(d => d.Id)); baseline = true; retries = 0; retryTimer.Stop();
                foreach (Device d in active) Settings.For(d);
                foreach (Binding b in Settings.Devices.Where(b => !active.Any(d => d.Id == b.Id)))
                    active.Add(new Device { Id = b.Id, Name = b.Name, Flow = b.Flow, Online = false });
                Devices = active;
                MeterVersion++;
                if (arriving != null)
                {
                    autoGroup = GroupKey(arriving);
                    pendingHeadphone = null; settleTimer.Stop();
                    autoAttempts = 0;
                    if (DeviceRules.HeadphonePriority(arriving) >= 2)
                    { pendingHeadphone = arriving.Id; settleTimer.Interval = 220; if (!settleTimer.Enabled) settleTimer.Start(); Notify(); }
                    else AutoSwitch(arriving.Id);
                }
                else Notify();
            }
            catch (Exception ex)
            {
                if (audio != null) { try { audio.Dispose(); } catch { } audio = null; }
                Error("Audio devices could not be refreshed: " + ex.Message);
                if ((!Demo || audioFactory != null) && retries++ < 5) retryTimer.Start();
            }
        }
        internal void Notify() { if (Changed != null) Changed(); }
        void AutoSwitch(string id)
        {
            Device candidate = Devices.FirstOrDefault(d => d.Id == id && d.Online && d.IsHeadphones);
            if (candidate == null || !Settings.AutoHeadphones) return;
            if (manualGroups.Contains(GroupKey(candidate)) || !string.Equals(GroupKey(candidate), autoGroup, StringComparison.OrdinalIgnoreCase)) return;
            id = DeviceRules.PreferredHeadphone(Devices, candidate).Id;
            try { SwitchCore(id, true); autoAttempts = 0; }
            catch (Exception ex)
            {
                // Some receivers announce an endpoint shortly before it accepts policy changes.
                if (Settings.AutoHeadphones && autoAttempts++ < 3)
                { pendingHeadphone = id; settleTimer.Interval = 150; settleTimer.Start(); Notify(); }
                else Error("Headphones could not be selected: " + ex.Message);
            }
        }
        internal void Error(string text) { LastError = text; if (Feedback != null) Feedback(text, true); }
        internal void Message(string text) { if (Feedback != null) Feedback(text, false); }
        internal void Run(Action action) { try { action(); } catch (Exception ex) { Error(ex.Message); } }
        internal bool Change(Action<Settings> change)
        {
            Settings old = Settings.Copy();
            try
            {
                change(Settings);
                if (!Settings.AutoHeadphones) { autoGroup = null; pendingHeadphone = null; settleTimer.Stop(); settlingInterfaces.Clear(); }
                if (!Demo) Store.Save(Settings);
                Notify(); return true;
            }
            catch (Exception ex) { Settings = old; Error("Changes could not be saved: " + ex.Message); Notify(); return false; }
        }
        internal void SaveGeometry()
        { if (!Demo) try { Store.Save(Settings); } catch (Exception ex) { Error("Window position could not be saved: " + ex.Message); } }
        internal void RegisterKeys()
        {
            if (Demo) return;
            Keys.Register(Settings, id => Run(delegate { Switch(id); }), flow => Run(delegate { Cycle(flow); }));
            if (LastError == shortcutError) LastError = null;
            shortcutError = Keys.Problems.Count > 0 ? Keys.Problems.Count + " shortcut(s) unavailable. Click the marked shortcut to repair." : null;
            if (shortcutError != null) LastError = shortcutError;
        }
        static string GroupKey(Device d) { return string.IsNullOrEmpty(d.HardwareGroup) ? "endpoint:" + d.Id : d.HardwareGroup; }
        internal void Switch(string id) { SwitchCore(id, false); }
        void SwitchCore(string id, bool automatic)
        {
            Device d = Devices.FirstOrDefault(x => x.Id == id && x.Online);
            if (d == null) throw new InvalidOperationException("This source is disconnected. Connect it and try again.");
            if (d.Flow == 0)
            {
                pendingHeadphone = null; settleTimer.Stop();
                if (!automatic)
                {
                    autoGroup = null;
                    settlingInterfaces.Clear();
                    foreach (Device source in Devices.Where(x => x.Online && x.IsHeadphones)) manualGroups.Add(GroupKey(source));
                }
            }
            if (audio != null) audio.Switch(d);
            foreach (Device source in Devices.Where(x => x.Flow == d.Flow)) source.Current = source.Id == id;
            LastError = null; Notify();
            Message("Switched to " + Settings.Display(d) + ".");
        }
        internal void Cycle(int flow) { Switch(DeviceRules.Next(Devices, Settings, flow).Id); }
        internal void PrepareMeters(IEnumerable<string> ids) { if (audio != null) audio.PrepareMeters(ids); }
        internal float Peak(string id)
        {
            if (Demo && audioFactory == null) return Devices.Any(d => d.Id == id && d.Current) ? .42f : 0;
            return audio == null ? 0 : audio.Peak(id);
        }
        internal static List<Device> DemoDevices()
        {
            return new List<Device> {
                new Device { Id = "demo-headphones", Name = "Headphones", Flow = 0, FormFactor = 3, Online = true, Current = true },
                new Device { Id = "demo-speakers", Name = "Desk speakers", Flow = 0, FormFactor = 1, Online = true },
                new Device { Id = "demo-display", Name = "Studio display", Flow = 0, FormFactor = 9, Online = true },
                new Device { Id = "demo-mic", Name = "USB microphone", Flow = 1, FormFactor = 4, Online = true, Current = true },
                new Device { Id = "demo-webcam", Name = "Webcam mic", Flow = 1, FormFactor = 4, Online = true }
            };
        }
        internal void FillDemoBindings()
        {
            // Showcase features independently of the first-run preference defaults.
            Settings.Theme = "Dark"; Settings.Meters = true;
            int n = 0;
            foreach (Device d in Devices)
            { Binding b = Settings.For(d); b.Cycle = d.Id != "demo-display"; if (d.Id != "demo-display") { b.Key = (uint)(49 + n++); b.Mods = 3; } }
            Settings.OutputKey = 32; Settings.OutputMods = 3; Settings.InputKey = 77; Settings.InputMods = 3;
        }
        public void Dispose()
        {
            if (disposed) return; disposed = true;
            if (!Demo) { SystemEvents.PowerModeChanged -= PowerChanged; SystemEvents.SessionSwitch -= SessionChanged; }
            changeTimer.Dispose(); retryTimer.Dispose(); settleTimer.Dispose(); Keys.Dispose();
            if (audio != null) { audio.Dispose(); audio = null; } Host.Dispose();
        }
    }
}

