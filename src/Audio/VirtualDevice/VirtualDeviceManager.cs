using Mixline.Audio.Devices;
using Mixline.Logging;

namespace Mixline.Audio.VirtualDevice;

public sealed class VirtualRouteStatus
{
    public bool Available { get; init; }
    public bool Connected { get; init; }
    public string? RenderName { get; init; }
    public string? Hint { get; init; }
    public string Message { get; init; } = "";
}

public sealed class VirtualDeviceManager
{
    private readonly AppLog _log;
    private readonly DeviceManager _devices;

    public VirtualDeviceManager(AppLog log, DeviceManager devices)
    {
        _log = log;
        _devices = devices;
    }

    public IReadOnlyList<AudioDeviceInfo> Candidates()
        => _devices.RenderDevices().Where(d => d.IsVirtualCandidate).ToArray();

    public VirtualRouteStatus Status(string? selectedId, bool engineRunning)
    {
        var selected = _devices.Find(selectedId, DeviceFlow.Render);
        if (selected is null)
        {
            var candidates = Candidates();
            if (candidates.Count == 0)
            {
                return new VirtualRouteStatus
                {
                    Available = false,
                    Connected = false,
                    Message = "No virtual cable found. Install VB-Audio Cable so games get a Cuebox microphone.",
                    Hint = "Install VB-Audio Cable, then restart Cuebox. Games should use CABLE Output."
                };
            }

            return new VirtualRouteStatus
            {
                Available = true,
                Connected = false,
                Message = "Select CABLE Input as virtual output so games can hear you.",
                Hint = VirtualDeviceCatalog.CaptureHint(candidates[0].Name)
            };
        }

        var gameMic = VirtualDeviceCatalog.PairName(selected.Name);
        return new VirtualRouteStatus
        {
            Available = true,
            Connected = engineRunning,
            RenderName = selected.Name,
            Message = engineRunning
                ? $"Sending mix to {selected.Name}"
                : $"{selected.Name} is selected. Start the engine to send audio.",
            Hint = engineRunning
                ? $"In games, pick {gameMic} as the microphone. Windows default mic is pointed there too."
                : VirtualDeviceCatalog.CaptureHint(selected.Name)
        };
    }

    public void LogMissing()
    {
        _log.Warning("virtual", "Virtual audio device disconnected.");
    }
}
