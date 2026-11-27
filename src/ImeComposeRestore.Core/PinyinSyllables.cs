namespace ImeComposeRestore;

/// <summary>
/// Standard Hanyu Pinyin syllables (tone-less) plus abbreviated initials.
/// Used only as a likelihood test — leftover strings may also be incomplete.
/// </summary>
public static class PinyinSyllables
{
    public static readonly IReadOnlySet<string> Complete;
    public static readonly IReadOnlySet<string> Prefixes;
    public static readonly IReadOnlySet<string> Initials;

    static PinyinSyllables()
    {
        var complete = new HashSet<string>(StringComparer.Ordinal);
        foreach (var s in AllCompleteSyllables)
            complete.Add(s);

        // Common ü spellings used by IMEs: v / ue / ve
        var extras = new List<string>();
        foreach (var s in complete)
        {
            if (s.Contains('v', StringComparison.Ordinal))
            {
                extras.Add(s.Replace('v', 'u'));
            }

            if (s.EndsWith("ve", StringComparison.Ordinal))
            {
                extras.Add(s[..^2] + "ue");
            }
        }

        foreach (var s in extras)
            complete.Add(s);

        Complete = complete;

        var prefixes = new HashSet<string>(StringComparer.Ordinal) { "" };
        foreach (var s in complete)
        {
            for (var i = 1; i <= s.Length; i++)
                prefixes.Add(s[..i]);
        }

        Prefixes = prefixes;

        Initials = new HashSet<string>(StringComparer.Ordinal)
        {
            "b", "p", "m", "f", "d", "t", "n", "l",
            "g", "k", "h", "j", "q", "x",
            "r", "z", "c", "s", "y", "w",
            "zh", "ch", "sh",
        };
    }

    public static bool IsCompleteSyllable(ReadOnlySpan<char> token)
        => Complete.Contains(token.ToString());

    public static bool IsSyllablePrefix(ReadOnlySpan<char> token)
        => Prefixes.Contains(token.ToString());

    public static bool IsInitial(ReadOnlySpan<char> token)
        => Initials.Contains(token.ToString());

    /// <summary>
    /// Try to segment a delimiter-free lowercase token as pinyin syllables,
    /// abbreviated initials, or an incomplete final syllable.
    /// </summary>
    public static bool TrySegment(string token, out IReadOnlyList<string> parts)
    {
        var list = new List<string>();
        if (TrySegmentCore(token, 0, list, allowIncompleteTail: true))
        {
            parts = list;
            return true;
        }

        list.Clear();
        if (TrySegmentInitials(token, 0, list, allowIncompleteTail: true))
        {
            parts = list;
            return true;
        }

        parts = Array.Empty<string>();
        return false;
    }

    private static bool TrySegmentCore(string token, int index, List<string> acc, bool allowIncompleteTail)
    {
        if (index >= token.Length)
            return acc.Count > 0 || token.Length == 0;

        for (var len = Math.Min(6, token.Length - index); len >= 1; len--)
        {
            var slice = token.AsSpan(index, len);
            var text = slice.ToString();
            var isLast = index + len == token.Length;
            if (Complete.Contains(text) || (isLast && allowIncompleteTail && Prefixes.Contains(text)))
            {
                acc.Add(text);
                if (TrySegmentCore(token, index + len, acc, allowIncompleteTail))
                    return true;
                acc.RemoveAt(acc.Count - 1);
            }
        }

        return false;
    }

    private static bool TrySegmentInitials(string token, int index, List<string> acc, bool allowIncompleteTail)
    {
        if (index >= token.Length)
            return acc.Count > 0;

        foreach (var len in new[] { 2, 1 })
        {
            if (index + len > token.Length)
                continue;
            var text = token.Substring(index, len);
            var isLast = index + len == token.Length;
            if (Initials.Contains(text) || (isLast && allowIncompleteTail && (text is "z" or "c" or "s")))
            {
                acc.Add(text);
                if (TrySegmentInitials(token, index + len, acc, allowIncompleteTail))
                    return true;
                acc.RemoveAt(acc.Count - 1);
            }
        }

        return false;
    }

