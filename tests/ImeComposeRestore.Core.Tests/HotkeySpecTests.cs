using ImeComposeRestore;
using Xunit;

namespace ImeComposeRestore.Tests;

public class HotkeySpecTests
{
    [Fact]
    public void Default_is_ctrl_alt_r()
    {
        var hk = HotkeySpec.Default;
        Assert.Equal(HotkeyModifiers.Control | HotkeyModifiers.Alt, hk.Modifiers);
        Assert.Equal((byte)'R', hk.VirtualKey);
        Assert.Equal("Ctrl+Alt+R", hk.Display);
        Assert.False(HotkeySpec.IsReservedImeShortcut(hk));
    }

    [Theory]
    [InlineData("Ctrl+Alt+R")]
    [InlineData("ctrl+alt+r")]
    [InlineData("Control+Alt+P")]
    [InlineData("Ctrl+Shift+Alt+I")]
    [InlineData("Win+Ctrl+R")]
    [InlineData("Ctrl+Alt+;")]
    [InlineData("Ctrl+Alt+'")]
    [InlineData("Ctrl+Alt+F8")]
    public void Parses_valid_shortcuts(string text)
    {
        var hk = HotkeySpec.Parse(text);
        Assert.True(hk.VirtualKey != 0);
        Assert.False(HotkeySpec.IsReservedImeShortcut(hk));
    }

    [Theory]
    [InlineData("Ctrl+Space")]
    [InlineData("Win+Space")]
    [InlineData("Ctrl+Shift+Space")]
    [InlineData("Ctrl+.")]
    [InlineData("Ctrl+`")]
    [InlineData("F4")]
    public void Rejects_common_ime_shortcuts(string text)
    {
        var ex = Assert.Throws<ArgumentException>(() => HotkeySpec.Parse(text));
        Assert.Contains("冲突", ex.Message);
    }

    [Fact]
    public void Rejects_empty()
    {
        Assert.Throws<ArgumentException>(() => HotkeySpec.Parse(""));
    }
}
