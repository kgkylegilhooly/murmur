using System.Windows;
using System.Windows.Input;
using Murmur.Input;

namespace Murmur;

/// <summary>Captures a new push-to-talk hotkey from the keyboard.</summary>
public partial class HotkeyWindow : Window
{
    private Key? _pendingModifier;

    public HotkeyWindow(Hotkey current)
    {
        InitializeComponent();
        Hotkey = current;
        HotkeyText.Text = current.ToString();
    }

    public Hotkey Hotkey { get; private set; }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        e.Handled = true;
        var key = RealKey(e);

        if (Hotkey.IsModifierKey(key))
        {
            // Could be the start of a combo, or a modifier used on its own; decide on key-up.
            _pendingModifier = key;
            return;
        }

        _pendingModifier = null;
        SetHotkey(new Hotkey(Keyboard.Modifiers, key));
    }

    protected override void OnPreviewKeyUp(KeyEventArgs e)
    {
        e.Handled = true;
        var key = RealKey(e);

        // A modifier pressed and released without any other key: use it on its own.
        if (_pendingModifier == key)
        {
            _pendingModifier = null;
            SetHotkey(new Hotkey(Keyboard.Modifiers & ~ModifierFor(key), key));
        }
    }

    private void SetHotkey(Hotkey hotkey)
    {
        Hotkey = hotkey;
        HotkeyText.Text = hotkey.ToString();
    }

    private static Key RealKey(KeyEventArgs e) => e.Key switch
    {
        Key.System => e.SystemKey,
        Key.ImeProcessed => e.ImeProcessedKey,
        Key.DeadCharProcessed => e.DeadCharProcessedKey,
        _ => e.Key,
    };

    private static ModifierKeys ModifierFor(Key key) => key switch
    {
        Key.LeftCtrl or Key.RightCtrl => ModifierKeys.Control,
        Key.LeftShift or Key.RightShift => ModifierKeys.Shift,
        Key.LeftAlt or Key.RightAlt => ModifierKeys.Alt,
        Key.LWin or Key.RWin => ModifierKeys.Windows,
        _ => ModifierKeys.None,
    };

    private void Save_Click(object sender, RoutedEventArgs e) => DialogResult = true;

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
