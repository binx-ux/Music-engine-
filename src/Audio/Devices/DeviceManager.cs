using System.Diagnostics;
using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;
using Mixline.Logging;

namespace Mixline.Audio.Devices;

public sealed class DeviceManager : IDisposable
{
    private readonly AppLog _log;
    private readonly MMDeviceEnumerator _enumerator;
    private readonly NotificationClient _notifications;
    private readonly object _cacheLock = new();
    private IReadOnlyList<AudioDeviceInfo>? _capture;
    private IReadOnlyList<AudioDeviceInfo>? _render;
    private bool _disposed;

    public event EventHandler<DeviceListChangedEventArgs>? DevicesChanged;

    public DeviceManager(AppLog log)
    {
        _log = log;
        _enumerator = new MMDeviceEnumerator();
        _notifications = new NotificationClient(this);
        _enumerator.RegisterEndpointNotificationCallback(_notifications);
    }

    public IReadOnlyList<AudioDeviceInfo> CaptureDevices()
    {
        lock (_cacheLock)
            return _capture ??= Enumerate(DataFlow.Capture);
    }

    public IReadOnlyList<AudioDeviceInfo> RenderDevices()
    {
        lock (_cacheLock)
            return _render ??= Enumerate(DataFlow.Render);
    }

    public AudioDeviceInfo? Find(string? id, DeviceFlow flow)
    {
        if (string.IsNullOrWhiteSpace(id))
            return null;
        var list = flow == DeviceFlow.Capture ? CaptureDevices() : RenderDevices();
        return list.FirstOrDefault(d => string.Equals(d.Id, id, StringComparison.OrdinalIgnoreCase));
    }

    public MMDevice? GetDevice(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return null;
        try
        {
            return _enumerator.GetDevice(id);
        }
        catch (Exception ex)
        {
            _log.Warning("devices", "Requested device is unavailable.", ex.Message);
            return null;
        }
    }

    public MMDevice? GetDefault(DeviceFlow flow)
    {
        try
        {
            return _enumerator.GetDefaultAudioEndpoint(
                flow == DeviceFlow.Capture ? DataFlow.Capture : DataFlow.Render,
                Role.Communications);
        }
        catch (Exception ex)
        {
            _log.Warning("devices", "No default audio device.", ex.Message);
            return null;
        }
    }

    private IReadOnlyList<AudioDeviceInfo> Enumerate(DataFlow flow)
    {
        var result = new List<AudioDeviceInfo>();
        MMDevice? defaultDevice = null;
        try
        {
            defaultDevice = _enumerator.GetDefaultAudioEndpoint(flow, Role.Multimedia);
        }
        catch
        {
        }

        var devices = _enumerator.EnumerateAudioEndPoints(flow, DeviceState.Active);
        foreach (var device in devices)
        {
            try
            {
                var mix = device.AudioClient.MixFormat;
                var name = device.FriendlyName;
                result.Add(new AudioDeviceInfo
                {
                    Id = device.ID,
                    Name = name,
                    Flow = flow == DataFlow.Capture ? DeviceFlow.Capture : DeviceFlow.Render,
                    IsDefault = defaultDevice is not null && device.ID == defaultDevice.ID,
                    IsActive = device.State == DeviceState.Active,
                    SampleRate = mix.SampleRate,
                    Channels = mix.Channels,
                    State = "Connected",
                    IsVirtualCandidate = VirtualDeviceCatalog.IsMicRoute(name)
                });
            }
            catch (Exception ex)
            {
                _log.Warning("devices", "Skipped a device that could not be queried.", ex.Message);
            }
            finally
            {
                device.Dispose();
            }
        }

        defaultDevice?.Dispose();
        return result;
    }

