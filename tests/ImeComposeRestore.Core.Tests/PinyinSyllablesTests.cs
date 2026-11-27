using ImeComposeRestore;
using Xunit;

namespace ImeComposeRestore.Tests;

public class PinyinSyllablesTests
{
    [Theory]
    [InlineData("ni", true)]
    [InlineData("hao", true)]
    [InlineData("zhong", true)]
    [InlineData("xian", true)]
    [InlineData("lve", true)]
    [InlineData("nve", true)]
    [InlineData("jue", true)]
    [InlineData("hello", false)]
    [InlineData("th", false)]
    public void Complete_syllables(string token, bool expected)
    {
        Assert.Equal(expected, PinyinSyllables.IsCompleteSyllable(token));
    }

    [Fact]
    public void Segments_common_phrases_and_prefixes()
    {
        Assert.True(PinyinSyllables.TrySegment("woyaokaishi", out var parts));
        Assert.Equal(new[] { "wo", "yao", "kai", "shi" }, parts);

        Assert.True(PinyinSyllables.TrySegment("zhon", out var prefix));
        Assert.Equal("zhon", prefix[^1]);

        Assert.True(PinyinSyllables.TrySegment("zgrm", out var abbr));
        Assert.True(abbr.Count >= 3);
    }
}
