namespace ImeComposeRestore;

/// <summary>
/// Finds leftover IME pinyin near a caret, including vendor-specific delimiters.
/// </summary>
public static class PinyinResidueScanner
{
    /// <summary>
    /// Locate the pinyin residue in <paramref name="text"/> relative to the caret
    /// (and an optional existing selection).
    /// </summary>
    /// <param name="text">Visible text around the caret (a line, or the whole control).</param>
    /// <param name="caretOffset">Caret index in <paramref name="text"/> (0..Length).</param>
    /// <param name="selectionStart">Selection start, or null if unknown / caret only.</param>
    /// <param name="selectionLength">Selection length, or 0.</param>
    public static PinyinResidue? Find(
        string text,
        int caretOffset,
        int? selectionStart = null,
        int selectionLength = 0)
    {
        if (text is null)
            throw new ArgumentNullException(nameof(text));
        if (caretOffset < 0 || caretOffset > text.Length)
            throw new ArgumentOutOfRangeException(nameof(caretOffset));

        if (selectionStart is int selStart && selectionLength > 0)
        {
            var selEnd = selStart + selectionLength;
            if (selStart >= 0 && selEnd <= text.Length)
            {
                var selected = text.Substring(selStart, selectionLength);
                if (LooksLikeResidue(selected, out var selectedNormalized, out var selectedLikely, out var selectedDelim))
                {
                    return new PinyinResidue(selected, selStart, selectionLength, selectedLikely, selectedDelim, selectedNormalized);
                }
            }
        }

        // Expand from the caret over letters / non-space delimiters / tone digits,
        // then optionally join a preceding pinyin token across a single space.
        var (start, end) = ExpandAroundCaret(text, caretOffset);
        if (end <= start)
            return null;

        start = JoinSpacesToTheLeft(text, start, end);

        var raw = text[start..end];
        raw = TrimEdgeDelimiters(raw, ref start, ref end);
        if (raw.Length == 0)
            return null;

        if (!LooksLikeResidue(raw, out var normalized, out var likely, out var hasDelim))
            return null;

        return new PinyinResidue(raw, start, end - start, likely, hasDelim, normalized);
    }

    /// <summary>
    /// True when the whole string is a leftover-pinyin candidate (used by tests and pre-checks).
    /// </summary>
    public static bool LooksLikeResidue(string value)
        => LooksLikeResidue(value, out _, out _, out _);

    public static bool LooksLikeResidue(
        string value,
        out string normalized,
        out bool looksLikePinyin,
        out bool containsDelimiter)
    {
        normalized = "";
        looksLikePinyin = false;
        containsDelimiter = false;
        if (string.IsNullOrEmpty(value))
            return false;

        if (!IsResidueCharset(value))
            return false;

        if (!HasLetter(value))
            return false;

        containsDelimiter = value.Any(ImeDelimiters.IsDelimiter);
        var pieces = SplitDelimiters(value);
        if (pieces.Count == 0)
            return false;

        looksLikePinyin = pieces.Take(pieces.Count - 1).All(IsCompleteOrInitials)
                          && (IsCompleteOrInitials(pieces[^1]) || IsPrefixOrInitials(pieces[^1]));

        // A run with explicit IME delimiters between letters is accepted even if
        // segmentation is imperfect (user or IME already marked syllable boundaries).
        if (!looksLikePinyin && !containsDelimiter)
        {
            // Still accept a short all-letter run: the user pressed the hotkey
            // on whatever was left, which may be an incomplete or typo'd preedit.
            if (value.Length > 32 || value.Any(char.IsWhiteSpace))
                return false;
        }

        normalized = NormalizeComposition(value);
        return normalized.Length > 0 && HasLetter(normalized);
    }

    public static string NormalizeComposition(string raw)
    {
        if (string.IsNullOrEmpty(raw))
            return "";

        var chars = new List<char>(raw.Length);
        foreach (var ch in raw)
        {
            if (ch is 'ü' or 'Ü' or 'ǖ' or 'ǘ' or 'ǚ' or 'ǜ' or 'v' or 'V')
            {
                chars.Add('v');
                continue;
            }

            if (ImeDelimiters.IsDelimiter(ch))
            {
                // Collapse consecutive delimiters to a single ASCII apostrophe.
                if (chars.Count > 0 && chars[^1] != ImeDelimiters.Apostrophe)
                    chars.Add(ImeDelimiters.Apostrophe);
                continue;
            }

            if (ch is >= 'A' and <= 'Z')
            {
                chars.Add((char)(ch - 'A' + 'a'));
                continue;
            }

            if (ch is >= 'a' and <= 'z' || IsToneDigit(ch))
            {
                chars.Add(ch);
            }
        }

        while (chars.Count > 0 && chars[0] == ImeDelimiters.Apostrophe)
            chars.RemoveAt(0);
        while (chars.Count > 0 && chars[^1] == ImeDelimiters.Apostrophe)
            chars.RemoveAt(chars.Count - 1);

        return new string(chars.ToArray());
    }

