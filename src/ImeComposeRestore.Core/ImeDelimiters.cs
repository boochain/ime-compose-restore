namespace ImeComposeRestore;

/// <summary>
/// Characters documented or observed as syllable / segment separators
/// in Chinese IME preedit or in leftover committed pinyin.
/// </summary>
public static class ImeDelimiters
{
    /// <summary>ASCII apostrophe U+0027. Used by Microsoft Pinyin, Sogou, Rime default, and many others.</summary>
    public const char Apostrophe = '\'';

    /// <summary>Right single quotation mark U+2019, sometimes shown or committed as a typographic apostrophe.</summary>
    public const char RightSingleQuote = '\u2019';

    /// <summary>Left single quotation mark U+2018, included for robustness if a typographic layer flipped the mark.</summary>
    public const char LeftSingleQuote = '\u2018';

    /// <summary>
    /// Backtick U+0060. Not documented as the current official delimiter of the
    /// IMEs surveyed; old Microsoft Pinyin shuangpin showed it for zero-initials.
    /// Accepted as an optional leftover mark so it can be normalized to apostrophe.
    /// </summary>
    public const char Backtick = '`';

    /// <summary>
    /// Space. Official Microsoft Pinyin help lists space as a syllable delimiter
    /// (e.g. "xi an"). Rime luna_pinyin default delimiter is space + apostrophe.
    /// Must not be treated as a free walk character, or English sentences would be swallowed.
    /// </summary>
    public const char Space = ' ';

    public static readonly IReadOnlySet<char> NonSpace = new HashSet<char>
    {
        Apostrophe, RightSingleQuote, LeftSingleQuote, Backtick,
    };

    public static bool IsNonSpaceDelimiter(char c) => NonSpace.Contains(c);

    public static bool IsDelimiter(char c) => c == Space || IsNonSpaceDelimiter(c);

    /// <summary>Map any recognized delimiter to the ASCII apostrophe IMEs expect when re-typed.</summary>
    public static char Normalize(char c) => IsDelimiter(c) ? Apostrophe : c;
}
