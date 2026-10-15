using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using static Murmur.Input.NativeMethods;

namespace Murmur.Tray;

/// <summary>The notification-area icon and its right-click menu.</summary>
public sealed class TrayIcon : IDisposable
{
    private readonly NotifyIcon _notifyIcon;
    private readonly ToolStripMenuItem _hotkeyItem;
    private readonly Icon _idleIcon = CreateMicIcon(Color.FromArgb(90, 90, 90));
    private readonly Icon _recordingIcon = CreateMicIcon(Color.FromArgb(220, 38, 38));

    public TrayIcon()
    {
        _hotkeyItem = new ToolStripMenuItem { Enabled = false };

        var menu = new ContextMenuStrip();
        menu.Items.Add(_hotkeyItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Change hotkey…", null, (_, _) => ChangeHotkeyClicked?.Invoke(this, EventArgs.Empty));
        menu.Items.Add("Open recordings folder", null, (_, _) => OpenRecordingsClicked?.Invoke(this, EventArgs.Empty));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => ExitClicked?.Invoke(this, EventArgs.Empty));

        _notifyIcon = new NotifyIcon
        {
            Icon = _idleIcon,
            ContextMenuStrip = menu,
            Visible = true,
        };
        _notifyIcon.DoubleClick += (_, _) => ChangeHotkeyClicked?.Invoke(this, EventArgs.Empty);
    }

    public event EventHandler? ChangeHotkeyClicked;
    public event EventHandler? OpenRecordingsClicked;
    public event EventHandler? ExitClicked;

    public void SetHotkey(string hotkey)
    {
        _hotkeyItem.Text = $"Hold {hotkey} to dictate";
        SetText($"Murmur: hold {hotkey} to dictate");
    }

    public void SetRecording(bool recording)
    {
        _notifyIcon.Icon = recording ? _recordingIcon : _idleIcon;
    }

    public void ShowMessage(string title, string text, ToolTipIcon icon = ToolTipIcon.Info) =>
        _notifyIcon.ShowBalloonTip(3000, title, text, icon);

    private void SetText(string text) =>
        _notifyIcon.Text = text.Length > 63 ? text[..63] : text; // NotifyIcon.Text is capped at 63 chars

    /// <summary>Draws a simple microphone glyph so the repo doesn't need binary icon files yet.</summary>
    private static Icon CreateMicIcon(Color color)
    {
        using var bitmap = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);

            using var brush = new SolidBrush(color);
            using var pen = new Pen(color, 2.5f);
            using var capsule = new GraphicsPath();
            capsule.AddArc(11, 3, 10, 10, 180, 180);
            capsule.AddArc(11, 11, 10, 10, 0, 180);
            capsule.CloseFigure();
            g.FillPath(brush, capsule);
            g.DrawArc(pen, 7, 9, 18, 16, 0, 180); // cradle
            g.DrawLine(pen, 16, 25, 16, 29);      // stand
            g.DrawLine(pen, 11, 29, 21, 29);      // base
        }

        var handle = bitmap.GetHicon();
        try
        {
            return (Icon)Icon.FromHandle(handle).Clone();
        }
        finally
        {
            DestroyIcon(handle);
        }
    }

    public void Dispose()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _idleIcon.Dispose();
        _recordingIcon.Dispose();
    }
}
