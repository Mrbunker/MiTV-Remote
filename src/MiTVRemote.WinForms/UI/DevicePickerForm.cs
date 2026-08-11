using MiTVRemote.Controllers;
using MiTVRemote.Models;

namespace MiTVRemote.UI;

public sealed class DevicePickerForm : Form
{
    private readonly RemoteController _remote;
    private readonly CancellationTokenSource _lifetime = new();
    private readonly ListBox _devices = new() { Dock = DockStyle.Fill };
    private readonly Label _status = new() { Dock = DockStyle.Top, Height = 28 };
    private readonly Button _search = new() { Text = "搜索", AutoSize = true };
    private readonly Button _use = new() { Text = "使用", AutoSize = true, Enabled = false };

    public string? SelectedHost { get; private set; }

    public DevicePickerForm(RemoteController remote)
    {
        _remote = remote;
        Text = "选择设备";
        ClientSize = new Size(430, 330);
        MinimumSize = new Size(430, 330);
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;

        _status.Text = "正在准备搜索...";
        _status.Padding = new Padding(8, 6, 8, 0);
        _devices.Margin = new Padding(8);
        _devices.DoubleClick += (_, _) => Accept();
        _search.Click += async (_, _) => await SearchAsync();
        _use.Click += (_, _) => Accept();
        var cancel = new Button { Text = "取消", AutoSize = true, DialogResult = DialogResult.Cancel };
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 42, FlowDirection = FlowDirection.RightToLeft };
        buttons.Controls.Add(cancel);
        buttons.Controls.Add(_use);
        buttons.Controls.Add(_search);
        Controls.Add(_devices);
        Controls.Add(_status);
        Controls.Add(buttons);
        AcceptButton = _use;
        CancelButton = cancel;
        Shown += async (_, _) => await SearchAsync();
        FormClosed += (_, _) => _lifetime.Cancel();
    }

    private async Task SearchAsync()
    {
        if (IsDisposed) return;
        _search.Enabled = false;
        _use.Enabled = false;
        _devices.Items.Clear();
        _status.Text = "正在扫描局域网...";
        try
        {
            var progress = new Progress<string>(message => { if (!IsDisposed) _status.Text = message; });
            var result = await _remote.DiscoverAsync(progress, _lifetime.Token);
            if (IsDisposed) return;
            if (!result.IsSuccess)
            {
                _status.Text = result.Error;
                return;
            }
            foreach (var device in result.Value ?? Array.Empty<MiTVDevice>()) _devices.Items.Add(device);
            _devices.SelectedIndex = _devices.Items.Count > 0 ? 0 : -1;
            _use.Enabled = _devices.SelectedIndex >= 0;
            _status.Text = _use.Enabled ? $"找到 {_devices.Items.Count} 台设备。" : "未找到设备。";
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { }
        finally
        {
            if (!IsDisposed) _search.Enabled = true;
        }
    }

    private void Accept()
    {
        if (_devices.SelectedItem is MiTVDevice device)
        {
            SelectedHost = device.Host;
            DialogResult = DialogResult.OK;
        }
    }
}
