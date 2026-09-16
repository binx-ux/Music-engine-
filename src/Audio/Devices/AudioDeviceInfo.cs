using NAudio.CoreAudioApi;

namespace Mixline.Audio.Devices;

public enum DeviceFlow
{
    Capture,
    Render
}

public sealed class AudioDeviceInfo
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required DeviceFlow Flow { get; init; }
    public bool IsDefault { get; init; }
    public bool IsActive { get; init; }
    public int SampleRate { get; init; }
    public int Channels { get; init; }
    public string State { get; init; } = "Connected";
    public bool IsVirtualCandidate { get; init; }

    public override string ToString() => Name;

    public override bool Equals(object? obj) => obj is AudioDeviceInfo other && other.Id == Id;

    public override int GetHashCode() => Id.GetHashCode(StringComparison.Ordinal);
}

public sealed class DeviceListChangedEventArgs : EventArgs
{
    public required string Reason { get; init; }
    public string? DeviceId { get; init; }
}
