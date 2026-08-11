namespace MiTVRemote.Models;

public sealed record MiTVDevice(string Name, string Host)
{
    public override string ToString() => $"{Name} ({Host})";
}

public sealed record VolumeStatus(int Volume, int MaxVolume)
{
    public int Percent => MaxVolume <= 0 ? 0 : Math.Clamp((int)Math.Round(Volume * 100d / MaxVolume), 0, 100);
}

public sealed record SystemInfo(string? WifiMac, string? EthernetMac)
{
    public string? SigningMac => (EthernetMac ?? WifiMac)?.Replace(":", string.Empty).ToLowerInvariant();
}

public sealed record OperationResult(bool IsSuccess, string Message)
{
    public static OperationResult Success(string message) => new(true, message);
    public static OperationResult Failure(string message) => new(false, message);
}

public sealed record Result<T>(bool IsSuccess, T? Value, string Error)
{
    public static Result<T> Ok(T value) => new(true, value, string.Empty);
    public static Result<T> Fail(string error) => new(false, default, error);
}
