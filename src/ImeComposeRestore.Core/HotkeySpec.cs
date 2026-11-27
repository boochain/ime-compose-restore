namespace ImeComposeRestore;

[Flags]
public enum HotkeyModifiers
{
    None = 0,
    Alt = 1,
    Control = 2,
    Shift = 4,
    Win = 8,
}

public sealed record HotkeySpec(HotkeyModifiers Modifiers, byte VirtualKey, string Display)
{
    /// <summary>Win32 MOD_* bits (same numeric values as <see cref="HotkeyModifiers"/>).</summary>
    public uint NativeModifiers => (uint)Modifiers | 0x4000; // MOD_NOREPEAT

    public static HotkeySpec Default { get; } = Parse("Ctrl+Alt+R");

    public static HotkeySpec Parse(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new ArgumentException("快捷键不能为空。", nameof(text));

        var parts = text.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
            throw new ArgumentException($"无法解析快捷键：{text}", nameof(text));

        var mods = HotkeyModifiers.None;
        string? keyToken = null;
        foreach (var part in parts)
        {
            switch (part.ToLowerInvariant())
            {
                case "ctrl":
                case "control":
                    mods |= HotkeyModifiers.Control;
                    break;
                case "alt":
                    mods |= HotkeyModifiers.Alt;
                    break;
                case "shift":
                    mods |= HotkeyModifiers.Shift;
                    break;
                case "win":
                case "windows":
                case "meta":
                    mods |= HotkeyModifiers.Win;
                    break;
                default:
                    if (keyToken is not null)
                        throw new ArgumentException($"快捷键里有多个主键：{text}", nameof(text));
                    keyToken = part;
                    break;
            }
        }

        if (keyToken is null)
            throw new ArgumentException($"快捷键缺少主键：{text}", nameof(text));

        var vk = ParseVirtualKey(keyToken);
        var spec = new HotkeySpec(mods, vk, Format(mods, keyToken));
        if (IsReservedImeShortcut(spec))
        {
            throw new ArgumentException(
                $"快捷键 {spec.Display} 与常见输入法/系统切换键冲突，请换一组。",
                nameof(text));
        }

        return spec;
    }

    public static bool IsReservedImeShortcut(HotkeySpec spec)
    {
        var key = spec.VirtualKey;
        var mods = spec.Modifiers;

        // Ctrl+Space — typical IME on/off
        if (mods == HotkeyModifiers.Control && key == 0x20)
            return true;
        // Win+Space — Windows input source switch
        if (mods == HotkeyModifiers.Win && key == 0x20)
            return true;
        // Ctrl+Shift+Space — sometimes IME-related
        if (mods == (HotkeyModifiers.Control | HotkeyModifiers.Shift) && key == 0x20)
            return true;
        // Ctrl+. — Microsoft IME punctuation / Chinese-English punctuation toggle (varies)
        if (mods == HotkeyModifiers.Control && key == 0xBE)
            return true;
        // Ctrl+Shift with no other useful key already rejected; bare Shift / Ctrl+Shift
        // cannot be registered (no primary key). Alt+Shift is also a layout switch.
        if (mods == (HotkeyModifiers.Alt | HotkeyModifiers.Shift) && key is >= 0x10 and <= 0x12)
            return true;
        // Ctrl+` often opens Rime/Weasel schema menu (F4 is another). Avoid Ctrl+Oem3.
        if (mods == HotkeyModifiers.Control && key == 0xC0)
            return true;
        // F4 is commonly bound by Weasel to the schema menu.
        if (mods == HotkeyModifiers.None && key == 0x73)
            return true;

        return false;
    }

    private static byte ParseVirtualKey(string token)
    {
        if (token.Length == 1)
        {
            var ch = char.ToUpperInvariant(token[0]);
            if (ch is >= 'A' and <= 'Z')
                return (byte)ch;
            if (ch is >= '0' and <= '9')
                return (byte)ch;
            return token[0] switch
            {
                ';' or '；' => 0xBA, // VK_OEM_1
                '=' => 0xBB,
                ',' => 0xBC,
                '-' => 0xBD,
                '.' => 0xBE,
                '/' => 0xBF,
                '`' => 0xC0,
                '[' => 0xDB,
                '\\' => 0xDC,
                ']' => 0xDD,
                '\'' => 0xDE,
                _ => throw new ArgumentException($"不支持的主键：{token}"),
            };
        }

        var upper = token.ToUpperInvariant();
        if (upper.StartsWith("VK_", StringComparison.Ordinal) &&
            byte.TryParse(upper[3..], System.Globalization.NumberStyles.HexNumber, null, out var vkHex))
        {
            return vkHex;
        }

        if (upper.Length >= 2 && upper[0] == 'F' && int.TryParse(upper[1..], out var fn) && fn is >= 1 and <= 24)
            return (byte)(0x70 + fn - 1);

        return upper switch
        {
            "SPACE" => 0x20,
            "TAB" => 0x09,
            "ESC" or "ESCAPE" => 0x1B,
            "OEM_1" or "SEMICOLON" => 0xBA,
            "OEM_7" or "QUOTE" or "APOSTROPHE" => 0xDE,
            "OEM_3" or "TILDE" => 0xC0,
            "OEM_PLUS" => 0xBB,
            "OEM_MINUS" => 0xBD,
            "PAUSE" or "BREAK" => 0x13,
            "INSERT" or "INS" => 0x2D,
            "HOME" => 0x24,
            "END" => 0x23,
            "PRIOR" or "PAGEUP" or "PGUP" => 0x21,
            "NEXT" or "PAGEDOWN" or "PGDN" => 0x22,
            _ => throw new ArgumentException($"不支持的主键：{token}"),
        };
    }

    private static string Format(HotkeyModifiers mods, string keyToken)
    {
        var parts = new List<string>();
        if (mods.HasFlag(HotkeyModifiers.Control))
            parts.Add("Ctrl");
        if (mods.HasFlag(HotkeyModifiers.Alt))
            parts.Add("Alt");
        if (mods.HasFlag(HotkeyModifiers.Shift))
            parts.Add("Shift");
        if (mods.HasFlag(HotkeyModifiers.Win))
            parts.Add("Win");
        parts.Add(keyToken.Length == 1 ? keyToken.ToUpperInvariant() : keyToken);
        return string.Join("+", parts);
    }
}