    // Standard Putonghua pinyin finals with ü written as v (nve, lve, jue->ju+e already separate).
    private static readonly string[] AllCompleteSyllables =
    [
        "a", "ai", "an", "ang", "ao",
        "ba", "bai", "ban", "bang", "bao", "bei", "ben", "beng", "bi", "bian", "biao", "bie", "bin", "bing", "bo", "bu",
        "ca", "cai", "can", "cang", "cao", "ce", "cei", "cen", "ceng", "ci", "cong", "cou", "cu", "cuan", "cui", "cun", "cuo",
        "cha", "chai", "chan", "chang", "chao", "che", "chen", "cheng", "chi", "chong", "chou", "chu", "chua", "chuai", "chuan", "chuang", "chui", "chun", "chuo",
        "da", "dai", "dan", "dang", "dao", "de", "dei", "den", "deng", "di", "dia", "dian", "diao", "die", "ding", "diu", "dong", "dou", "du", "duan", "dui", "dun", "duo",
        "e", "ei", "en", "eng", "er",
        "fa", "fan", "fang", "fei", "fen", "feng", "fo", "fou", "fu",
        "ga", "gai", "gan", "gang", "gao", "ge", "gei", "gen", "geng", "gong", "gou", "gu", "gua", "guai", "guan", "guang", "gui", "gun", "guo",
        "ha", "hai", "han", "hang", "hao", "he", "hei", "hen", "heng", "hong", "hou", "hu", "hua", "huai", "huan", "huang", "hui", "hun", "huo",
        "ji", "jia", "jian", "jiang", "jiao", "jie", "jin", "jing", "jiong", "jiu", "ju", "juan", "jue", "jun",
        "ka", "kai", "kan", "kang", "kao", "ke", "kei", "ken", "keng", "kong", "kou", "ku", "kua", "kuai", "kuan", "kuang", "kui", "kun", "kuo",
        "la", "lai", "lan", "lang", "lao", "le", "lei", "leng", "li", "lia", "lian", "liang", "liao", "lie", "lin", "ling", "liu", "lo", "long", "lou", "lu", "luan", "lun", "luo", "lv", "lve",
        "ma", "mai", "man", "mang", "mao", "me", "mei", "men", "meng", "mi", "mian", "miao", "mie", "min", "ming", "miu", "mo", "mou", "mu",
        "na", "nai", "nan", "nang", "nao", "ne", "nei", "nen", "neng", "ni", "nian", "niang", "niao", "nie", "nin", "ning", "niu", "nong", "nou", "nu", "nuan", "nuo", "nv", "nve",
        "o", "ou",
        "pa", "pai", "pan", "pang", "pao", "pei", "pen", "peng", "pi", "pian", "piao", "pie", "pin", "ping", "po", "pou", "pu",
        "qi", "qia", "qian", "qiang", "qiao", "qie", "qin", "qing", "qiong", "qiu", "qu", "quan", "que", "qun",
        "ran", "rang", "rao", "re", "ren", "reng", "ri", "rong", "rou", "ru", "rua", "ruan", "rui", "run", "ruo",
        "sa", "sai", "san", "sang", "sao", "se", "sen", "seng", "si", "song", "sou", "su", "suan", "sui", "sun", "suo",
        "sha", "shai", "shan", "shang", "shao", "she", "shei", "shen", "sheng", "shi", "shou", "shu", "shua", "shuai", "shuan", "shuang", "shui", "shun", "shuo",
        "ta", "tai", "tan", "tang", "tao", "te", "tei", "teng", "ti", "tian", "tiao", "tie", "ting", "tong", "tou", "tu", "tuan", "tui", "tun", "tuo",
        "wa", "wai", "wan", "wang", "wei", "wen", "weng", "wo", "wu",
        "xi", "xia", "xian", "xiang", "xiao", "xie", "xin", "xing", "xiong", "xiu", "xu", "xuan", "xue", "xun",
        "ya", "yan", "yang", "yao", "ye", "yi", "yin", "ying", "yo", "yong", "you", "yu", "yuan", "yue", "yun",
        "za", "zai", "zan", "zang", "zao", "ze", "zei", "zen", "zeng", "zi", "zong", "zou", "zu", "zuan", "zui", "zun", "zuo",
        "zha", "zhai", "zhan", "zhang", "zhao", "zhe", "zhei", "zhen", "zheng", "zhi", "zhong", "zhou", "zhu", "zhua", "zhuai", "zhuan", "zhuang", "zhui", "zhun", "zhuo",
    ];
}
