using ImeComposeRestore;
using Xunit;

namespace ImeComposeRestore.Tests;

public class RestoreSequenceTests
{
    [Fact]
    public void Composition_string_uses_ascii_apostrophe()
    {
        Assert.Equal("xi'an", RestoreSequence.CompositionString("xi\u2019an"));
        Assert.Equal("ni'hao", RestoreSequence.CompositionString("ni'hao"));
        Assert.Equal("xi'an", RestoreSequence.CompositionString("xi an"));
        Assert.Equal("w'yao'kai'shi", RestoreSequence.CompositionString("w'yao'kai'shi"));
    }

    [Fact]
    public void Keystrokes_for_xi_an_use_apostrophe_key_not_space()
    {
        var keys = RestoreSequence.ToKeyStrokes("xi an");
        Assert.Equal(
            new[]
            {
                new KeyStroke(RestoreSequence.VkA + ('x' - 'a'), false),
                new KeyStroke(RestoreSequence.VkA + ('i' - 'a'), false),
                new KeyStroke(RestoreSequence.VkOem7, false),
                new KeyStroke(RestoreSequence.VkA + 0, false),
                new KeyStroke(RestoreSequence.VkA + ('n' - 'a'), false),
            },
            keys);
    }

    [Fact]
    public void Keystrokes_cover_letters_apostrophes_and_tone_digits()
    {
        var keys = RestoreSequence.ToKeyStrokes("xi1an");
        Assert.Equal(5, keys.Count);
        Assert.Equal(RestoreSequence.Vk0 + 1, keys[2].VirtualKey);
        Assert.DoesNotContain(keys, k => k.Shift);
    }

    [Fact]
    public void Umlaut_u_is_sent_as_v()
    {
        var keys = RestoreSequence.ToKeyStrokes("nü");
        Assert.Equal(2, keys.Count);
        Assert.Equal(RestoreSequence.VkA + ('n' - 'a'), keys[0].VirtualKey);
        Assert.Equal(RestoreSequence.VkA + ('v' - 'a'), keys[1].VirtualKey);
    }

    [Fact]
    public void Empty_or_punctuation_only_yields_no_keys()
    {
        Assert.Empty(RestoreSequence.ToKeyStrokes(""));
        Assert.Empty(RestoreSequence.ToKeyStrokes("'''"));
    }
}
