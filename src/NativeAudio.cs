using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Diagnostics;

namespace SwitchMyAudio
{
    [StructLayout(LayoutKind.Sequential)] internal struct PropertyKey
    {
        public Guid Format; public uint Id;
        public PropertyKey(string guid, uint id) { Format = new Guid(guid); Id = id; }
    }
    [StructLayout(LayoutKind.Explicit, Size = 24)] internal struct PropVariant
    {
        [FieldOffset(0)] public ushort Type;
        [FieldOffset(8)] public IntPtr Pointer;
        [FieldOffset(8)] public uint Number;
    }
    [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")] internal class DeviceEnumeratorClass { }
    [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IDeviceEnumerator
    {
        void EnumAudioEndpoints(int flow, uint state, out IDeviceCollection devices);
        void GetDefaultAudioEndpoint(int flow, int role, out IAudioDevice device);
        void GetDevice([MarshalAs(UnmanagedType.LPWStr)] string id, out IAudioDevice device);
        void RegisterEndpointNotificationCallback(IAudioNotification callback);
        void UnregisterEndpointNotificationCallback(IAudioNotification callback);
    }
    [ComImport, Guid("0BD7A1BE-7A1A-44DB-8397-CC5392387B5E"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IDeviceCollection { void GetCount(out uint count); void Item(uint index, out IAudioDevice device); }
    [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IAudioDevice
    {
        void Activate(ref Guid iid, uint context, IntPtr param, [MarshalAs(UnmanagedType.IUnknown)] out object value);
        void OpenPropertyStore(uint access, out IPropertyStore store);
        void GetId([MarshalAs(UnmanagedType.LPWStr)] out string id);
        void GetState(out uint state);
    }
    [ComImport, Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IPropertyStore
    {
        void GetCount(out uint count); void GetAt(uint index, out PropertyKey key);
        void GetValue(ref PropertyKey key, out PropVariant value);
        void SetValue(ref PropertyKey key, ref PropVariant value); void Commit();
    }
    [Guid("7991EEC9-7E89-4D85-8390-6C703CEC60C0"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IAudioNotification
    {
        [PreserveSig] int OnDeviceStateChanged([MarshalAs(UnmanagedType.LPWStr)] string id, uint state);
        [PreserveSig] int OnDeviceAdded([MarshalAs(UnmanagedType.LPWStr)] string id);
        [PreserveSig] int OnDeviceRemoved([MarshalAs(UnmanagedType.LPWStr)] string id);
        [PreserveSig] int OnDefaultDeviceChanged(int flow, int role, [MarshalAs(UnmanagedType.LPWStr)] string id);
        [PreserveSig] int OnPropertyValueChanged([MarshalAs(UnmanagedType.LPWStr)] string id, PropertyKey key);
    }
    [ClassInterface(ClassInterfaceType.None)] internal sealed class AudioNotification : IAudioNotification
    {
        internal Action Changed;
        int Signal() { try { Action a = Changed; if (a != null) a(); } catch { } return 0; }
        public int OnDeviceStateChanged(string id, uint state) { return Signal(); }
        public int OnDeviceAdded(string id) { return Signal(); }
        public int OnDeviceRemoved(string id) { return Signal(); }
        public int OnDefaultDeviceChanged(int flow, int role, string id) { return Signal(); }
        public int OnPropertyValueChanged(string id, PropertyKey key) { return Signal(); }
    }
    [ComImport, Guid("C02216F6-8C67-4B5B-9D00-D008E73E0064"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IAudioMeter
    {
        void GetPeakValue(out float peak); void GetMeteringChannelCount(out uint count);
        void GetChannelsPeakValues(uint count, IntPtr peaks); void QueryHardwareSupport(out uint mask);
    }
    // Private Windows policy interface. Kept isolated here for compatibility maintenance.
    // Unused methods reserve their native vtable slots; only SetDefaultEndpoint is invoked.
    [ComImport, Guid("870AF99C-171D-4F9E-AF0D-E63DF40C2BC9")] internal class PolicyClass { }
    [ComImport, Guid("F8679F50-850A-41CF-9C72-430F290290C8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IPolicy
    {
        void GetMixFormat(); void GetDeviceFormat(); void ResetDeviceFormat(); void SetDeviceFormat();
        void GetProcessingPeriod(); void SetProcessingPeriod(); void GetShareMode(); void SetShareMode();
        void GetPropertyValue(); void SetPropertyValue();
        void SetDefaultEndpoint([MarshalAs(UnmanagedType.LPWStr)] string id, int role);
        void SetEndpointVisibility();
    }
    internal interface IAudioBackend : IDisposable
    {
        List<Device> List();
        void Switch(Device target);
        void PrepareMeters(IEnumerable<string> ids);
        float Peak(string id);
    }
    internal sealed class AudioService : IAudioBackend
    {
        IDeviceEnumerator enumerator;
        AudioNotification notification;
        readonly Dictionary<string, IAudioMeter> meters = new Dictionary<string, IAudioMeter>();
        readonly Dictionary<string, long> meterRetries = new Dictionary<string, long>();
        internal int MeterOpenAttempts;
        internal int CachedMeters { get { return meters.Count; } }
        internal int PendingMeters { get { return meterRetries.Count; } }
        bool disposed;
        [DllImport("ole32.dll")] static extern int PropVariantClear(ref PropVariant v);
        [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)] static extern uint CM_Locate_DevNodeW(out uint node, string instance, uint flags);
        [DllImport("cfgmgr32.dll")] static extern uint CM_Get_Parent(out uint parent, uint node, uint flags);
        [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)] static extern uint CM_Get_DevNode_PropertyW(uint node, ref PropertyKey key, out uint type, byte[] buffer, ref uint size, uint flags);

        internal static bool HeadphoneHardware(string endpointId, out int rank, bool includeAbsent = false)
        { string group; return HeadphoneHardware(endpointId, out rank, out group, includeAbsent); }
        internal static bool HeadphoneHardware(string endpointId, out int rank, out string group, bool includeAbsent = false)
        {
            // Read canonical device categories, including USB/Bluetooth headset metadata.
            // Never infer headphone identity from an editable name or from USB/Bluetooth alone.
            rank = 1; group = ""; uint node;
            if (CM_Locate_DevNodeW(out node, "SWD\\MMDEVAPI\\" + endpointId, includeAbsent ? 1u : 0u) != 0) return false;
            PropertyKey container = new PropertyKey("8C7ED206-3F8A-4827-B3AB-AE9E1FAEFC6C", 2);
            byte[] guidBytes = new byte[16]; uint guidSize = 16, guidType;
            if (CM_Get_DevNode_PropertyW(node, ref container, out guidType, guidBytes, ref guidSize, 0) == 0 && guidType == 13 && guidSize == 16)
            { Guid guid = new Guid(guidBytes); if (guid != Guid.Empty) group = guid.ToString("D"); }
            PropertyKey category = new PropertyKey("78C34FC8-104A-4ACA-9EA4-524D52996E57", 90);
            byte[] buffer = new byte[8192];
            for (int depth = 0; depth < 6; depth++)
            {
                uint size = (uint)buffer.Length, type;
                if (CM_Get_DevNode_PropertyW(node, ref category, out type, buffer, ref size, 0) == 0 && type == 0x2012 && size <= buffer.Length)
                {
                    string[] values = Encoding.Unicode.GetString(buffer, 0, (int)size).Split(new char[] { '\0' }, StringSplitOptions.RemoveEmptyEntries);
                    if (DeviceRules.HeadphoneCategories(values)) return true;
                }
                PropertyKey hardware = new PropertyKey("A45C254E-DF1C-4EFD-8020-67D146A850E0", 3); size = (uint)buffer.Length;
                if (CM_Get_DevNode_PropertyW(node, ref hardware, out type, buffer, ref size, 0) == 0 && type == 0x2012 && size <= buffer.Length)
                {
                    string[] values = Encoding.Unicode.GetString(buffer, 0, (int)size).Split(new char[] { '\0' }, StringSplitOptions.RemoveEmptyEntries);
                    if (DeviceRules.KnownHeadphoneHardware(values, out rank)) return true;
                }
                uint parent; if (CM_Get_Parent(out parent, node, 0) != 0 || parent == node) break;
                node = parent;
            }
            return false;
        }

        internal AudioService(Action changed)
        {
            try
            {
                enumerator = (IDeviceEnumerator)new DeviceEnumeratorClass();
                if (changed != null)
                {
                    notification = new AudioNotification { Changed = changed };
                    enumerator.RegisterEndpointNotificationCallback(notification);
                }
            }
            catch { if (notification != null) notification.Changed = null; Release(enumerator); enumerator = null; throw; }
        }
        internal static void Release(object value) { if (value != null && Marshal.IsComObject(value)) Marshal.ReleaseComObject(value); }
        public List<Device> List()
        {
            List<Device> list = new List<Device>();
            for (int flow = 0; flow < 2; flow++)
            {
                string current = Default(flow, 0);
                IDeviceCollection collection = null;
                try
                {
                    enumerator.EnumAudioEndpoints(flow, 1, out collection);
                    uint count; collection.GetCount(out count);
                    for (uint i = 0; i < count; i++)
                    {
                        IAudioDevice d = null; IPropertyStore properties = null;
                        try
                        {
                            collection.Item(i, out d); string id; d.GetId(out id);
                            d.OpenPropertyStore(0, out properties);
                            string name = StringProperty(properties, new PropertyKey("A45C254E-DF1C-4EFD-8020-67D146A850E0", 14)) ?? "Audio device";
                            int factor = -1;
                            try
                            {
                                PropertyKey key = new PropertyKey("1DA5D803-D492-4EDD-8C23-E0C0FFEE7F0E", 0);
                                PropVariant v; properties.GetValue(ref key, out v);
                                try { if (v.Type == 19) factor = (int)v.Number; } finally { PropVariantClear(ref v); }
                            }
                            catch (COMException) { }
                            int rank = 1; string group = "";
                            bool headphone = flow == 0 && HeadphoneHardware(id, out rank, out group);
                            list.Add(new Device { Id = id, Name = name, Flow = flow, FormFactor = factor,
                                HeadphoneCategory = headphone, HeadphoneRank = rank, HardwareGroup = group, Online = true, Current = id == current });
                        }
                        catch (COMException) { /* A device can vanish between enumeration and reading its properties. */ }
                        finally { Release(properties); Release(d); }
                    }
                }
                finally { Release(collection); }
            }
            HashSet<string> active = new HashSet<string>(list.Select(d => d.Id));
            foreach (string id in meters.Keys.Where(id => !active.Contains(id)).ToArray()) { Release(meters[id]); meters.Remove(id); }
            foreach (string id in meterRetries.Keys.Where(id => !active.Contains(id)).ToArray()) meterRetries.Remove(id);
            return list;
        }
        static string StringProperty(IPropertyStore store, PropertyKey key)
        {
            PropVariant value; store.GetValue(ref key, out value);
            try { return value.Type == 31 ? Marshal.PtrToStringUni(value.Pointer) : null; }
            finally { PropVariantClear(ref value); }
        }
        internal string Default(int flow, int role)
        {
            IAudioDevice d = null;
            try { enumerator.GetDefaultAudioEndpoint(flow, role, out d); string id; d.GetId(out id); return id; }
            catch (COMException) { return null; }
            finally { Release(d); }
        }
        public void Switch(Device target)
        {
            IAudioDevice device = null;
            try
            {
                enumerator.GetDevice(target.Id, out device); uint state; device.GetState(out state);
                if ((state & 1) == 0) throw new InvalidOperationException("This source is disconnected.");
            }
            finally { Release(device); }
            string[] previous = Enumerable.Range(0, 3).Select(role => Default(target.Flow, role)).ToArray();
            IPolicy policy = (IPolicy)new PolicyClass(); int changed = 0;
            try
            {
                for (int role = 0; role < 3; role++) { policy.SetDefaultEndpoint(target.Id, role); changed++; }
                for (int role = 0; role < 3; role++) if (Default(target.Flow, role) != target.Id) throw new InvalidOperationException("Windows did not confirm the source change.");
            }
            catch
            {
                for (int role = 0; role < changed; role++) try { if (previous[role] != null) policy.SetDefaultEndpoint(previous[role], role); } catch { }
                throw;
            }
            finally { Release(policy); }
        }
        public void PrepareMeters(IEnumerable<string> ids)
        {
            HashSet<string> desired = new HashSet<string>(ids);
            foreach (string id in meters.Keys.Where(id => !desired.Contains(id)).ToArray()) { Release(meters[id]); meters.Remove(id); }
            foreach (string id in meterRetries.Keys.Where(id => !desired.Contains(id)).ToArray()) meterRetries.Remove(id);
            foreach (string id in desired.Where(id => !meters.ContainsKey(id))) OpenMeter(id);
        }
        void OpenMeter(string id)
        {
            long retry; if (meterRetries.TryGetValue(id, out retry) && Stopwatch.GetTimestamp() < retry) return;
            IAudioDevice device = null; object value = null; MeterOpenAttempts++;
            try
            {
                enumerator.GetDevice(id, out device); Guid iid = typeof(IAudioMeter).GUID;
                device.Activate(ref iid, 23, IntPtr.Zero, out value); meters[id] = (IAudioMeter)value; value = null; meterRetries.Remove(id);
            }
            catch (Exception ex)
            {
                if (!(ex is COMException) && !(ex is ArgumentException) && !(ex is InvalidCastException) && !(ex is InvalidComObjectException)) throw;
                meterRetries[id] = Stopwatch.GetTimestamp() + 2L * Stopwatch.Frequency;
            }
            finally { Release(value); Release(device); }
        }
        public float Peak(string id)
        {
            IAudioMeter meter;
            if (!meters.TryGetValue(id, out meter))
            { if (meterRetries.ContainsKey(id)) OpenMeter(id); if (!meters.TryGetValue(id, out meter)) return 0; }
            try { float peak; meter.GetPeakValue(out peak); return float.IsNaN(peak) ? 0 : Math.Max(0, Math.Min(1, peak)); }
            catch (COMException) { Release(meter); meters.Remove(id); meterRetries[id] = Stopwatch.GetTimestamp() + 2L * Stopwatch.Frequency; return 0; }
        }
        public void Dispose()
        {
            if (disposed) return; disposed = true;
            if (notification != null) { notification.Changed = null; try { enumerator.UnregisterEndpointNotificationCallback(notification); } catch (COMException) { } }
            foreach (IAudioMeter meter in meters.Values) Release(meter); meters.Clear();
            meterRetries.Clear();
            Release(enumerator); enumerator = null;
        }
    }
}
