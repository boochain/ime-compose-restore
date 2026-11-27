using ImeComposeRestore;
using Xunit;

namespace ImeComposeRestore.Tests;

public class PinyinResidueScannerTests
{
    private static PinyinResidue MustFind(string text, int? caret = null)
        => PinyinResidueScanner.Find(text, caret ?? text.Length)
           ?? throw new Xunit.Sdk.XunitException($"应识别残留串，但没有：{text}");

    [Theory]
    [InlineData("nihao")]
    [InlineData("zhongguo")]
    [InlineData("woyaokaishi")]
    [InlineData("zhon")] // incomplete last syllable
    [InlineData("zh")]
    [InlineData("zgrm")] // abbreviated initials
    public void Finds_plain_pinyin_at_caret(string raw)
    {
        var hit = MustFind(raw);
        Assert.Equal(raw, hit.Raw);
        Assert.Equal(0, hit.StartIndex);
        Assert.Equal(raw, hit.NormalizedComposition);
        Assert.True(hit.LooksLikePinyin);
    }

    [Theory]
    [InlineData("xi'an", "xi'an")]
    [InlineData("ni'hao", "ni'hao")]
    [InlineData("w'yao'kai'shi", "w'yao'kai'shi")]
    [InlineData("k's'h'y'z", "k's'h'y'z")]
    [InlineData("fen'hao", "fen'hao")]
    public void Finds_ascii_apostrophe_delimiters(string raw, string normalized)
    {
        var hit = MustFind(raw);
        Assert.Equal(raw, hit.Raw);
        Assert.Equal(normalized, hit.NormalizedComposition);
        Assert.True(hit.ContainsDelimiter);
        Assert.True(hit.LooksLikePinyin);
    }

    [Fact]
    public void Finds_typographic_right_single_quote()
    {
        var raw = "xi\u2019an";
        var hit = MustFind(raw);
        Assert.Equal(raw, hit.Raw);
        Assert.Equal("xi'an", hit.NormalizedComposition);
        Assert.True(hit.ContainsDelimiter);
    }

    [Fact]
    public void Finds_typographic_left_single_quote_and_normalizes()
    {
        var raw = "ni\u2018hao";
        var hit = MustFind(raw);
        Assert.Equal("ni'hao", hit.NormalizedComposition);
    }

    [Fact]
    public void Finds_backtick_and_normalizes_to_apostrophe()
    {
        var raw = "xi`an";
        var hit = MustFind(raw);
        Assert.Equal("xi'an", hit.NormalizedComposition);
        Assert.True(hit.ContainsDelimiter);
    }

    [Theory]
    [InlineData("xi an", "xi'an")]
    [InlineData("pi ao", "pi'ao")]
    [InlineData("xi an de", "xi'an'de")]
    public void Joins_space_delimited_microsoft_or_rime_style_pinyin(string raw, string normalized)
    {
        var hit = MustFind(raw);
        Assert.Equal(raw, hit.Raw);
        Assert.Equal(normalized, hit.NormalizedComposition);
        Assert.True(hit.ContainsDelimiter);
    }

    [Fact]
    public void Does_not_swallow_english_sentence_as_one_residue()
    {
        const string text = "hello world";
        var hit = PinyinResidueScanner.Find(text, text.Length);
        // "world" / "you"-like last word may be picked as a letter run,
        // but the scanner must not return the entire English sentence.
        if (hit is not null)
        {
            Assert.DoesNotContain(' ', hit.Raw);
            Assert.NotEqual(text, hit.Raw);
        }
    }

    [Fact]
    public void Picks_pinyin_after_english_instead_of_the_whole_line()
    {
        const string text = "hello xi'an";
        var hit = MustFind(text);
        Assert.Equal("xi'an", hit.Raw);
        Assert.Equal(text.Length - 5, hit.StartIndex);
    }

    [Theory]
    [InlineData("xi1an", "xi1an")]
    [InlineData("pi2ao", "pi2ao")]
    [InlineData("hao3", "hao3")]
    public void Keeps_tone_digits_used_by_microsoft_pinyin(string raw, string normalized)
    {
        var hit = MustFind(raw);
        Assert.Equal(normalized, hit.NormalizedComposition);
    }

    [Fact]
    public void Does_not_eat_a_year_or_plain_number_before_pinyin()
    {
        const string text = "2024nihao";
        var hit = MustFind(text);
        Assert.Equal("nihao", hit.Raw);
        Assert.Equal(4, hit.StartIndex);
    }

    [Fact]
    public void Finds_pinyin_after_already_committed_chinese()
    {
        const string text = "你好ma";
        var hit = MustFind(text);
        Assert.Equal("ma", hit.Raw);
        Assert.Equal(2, hit.StartIndex);
        Assert.Equal("ma", hit.NormalizedComposition);
    }

    [Fact]
    public void Finds_delimited_pinyin_after_chinese_like_flutter_corruption_tail()
    {
        const string text = "我要开始w'yao'kai'shi";
        var hit = MustFind(text);
        Assert.Equal("w'yao'kai'shi", hit.Raw);
        Assert.Equal("w'yao'kai'shi", hit.NormalizedComposition);
    }

    [Fact]
    public void Expands_when_caret_is_in_the_middle_of_the_residue()
    {
        const string text = "prefix xi'an suffix";
        var caret = text.IndexOf("xi'an", StringComparison.Ordinal) + 3; // at the apostrophe or just after xi'
        var hit = PinyinResidueScanner.Find(text, caret);
        Assert.NotNull(hit);
        Assert.Equal("xi'an", hit!.Raw);
    }

    [Fact]
    public void Prefers_current_selection_when_it_is_pinyin()
    {
        const string text = "abc xi'an def nihao";
        var start = text.IndexOf("xi'an", StringComparison.Ordinal);
        var hit = PinyinResidueScanner.Find(text, text.Length, start, 5);
        Assert.NotNull(hit);
        Assert.Equal("xi'an", hit!.Raw);
        Assert.Equal(start, hit.StartIndex);
    }

    [Fact]
    public void Normalizes_ue_and_umlaut_u()
    {
        Assert.Equal("nvhai", PinyinResidueScanner.NormalizeComposition("nühai"));
        Assert.Equal("lve", PinyinResidueScanner.NormalizeComposition("lüe"));
        Assert.Equal("nv", PinyinResidueScanner.NormalizeComposition("nV"));
    }

    [Fact]
    public void Collapses_mixed_delimiter_runs()
    {
        Assert.Equal("xi'an", PinyinResidueScanner.NormalizeComposition("xi''an"));
        Assert.Equal("xi'an", PinyinResidueScanner.NormalizeComposition("xi'\u2019an"));
        Assert.Equal("xi'an", PinyinResidueScanner.NormalizeComposition("xi` an"));
    }

    [Fact]
    public void Returns_null_for_empty_or_pure_punctuation()
    {
        Assert.Null(PinyinResidueScanner.Find("", 0));
        Assert.Null(PinyinResidueScanner.Find("'''", 3));
        Assert.Null(PinyinResidueScanner.Find("你好", 2));
        Assert.Null(PinyinResidueScanner.Find("12345", 5));
    }

    [Fact]
    public void Rejects_out_of_range_caret()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => PinyinResidueScanner.Find("ni", 5));
    }

    [Theory]
    [InlineData("搜狗 fen'hao", "fen'hao")]
    [InlineData("ms w'yao", "w'yao")]
    public void Documented_vendor_examples(string text, string expected)
    {
        var hit = MustFind(text);
        Assert.Equal(expected, hit.Raw);
    }
}
