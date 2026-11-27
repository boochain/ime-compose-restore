namespace ImeComposeRestore;

/// <summary>
/// A leftover pinyin (or pinyin-like) run found near the caret after IME focus loss.
/// </summary>
public sealed record PinyinResidue(
    string Raw,
    int StartIndex,
    int Length,
    bool LooksLikePinyin,
    bool ContainsDelimiter,
    string NormalizedComposition)
{
    public int EndIndex => StartIndex + Length;
}