    internal void OnDeviceEvent(string reason, string? id)
    {
        lock (_cacheLock)
        {
            _capture = null;
            _render = null;
        }
        _log.Info("devices", reason + (id is null ? "" : $" ({id})"));
        DevicesChanged?.Invoke(this, new DeviceListChangedEventArgs { Reason = reason, DeviceId = id });
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        try
        {
            _enumerator.UnregisterEndpointNotificationCallback(_notifications);
        }
        catch
        {
        }
        _enumerator.Dispose();
    }

    private sealed class NotificationClient : IMMNotificationClient
    {
        private readonly DeviceManager _owner;

        public NotificationClient(DeviceManager owner) => _owner = owner;

        public void OnDeviceStateChanged(string deviceId, DeviceState newState)
            => _owner.OnDeviceEvent($"Device state changed: {newState}", deviceId);

        public void OnDeviceAdded(string pwstrDeviceId)
            => _owner.OnDeviceEvent("Device connected", pwstrDeviceId);

        public void OnDeviceRemoved(string deviceId)
            => _owner.OnDeviceEvent("Device disconnected", deviceId);

        public void OnDefaultDeviceChanged(DataFlow flow, Role role, string defaultDeviceId)
            => _owner.OnDeviceEvent("Default device changed", defaultDeviceId);

        public void OnPropertyValueChanged(string pwstrDeviceId, PropertyKey key)
        {
        }
    }
}

public static class VirtualDeviceCatalog
{
    public static bool IsVirtualName(string name) => IsMicRoute(name);

    public static bool IsMicRoute(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return false;
        if (Contains(name, "NVIDIA") || Contains(name, "Oculus") || Contains(name, "Meta Virtual")
            || Contains(name, "Sonar") || Contains(name, "Steam Streaming"))
            return false;
        return Contains(name, "CABLE Input")
            || (Contains(name, "VB-Audio") && Contains(name, "CABLE"))
            || Contains(name, "VoiceMeeter")
            || Contains(name, "Cuebox Virtual");
    }

    public static bool IsVirtualCapture(string name)
        => Contains(name, "CABLE Output")
            || Contains(name, "VoiceMeeter Output")
            || (Contains(name, "VoiceMeeter") && Contains(name, "Output"));

    public static AudioDeviceInfo? PreferredVirtualRender(IEnumerable<AudioDeviceInfo> renders)
    {
        var list = renders.Where(d => IsMicRoute(d.Name)).ToList();
        return list.FirstOrDefault(d => Contains(d.Name, "CABLE Input"))
            ?? list.FirstOrDefault(d => Contains(d.Name, "VoiceMeeter"))
            ?? list.FirstOrDefault();
    }

    public static AudioDeviceInfo? PairCapture(string renderName, IEnumerable<AudioDeviceInfo> captures)
    {
        if (Contains(renderName, "CABLE Input"))
            return captures.FirstOrDefault(c => Contains(c.Name, "CABLE Output"));
        if (Contains(renderName, "VoiceMeeter"))
            return captures.FirstOrDefault(c => Contains(c.Name, "VoiceMeeter Output"))
                ?? captures.FirstOrDefault(c => Contains(c.Name, "VoiceMeeter") && !Contains(c.Name, "Input"));
        return captures.FirstOrDefault(c => Contains(c.Name, "CABLE Output"));
    }

    public const string CableDownloadUrl = "https://vb-audio.com/Cable/";

    public static void OpenCableDownload()
    {
        try
        {
            Process.Start(new ProcessStartInfo(CableDownloadUrl) { UseShellExecute = true });
        }
        catch
        {
        }
    }

    public static string CaptureHint(string renderName)
    {
        var gameMic = PairName(renderName);
        return $"In Roblox, Discord, and games, set the microphone to {gameMic}. Cuebox sends the mix there.";
    }

    public static string PairName(string renderName)
    {
        if (Contains(renderName, "CABLE Input"))
            return "CABLE Output";
        if (Contains(renderName, "VoiceMeeter"))
            return "VoiceMeeter Output";
        return "the matching virtual capture device";
    }

    private static bool Contains(string name, string token)
        => name.Contains(token, StringComparison.OrdinalIgnoreCase);
}
