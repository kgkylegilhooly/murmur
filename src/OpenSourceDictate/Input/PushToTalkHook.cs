using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows.Input;
using static OpenSourceDictate.Input.NativeMethods;

namespace OpenSourceDictate.Input;

/// <summary>
/// Watches the keyboard system-wide with a low-level hook and raises
/// <see cref="Pressed"/> when the hotkey goes down and <see cref="Released"/>
/// when it comes back up. RegisterHotKey can't be used here because it only
/// reports the press, never the release.
/// </summary>
/// <remarks>
/// Must be created on a thread with a message loop (the WPF UI thread).
/// Handlers run inside the hook callback, so they must return quickly;
/// Windows silently removes hooks that take too long.
/// </remarks>
public sealed class PushToTalkHook : IDisposable
{
    private readonly LowLevelKeyboardProc _proc; // keep alive: the native side holds a pointer to it
    private readonly HashSet<Key> _down = new();
    private IntPtr _hook;
    private bool _active;
    private bool _swallowMainKey; // swallow the main key until it's physically released

    public PushToTalkHook(Hotkey hotkey)
    {
        Hotkey = hotkey;
        _proc = HookCallback;
        _hook = SetWindowsHookEx(WH_KEYBOARD_LL, _proc, GetModuleHandle(null), 0);
        if (_hook == IntPtr.Zero)
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not install the keyboard hook.");
    }

    public event EventHandler? Pressed;
    public event EventHandler? Released;

    public Hotkey Hotkey { get; private set; }

    /// <summary>When false, keys pass through untouched (e.g. while the user is choosing a new hotkey).</summary>
    public bool Enabled { get; set; } = true;

    public void SetHotkey(Hotkey hotkey)
    {
        ReleaseIfActive();
        _swallowMainKey = false;
        Hotkey = hotkey;
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            var data = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);

            // Ignore keystrokes we (or other apps) inject, so typing out a
            // transcription can never trigger push-to-talk.
            if ((data.flags & LLKHF_INJECTED) == 0 && HandleKey(data.vkCode, (int)wParam))
                return (IntPtr)1; // swallow
        }

        return CallNextHookEx(_hook, nCode, wParam, lParam);
    }

    /// <returns>True if the keystroke should be swallowed.</returns>
    private bool HandleKey(uint vkCode, int message)
    {
        var key = KeyInterop.KeyFromVirtualKey((int)vkCode);
        var isDown = message is WM_KEYDOWN or WM_SYSKEYDOWN;
        var isUp = message is WM_KEYUP or WM_SYSKEYUP;

        if (isDown) _down.Add(key);
        else if (isUp) _down.Remove(key);

        if (!Enabled)
        {
            ReleaseIfActive();
            return false;
        }

        var hotkey = Hotkey;
        var isMainKey = key == hotkey.Key;
        // Swallow a regular main key (F9, CapsLock...) so it doesn't also reach the focused app.
        // Modifier keys always pass through, otherwise Windows could think they're stuck down.
        var swallow = isMainKey && !Hotkey.IsModifierKey(key);

        if (isDown && isMainKey)
        {
            if (_active || _swallowMainKey) return swallow; // auto-repeat while held
            if (!ModifiersHeld(hotkey.Modifiers)) return false;

            _active = true;
            _swallowMainKey = swallow;
            Pressed?.Invoke(this, EventArgs.Empty);
            return swallow;
        }

        if (isUp && (isMainKey || IsRequiredModifier(key, hotkey.Modifiers)))
        {
            ReleaseIfActive();
            if (!isMainKey) return false;

            var swallowed = _swallowMainKey;
            _swallowMainKey = false;
            return swallowed;
        }

        return false;
    }

    private void ReleaseIfActive()
    {
        if (!_active) return;
        _active = false;
        Released?.Invoke(this, EventArgs.Empty);
    }

    private bool ModifiersHeld(ModifierKeys modifiers) =>
        (!modifiers.HasFlag(ModifierKeys.Control) || _down.Contains(Key.LeftCtrl) || _down.Contains(Key.RightCtrl)) &&
        (!modifiers.HasFlag(ModifierKeys.Shift) || _down.Contains(Key.LeftShift) || _down.Contains(Key.RightShift)) &&
        (!modifiers.HasFlag(ModifierKeys.Alt) || _down.Contains(Key.LeftAlt) || _down.Contains(Key.RightAlt)) &&
        (!modifiers.HasFlag(ModifierKeys.Windows) || _down.Contains(Key.LWin) || _down.Contains(Key.RWin));

    private static bool IsRequiredModifier(Key key, ModifierKeys modifiers) => key switch
    {
        Key.LeftCtrl or Key.RightCtrl => modifiers.HasFlag(ModifierKeys.Control),
        Key.LeftShift or Key.RightShift => modifiers.HasFlag(ModifierKeys.Shift),
        Key.LeftAlt or Key.RightAlt => modifiers.HasFlag(ModifierKeys.Alt),
        Key.LWin or Key.RWin => modifiers.HasFlag(ModifierKeys.Windows),
        _ => false,
    };

    public void Dispose()
    {
        if (_hook == IntPtr.Zero) return;
        UnhookWindowsHookEx(_hook);
        _hook = IntPtr.Zero;
    }
}