    private static (int start, int end) ExpandAroundCaret(string text, int caret)
    {
        var start = caret;
        var end = caret;

        while (start > 0 && IsCoreResidueChar(text[start - 1], previous: start >= 2 ? text[start - 2] : null))
            start--;

        while (end < text.Length && IsCoreResidueChar(text[end], previous: end > 0 ? text[end - 1] : null))
            end++;

        return (start, end);
    }

    private static int JoinSpacesToTheLeft(string text, int start, int end)
    {
        while (start > 1 && text[start - 1] == ' ')
        {
            var prevEnd = start - 1;
            var prevStart = prevEnd;
            while (prevStart > 0 && IsCoreResidueChar(text[prevStart - 1], previous: prevStart >= 2 ? text[prevStart - 2] : null))
                prevStart--;

            if (prevStart >= prevEnd)
                break;

            var left = text[prevStart..prevEnd];
            var right = text[start..end];
            if (!CanJoinAcrossSpace(left, right))
                break;

            start = prevStart;
        }

        return start;
    }

    private static bool CanJoinAcrossSpace(string left, string right)
    {
        if (string.IsNullOrEmpty(left) || string.IsNullOrEmpty(right))
            return false;
        if (!IsResidueCharset(left) || !IsResidueCharset(right))
            return false;

        // Require a real syllable (or syllable prefix) on the left so that
        // English words and short initial-only abbreviations are not joined
        // across a space ("hello world", "ms w'yao").
        var l = StripTones(left.ToLowerInvariant());
        if (PinyinSyllables.IsCompleteSyllable(l) || PinyinSyllables.IsSyllablePrefix(l))
            return true;
        if (!PinyinSyllables.TrySegment(l, out var parts) || parts.Count == 0)
            return false;
        var onlyInitials = parts.All(p => PinyinSyllables.IsInitial(p) && !PinyinSyllables.IsCompleteSyllable(p));
        return !onlyInitials;
    }

    private static bool IsCompleteOrInitials(string token)
    {
        var t = StripTones(token.ToLowerInvariant());
        if (t.Length == 0)
            return false;
        if (PinyinSyllables.IsCompleteSyllable(t) || PinyinSyllables.IsInitial(t))
            return true;
        return PinyinSyllables.TrySegment(t, out _);
    }

    private static bool IsPrefixOrInitials(string token)
    {
        var t = StripTones(token.ToLowerInvariant());
        return t.Length > 0 && (PinyinSyllables.IsSyllablePrefix(t) || PinyinSyllables.IsInitial(t) || PinyinSyllables.TrySegment(t, out _));
    }

    private static string StripTones(string token)
    {
        if (token.Length == 0)
            return token;
        var chars = token.Where(c => !IsToneDigit(c)).ToArray();
        return new string(chars);
    }

    private static bool IsCoreResidueChar(char c, char? previous)
    {
        if (IsPinyinLetter(c))
            return true;
        if (ImeDelimiters.IsNonSpaceDelimiter(c))
            return true;
        // Tone digits stay attached only when they follow a letter (xi1an).
        if (IsToneDigit(c) && previous is char p && IsPinyinLetter(p))
            return true;
        return false;
    }

    private static bool IsResidueCharset(string value)
    {
        char? prev = null;
        foreach (var c in value)
        {
            if (IsPinyinLetter(c) || ImeDelimiters.IsDelimiter(c) || (IsToneDigit(c) && prev is char p && IsPinyinLetter(p)))
            {
                prev = c;
                continue;
            }

            return false;
        }

        return true;
    }

    private static bool IsPinyinLetter(char c)
        => c is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z')
            or 'ü' or 'Ü' or 'ǖ' or 'ǘ' or 'ǚ' or 'ǜ' or 'v' or 'V';

    private static bool IsToneDigit(char c) => c is >= '1' and <= '5';

    private static bool HasLetter(string value) => value.Any(IsPinyinLetter);

    private static List<string> SplitDelimiters(string value)
    {
        var list = new List<string>();
        var current = new char[value.Length];
        var n = 0;
        foreach (var c in value)
        {
            if (ImeDelimiters.IsDelimiter(c))
            {
                if (n > 0)
                {
                    list.Add(new string(current, 0, n));
                    n = 0;
                }
            }
            else
            {
                current[n++] = c;
            }
        }

        if (n > 0)
            list.Add(new string(current, 0, n));
        return list;
    }

    private static string TrimEdgeDelimiters(string raw, ref int start, ref int end)
    {
        var i = 0;
        var j = raw.Length;
        while (i < j && ImeDelimiters.IsDelimiter(raw[i]))
            i++;
        while (j > i && ImeDelimiters.IsDelimiter(raw[j - 1]))
            j--;
        start += i;
        end -= raw.Length - j;
        return raw[i..j];
    }
}
