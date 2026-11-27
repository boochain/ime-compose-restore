namespace ImeComposeRestore;

/// <summary>
/// One virtual-key to send through the IME (not Unicode injection, which bypasses IME).
/// </summary>
public readonly record struct KeyStroke(byte VirtualKey, bool Shift);

/// <summary>
/// Builds the string and key sequence used to reconstruct an IME composition
/// from a leftover pinyin residue.
/// </summary>
public static class RestoreSequence
{
    public const byte VkOem7 = 0xDE; // apostrophe / quotedbl on US and most CN layouts
    public const byte Vk0 = 0x30;
    public const byte VkA = 0x41;

    /// <summary>
    /// Composition string suitable for ImmSetCompositionString(SCS_SETSTR).
    /// Delimiters are normalized to ASCII apostrophe; ü becomes v; letters are lowercased.
    /// </summary>
    public static string CompositionString(string rawResidue)
        => PinyinResidueScanner.NormalizeComposition(rawResidue);

    /// <summary>
    /// Virtual-key strokes that should be delivered via SendInput / keybd_event
    /// so the current IME starts a new composition. Spaces and typographic
    /// marks become the apostrophe key — sending Space would often confirm
    /// the first candidate instead of delimiting a syllable.
    /// </summary>
    public static IReadOnlyList<KeyStroke> ToKeyStrokes(string rawResidue)
    {
        var normalized = CompositionString(rawResidue);
        var list = new List<KeyStroke>(normalized.Length);
        foreach (var ch in normalized)
        {
            if (ch == ImeDelimiters.Apostrophe)
            {
                list.Add(new KeyStroke(VkOem7, Shift: false));
                continue;
            }

            if (ch is >= 'a' and <= 'z')
            {
                list.Add(new KeyStroke((byte)(VkA + (ch - 'a')), Shift: false));
                continue;
            }

            if (ch is >= '1' and <= '5')
            {
                list.Add(new KeyStroke((byte)(Vk0 + (ch - '0')), Shift: false));
                continue;
            }
        }

        return list;
    }
}
