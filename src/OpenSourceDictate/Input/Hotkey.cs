using System.Windows.Input;

namespace OpenSourceDictate.Input;

/// <summary>
/// A push-to-talk key: a main key plus any modifiers that must be held with it.
/// The main key may itself be a modifier (e.g. RightCtrl on its own).
/// </summary>
public sealed record Hotkey(ModifierKeys Modifiers, Key Key)
{
    public static Hotkey Default { get; } = new(ModifierKeys.None, Key.RightCtrl);

    public override string ToString()
    {
        var parts = new List<string>();
        if (Modifiers.HasFlag(ModifierKeys.Control)) parts.Add("Ctrl");
        if (Modifiers.HasFlag(ModifierKeys.Shift)) parts.Add("Shift");
        if (Modifiers.HasFlag(ModifierKeys.Alt)) parts.Add("Alt");
        if (Modifiers.HasFlag(ModifierKeys.Windows)) parts.Add("Win");
        parts.Add(Key.ToString());
        return string.Join("+", parts);
    }

    /// <summary>Parses strings like "RightCtrl", "F9" or "Ctrl+Shift+Space".</summary>
    public static bool TryParse(string? text, out Hotkey hotkey)
    {
        hotkey = Default;
        if (string.IsNullOrWhiteSpace(text)) return false;

        var parts = text.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var modifiers = ModifierKeys.None;
        foreach (var part in parts[..^1])
        {
            switch (part.ToLowerInvariant())
            {
                case "ctrl" or "control": modifiers |= ModifierKeys.Control; break;
                case "shift": modifiers |= ModifierKeys.Shift; break;
                case "alt": modifiers |= ModifierKeys.Alt; break;
                case "win" or "windows": modifiers |= ModifierKeys.Windows; break;
                default: return false;
            }
        }

        if (!Enum.TryParse<Key>(parts[^1], ignoreCase: true, out var key) || key == Key.None)
            return false;

        hotkey = new Hotkey(modifiers, key);
        return true;
    }

    public static bool IsModifierKey(Key key) => key is
        Key.LeftCtrl or Key.RightCtrl or
        Key.LeftShift or Key.RightShift or
        Key.LeftAlt or Key.RightAlt or
        Key.LWin or Key.RWin;
}
