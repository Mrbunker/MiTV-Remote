using MiTVRemote.Controllers;
using MiTVRemote.Platform;
using MiTVRemote.UI;

namespace MiTVRemote;

public sealed class TrayApplicationContext : ApplicationContext
{
    private readonly NotifyIcon _notifyIcon;
    private readonly RemoteController _remote;
    private RemoteControlForm? _form;

    public TrayApplicationContext(AppConfig config)
    {
        _remote = new RemoteController(new MiTVController(config));
        _notifyIcon = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            Text = "MiTV Remote",
            Visible = true,
            ContextMenuStrip = new ContextMenuStrip()
        };
        _notifyIcon.ContextMenuStrip.Items.Add("打开遥控器", null, (_, _) => ShowRemote());
        _notifyIcon.ContextMenuStrip.Items.Add("搜索/切换设备", null, (_, _) => PickDevice());
        _notifyIcon.ContextMenuStrip.Items.Add(new ToolStripSeparator());
        _notifyIcon.ContextMenuStrip.Items.Add("退出", null, (_, _) => ExitThread());
        _notifyIcon.MouseClick += (_, e) => { if (e.Button == MouseButtons.Left) ShowRemote(); };
    }

    private void ShowRemote()
    {
        if (_form is { IsDisposed: false })
        {
            _form.Close();
            _form = null;
            return;
        }
        _form = new RemoteControlForm(_remote);
        _form.FormClosed += (_, _) => _form = null;
        var area = Screen.FromPoint(Cursor.Position).WorkingArea;
        _form.StartPosition = FormStartPosition.Manual;
        _form.Location = new Point(
            Math.Clamp(Cursor.Position.X - _form.Width / 2, area.Left, area.Right - _form.Width),
            Math.Max(area.Top, area.Bottom - _form.Height - 8));
        _form.Show();
        _form.Activate();
    }

    private void PickDevice()
    {
        using var picker = new DevicePickerForm(_remote);
        if (picker.ShowDialog() == DialogResult.OK && picker.SelectedHost is { } host)
        {
            _remote.SetHost(host);
            if (_form is { IsDisposed: false }) _form.Close();
        }
    }

    protected override void ExitThreadCore()
    {
        _form?.Close();
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _remote.Dispose();
        base.ExitThreadCore();
    }
}
