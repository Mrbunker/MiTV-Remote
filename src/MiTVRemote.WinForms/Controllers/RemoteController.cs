using MiTVRemote.Models;

namespace MiTVRemote.Controllers;

public sealed class RemoteController : IDisposable
{
    private readonly MiTVController _tv;
    public RemoteController(MiTVController tv) => _tv = tv;
    public string Host => _tv.CurrentHost;
    public void SetHost(string host) => _tv.SetHost(host);
    public Task<Result<IReadOnlyList<MiTVDevice>>> DiscoverAsync(IProgress<string>? progress, CancellationToken ct)
        => _tv.DiscoverDevicesAsync(progress, ct);
    public Task<Result<MiTVDevice>> StatusAsync(CancellationToken ct = default) => _tv.DeviceStatusAsync(ct);
    public Task<Result<VolumeStatus>> VolumeAsync(CancellationToken ct = default) => _tv.VolumeStatusAsync(ct);
    public Task<OperationResult> SetVolumeAsync(int percent, CancellationToken ct = default) => _tv.SetVolumePercentAsync(percent, ct);
    public Task<OperationResult> KeyAsync(string key, CancellationToken ct = default) => _tv.SendKeyAsync(key, ct);
    public Task<OperationResult> HDMIAsync(int input, CancellationToken ct = default) => _tv.SwitchHDMIAsync(input, ct);
    public Task<OperationResult> BrightnessAsync(bool increase, CancellationToken ct = default) => _tv.AdjustBacklightAsync(increase, ct);
    public void Dispose() => _tv.Dispose();
}
