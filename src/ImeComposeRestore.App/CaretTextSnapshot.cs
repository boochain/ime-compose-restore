namespace ImeComposeRestore.App;

public enum TextAccessMethod
{
    None,
    UiAutomation,
    Win32Edit,
}

public sealed record CaretTextSnapshot(
    string Text,
    int CaretOffset,
    int SelectionStart,
    int SelectionLength,
    TextAccessMethod Method,
    IntPtr Hwnd,
    int? DocumentSelectionStart = null);
