using MiTVRemote.Controllers;
using MiTVRemote.Models;

namespace MiTVRemote.UI;

public sealed class RemoteControlForm : Form
{
    private readonly RemoteController _remote;
    private readonly CancellationTokenSource _lifetime = new();
    private readonly Label _volume = new();
    private readonly TrackBar _slider = new() { Minimum = 0, Maximum = 100, TickFrequency = 25 };
    private readonly Label _status = new();
    private bool _updating;

    public RemoteControlForm(RemoteController remote)
    {
        _remote = remote;
        Text = "MiTV Remote";
        FormBorderStyle = FormBorderStyle.FixedToolWindow;
        ShowInTaskbar = false;
        TopMost = true;
        ClientSize = new Size(280, 410);
        KeyPreview = true;
        BuildUi();
        Shown += async (_, _) => await RefreshAsync();
        FormClosed += (_, _) => _lifetime.Cancel();
    }

    private void BuildUi()
    {
        var panel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(14), AutoScroll = true };
        var title = new Label { Text = "电视遥控器", Font = new Font(Font, FontStyle.Bold), Width = 240, Height = 24 };
        _volume.Text = "当前音量：--%"; _volume.Width = 240; _volume.Height = 22;
        _slider.Width = 240; _slider.Value = 50;
        _slider.MouseUp += async (_, _) => await SetVolumeAsync();
        _slider.KeyUp += async (_, _) => await SetVolumeAsync();
        panel.Controls.Add(title); panel.Controls.Add(_volume); panel.Controls.Add(_slider);
        AddRow(panel, ("HDMI 1", () => RunAsync(() => _remote.HDMIAsync(1))), ("HDMI 2", () => RunAsync(() => _remote.HDMIAsync(2))));
        AddRow(panel, ("电源", () => RunKeyAsync("power")), ("音量−", () => RunKeyAsync("volumedown")), ("音量+", () => RunKeyAsync("volumeup")));
        AddRow(panel, ("▲", () => RunKeyAsync("up")), ("◀", () => RunKeyAsync("left")), ("OK", () => RunKeyAsync("enter")), ("▶", () => RunKeyAsync("right")), ("▼", () => RunKeyAsync("down")));
        AddRow(panel, ("主页", () => RunKeyAsync("home")), ("返回", () => RunKeyAsync("back")), ("菜单", () => RunKeyAsync("menu")));
        AddRow(panel, ("亮度−", () => RunAsync(() => _remote.BrightnessAsync(false))), ("亮度+", () => RunAsync(() => _remote.BrightnessAsync(true))));
        _status.Text = "设备：--"; _status.Width = 240; _status.Height = 38; _status.ForeColor = Color.Gray;
        panel.Controls.Add(_status);
        Controls.Add(panel);
    }

    private static void AddRow(Control parent, params (string Text, Action Click)[] buttons)
    {
        var row = new FlowLayoutPanel { Width = 245, Height = 38, WrapContents = false };
        foreach (var item in buttons)
        {
            var button = new Button { Text = item.Text, Width = Math.Max(42, 240 / buttons.Length - 4), Height = 30 };
            button.Click += (_, _) => item.Click();
            row.Controls.Add(button);
        }
        parent.Controls.Add(row);
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        var key = keyData switch { Keys.Left => "left", Keys.Right => "right", Keys.Up => "up", Keys.Down => "down", Keys.Enter => "enter", _ => null };
        if (key is not null) { _ = RunKeyAsync(key); return true; }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    private async Task RefreshAsync()
    {
        var host = _remote.Host;
        _status.Text = $"设备：{host}";
        var status = await _remote.StatusAsync(_lifetime.Token);
        if (status.IsSuccess && status.Value is not null) _status.Text = $"设备：{status.Value.Name}\r\nIP：{host}";
        var volume = await _remote.VolumeAsync(_lifetime.Token);
        if (volume.IsSuccess && volume.Value is not null) UpdateVolume(volume.Value.Percent);
    }

    private async Task SetVolumeAsync()
    {
        if (_updating) return;
        var result = await _remote.SetVolumeAsync(_slider.Value, _lifetime.Token);
        if (!result.IsSuccess) ShowError(result);
        await RefreshAsync();
    }

    private async Task RunKeyAsync(string key)
    {
        var result = await _remote.KeyAsync(key, _lifetime.Token);
        if (!result.IsSuccess) ShowError(result);
        if (key is "volumeup" or "volumedown") await RefreshAsync();
    }

    private async Task RunAsync(Func<Task<OperationResult>> action)
    {
        var result = await action();
        if (!result.IsSuccess) ShowError(result);
    }

    private void UpdateVolume(int percent)
    {
        _updating = true; _slider.Value = Math.Clamp(percent, 0, 100); _updating = false;
        _volume.Text = $"当前音量：{_slider.Value}%";
    }

    private void ShowError(OperationResult result) => MessageBox.Show(this, result.Message, "操作失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
}
