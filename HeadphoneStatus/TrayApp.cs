using System.Drawing;
using System.Windows.Forms;

namespace HeadphoneStatus;

internal sealed class TrayApp : IDisposable
{
    private readonly NotifyIcon _notifyIcon;
    private readonly System.Windows.Forms.Timer _timer;
    private readonly HeadphoneMonitor _monitor = new();
    private readonly Icon _onIcon = IconFactory.CreateHeadphoneIcon(Color.LimeGreen);
    private readonly Icon _offIcon = IconFactory.CreateHeadphoneIcon(Color.Gainsboro);
    private HeadphoneState? _lastState;

    public TrayApp()
    {
        var menu = new ContextMenuStrip();
        var statusItem = new ToolStripMenuItem("Checking...") { Enabled = false };
        menu.Items.Add(statusItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Check now", null, (_, _) => Poll());
        menu.Items.Add("Exit", null, (_, _) => Application.Exit());
        _statusItem = statusItem;

        _notifyIcon = new NotifyIcon
        {
            Icon = _offIcon,
            Visible = true,
            Text = "G535 headphone status",
            ContextMenuStrip = menu,
        };

        _timer = new System.Windows.Forms.Timer { Interval = 3000 };
        _timer.Tick += (_, _) => Poll();
        _timer.Start();

        Poll();
    }

    private readonly ToolStripMenuItem _statusItem;

    private void Poll()
    {
        var state = _monitor.Poll();
        if (state == _lastState) return;
        _lastState = state;

        switch (state)
        {
            case HeadphoneState.On:
                _notifyIcon.Icon = _onIcon;
                _notifyIcon.Text = "G535: On";
                _statusItem.Text = "Headphones are ON";
                break;
            case HeadphoneState.Off:
                _notifyIcon.Icon = _offIcon;
                _notifyIcon.Text = "G535: Off";
                _statusItem.Text = "Headphones are OFF";
                break;
            case HeadphoneState.NotConnected:
                _notifyIcon.Icon = _offIcon;
                _notifyIcon.Text = "G535: receiver not found";
                _statusItem.Text = "Receiver not detected";
                break;
            case HeadphoneState.Unknown:
            default:
                _notifyIcon.Icon = _offIcon;
                _notifyIcon.Text = "G535: status unknown";
                _statusItem.Text = "Status unknown";
                break;
        }
    }

    public void Dispose()
    {
        _timer.Dispose();
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _onIcon.Dispose();
        _offIcon.Dispose();
    }
}
