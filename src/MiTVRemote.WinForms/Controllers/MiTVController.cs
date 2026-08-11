using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MiTVRemote.Models;
using MiTVRemote.Platform;

namespace MiTVRemote.Controllers;

public sealed class MiTVController : IDisposable
{
    private const int Port = 6095;
    private const string DefaultHost = "192.168.1.50";
    private readonly AppConfig _config;
    private readonly HttpClient _http = new(new SocketsHttpHandler { ConnectTimeout = TimeSpan.FromSeconds(2) })
    {
        Timeout = TimeSpan.FromSeconds(3)
    };
    private DateTime _osdUntil;

    public MiTVController(AppConfig config) => _config = config;
    public string CurrentHost =>
        Environment.GetEnvironmentVariable("TV_VOLUME_MITV_HOST")?.Trim() is { Length: > 0 } env ? env :
        _config.Get("MiTVHost")?.Trim() is { Length: > 0 } saved ? saved : DefaultHost;

    public void SetHost(string host) => _config.Set("MiTVHost", host.Trim());

    public async Task<Result<IReadOnlyList<MiTVDevice>>> DiscoverDevicesAsync(
        IProgress<string>? progress, CancellationToken cancellationToken)
    {
        var hosts = NetworkHelper.LocalIPv4Prefixes()
            .SelectMany(prefix => Enumerable.Range(1, 254).Select(i => $"{prefix}.{i}"))
            .Distinct(StringComparer.Ordinal).ToArray();
        if (hosts.Length == 0) return Result<IReadOnlyList<MiTVDevice>>.Fail("没有找到可用于扫描的局域网 IPv4 地址。");
        progress?.Report($"正在扫描 {hosts.Length} 个地址...");
        var found = new System.Collections.Concurrent.ConcurrentBag<MiTVDevice>();
        try
        {
            await Parallel.ForEachAsync(hosts, new ParallelOptions
            {
                MaxDegreeOfParallelism = 32, CancellationToken = cancellationToken
            }, async (host, ct) =>
            {
                var response = await RequestAsync(host, "/request?action=isalive", TimeSpan.FromMilliseconds(450), ct);
                if (response.Success && response.Json?.RootElement.TryGetProperty("data", out var data) == true)
                {
                    var name = data.TryGetProperty("devicename", out var value) ? value.GetString()?.Trim() : null;
                    found.Add(new MiTVDevice(string.IsNullOrEmpty(name) ? "MiTV" : name, host));
                }
            });
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        return Result<IReadOnlyList<MiTVDevice>>.Ok(found.OrderBy(x => x.Host, StringComparer.Ordinal).ToArray());
    }

    public async Task<Result<MiTVDevice>> DeviceStatusAsync(CancellationToken ct = default)
    {
        var response = await RequestAsync(CurrentHost, "/request?action=isalive", TimeSpan.FromSeconds(2), ct);
        if (!response.Success || response.Json?.RootElement.TryGetProperty("data", out var data) != true)
            return Result<MiTVDevice>.Fail(response.Error);
        var name = data.TryGetProperty("devicename", out var value) ? value.GetString()?.Trim() : null;
        return Result<MiTVDevice>.Ok(new MiTVDevice(string.IsNullOrEmpty(name) ? "MiTV" : name, CurrentHost));
    }

    public async Task<Result<VolumeStatus>> VolumeStatusAsync(CancellationToken ct = default)
    {
        var response = await RequestAsync(CurrentHost, "/controller?action=getvolume", TimeSpan.FromSeconds(2), ct);
        if (!response.Success || response.Json?.RootElement.TryGetProperty("data", out var data) != true ||
            !data.TryGetProperty("volume", out var volume) || !data.TryGetProperty("maxVolume", out var max))
            return Result<VolumeStatus>.Fail(response.Error.Length == 0 ? "音量返回格式无法识别。" : response.Error);
        return Result<VolumeStatus>.Ok(new VolumeStatus(volume.GetInt32(), max.GetInt32()));
    }

    public async Task<OperationResult> SetVolumePercentAsync(int percent, CancellationToken ct = default)
    {
        var status = await VolumeStatusAsync(ct);
        if (!status.IsSuccess || status.Value is null) return OperationResult.Failure(status.Error);
        var target = (int)Math.Round(Math.Clamp(percent, 0, 100) * status.Value.MaxVolume / 100d);
        var signed = await SetSignedVolumeAsync(target, ct);
        if (signed.IsSuccess) return signed;
        var delta = target - status.Value.Volume;
        for (var i = 0; i < Math.Abs(delta); i++)
        {
            var result = await SendKeyAsync(delta > 0 ? "volumeup" : "volumedown", ct);
            if (!result.IsSuccess) return result;
            await Task.Delay(45, ct);
        }
        return OperationResult.Success($"已通过遥控按键设置到约 {percent}%");
    }

    private async Task<OperationResult> SetSignedVolumeAsync(int volume, CancellationToken ct)
    {
        var response = await RequestAsync(CurrentHost, "/controller?action=getsysteminfo", TimeSpan.FromSeconds(2), ct);
        if (!response.Success || response.Json?.RootElement.TryGetProperty("data", out var data) != true)
            return OperationResult.Failure(response.Error);
        var ethernet = data.TryGetProperty("ethmac", out var e) ? e.GetString() : null;
        var wifi = data.TryGetProperty("wifimac", out var w) ? w.GetString() : null;
        var mac = new SystemInfo(wifi, ethernet).SigningMac;
        if (string.IsNullOrEmpty(mac)) return OperationResult.Failure("无法读取设备 MAC，不能生成音量签名。");
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var sign = Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes($"mitvsignsalt&{volume}&{mac}&{timestamp}"))).ToLowerInvariant();
        var result = await RequestAsync(CurrentHost, $"/general?action=setVolum&volum={volume}&ts={timestamp}&sign={sign}", TimeSpan.FromSeconds(2), ct);
        return result.Success ? OperationResult.Success($"已设置音量：{volume}") : OperationResult.Failure(result.Error);
    }

    public Task<OperationResult> SendKeyAsync(string key, CancellationToken ct = default) =>
        SendRequestAsync($"/controller?action=keyevent&keycode={Uri.EscapeDataString(key)}", $"已发送：{key}", ct);

    public Task<OperationResult> SwitchHDMIAsync(int input, CancellationToken ct = default) =>
        SendRequestAsync($"/controller?action=changesource&source=hdmi{Math.Clamp(input, 1, 2)}", $"已切换到 HDMI {Math.Clamp(input, 1, 2)}", ct);

    public async Task<OperationResult> AdjustBacklightAsync(bool increase, CancellationToken ct = default)
    {
        if (DateTime.UtcNow >= _osdUntil)
            foreach (var key in new[] { "menu", "right", "down", "right" })
            {
                var opened = await SendKeyAsync(key, ct);
                if (!opened.IsSuccess) return opened;
                await Task.Delay(400, ct);
            }
        var result = await SendKeyAsync(increase ? "up" : "down", ct);
        if (result.IsSuccess) _osdUntil = DateTime.UtcNow.AddSeconds(10);
        return result;
    }

    private async Task<OperationResult> SendRequestAsync(string path, string message, CancellationToken ct)
    {
        var response = await RequestAsync(CurrentHost, path, TimeSpan.FromSeconds(2), ct);
        return response.Success ? OperationResult.Success(message) : OperationResult.Failure(response.Error);
    }

    private async Task<Response> RequestAsync(string host, string path, TimeSpan timeout, CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate($"http://{host}:{Port}{path}", UriKind.Absolute, out var uri))
            return new Response(false, null, "设备地址无效。");
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);
        try
        {
            using var httpResponse = await _http.GetAsync(uri, timeoutCts.Token);
            var bytes = await httpResponse.Content.ReadAsByteArrayAsync(timeoutCts.Token);
            var body = Encoding.UTF8.GetString(bytes).Replace("\r", string.Empty);
            JsonDocument? json = null;
            try { json = JsonDocument.Parse(bytes); } catch (JsonException) { }
            var success = httpResponse.IsSuccessStatusCode &&
                          body.Contains("\"msg\":\"success\"", StringComparison.OrdinalIgnoreCase);
            return new Response(success, json, success ? string.Empty : body);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new Response(false, null, $"连接超时：{host}:{Port}");
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException)
        {
            return new Response(false, null, $"无法连接 {host}:{Port}：{ex.Message}");
        }
    }

    private sealed record Response(bool Success, JsonDocument? Json, string Error);
    public void Dispose() => _http.Dispose();
}
