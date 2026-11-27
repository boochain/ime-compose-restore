using System.Runtime.InteropServices;
using System.Text;
using ImeComposeRestore;
using System.Windows.Automation;
using System.Windows.Automation.Text;

namespace ImeComposeRestore.App;

/// <summary>
/// Reads and edits text around the caret of the currently focused control.
/// Prefers UI Automation; falls back to classic Edit/RichEdit messages.
/// </summary>
internal static class CaretTextReader
{
    public static CaretTextSnapshot? Read(IntPtr hwndFocus)
    {
        try
        {
            var viaUia = ReadViaUiAutomation();
            if (viaUia is not null)
                return viaUia;
        }
        catch (Exception ex)
        {
            AppLog.Write("UIA 读取失败: " + ex.Message);
        }

        try
        {
            if (hwndFocus != IntPtr.Zero && NativeMethods.IsEditLike(NativeMethods.GetWindowClass(hwndFocus)))
                return ReadViaWin32(hwndFocus);
        }
        catch (Exception ex)
        {
            AppLog.Write("Win32 读取失败: " + ex.Message);
        }

        return null;
    }

    public static bool TryDelete(CaretTextSnapshot snapshot, PinyinResidue residue)
    {
        if (snapshot.Method == TextAccessMethod.UiAutomation)
        {
            try
            {
                if (TryDeleteViaUiAutomation(snapshot, residue))
                    return true;
            }
            catch (Exception ex)
            {
                AppLog.Write("UIA 删除失败: " + ex.Message);
            }
        }

        if (snapshot.Hwnd != IntPtr.Zero && NativeMethods.IsEditLike(NativeMethods.GetWindowClass(snapshot.Hwnd)))
        {
            try
            {
                if (TryDeleteViaWin32(snapshot, residue))
                    return true;
            }
            catch (Exception ex)
            {
                AppLog.Write("Win32 删除失败: " + ex.Message);
            }
        }

        return TryDeleteViaBackspace(snapshot, residue);
    }

    private static CaretTextSnapshot? ReadViaUiAutomation()
    {
        var focused = AutomationElement.FocusedElement;
        if (focused is null)
            return null;
        if (!focused.TryGetCurrentPattern(TextPattern.Pattern, out var raw) || raw is not TextPattern pattern)
            return null;

        var selections = pattern.GetSelection();
        var caretRange = selections is { Length: > 0 } ? selections[0] : null;
        if (caretRange is null)
        {
            var visibles = pattern.GetVisibleRanges();
            if (visibles is { Length: > 0 })
                caretRange = visibles[0];
        }

        if (caretRange is null)
            return null;

        var line = caretRange.Clone();
        line.ExpandToEnclosingUnit(TextUnit.Line);
        var lineText = line.GetText(-1) ?? "";
        if (lineText.Length == 0)
        {
            // Some controls report an empty "line"; try a slightly larger window.
            line = caretRange.Clone();
            line.ExpandToEnclosingUnit(TextUnit.Paragraph);
            lineText = line.GetText(-1) ?? "";
        }

        var prefix = line.Clone();
        prefix.MoveEndpointByRange(TextPatternRangeEndpoint.End, caretRange, TextPatternRangeEndpoint.Start);
        var prefixText = prefix.GetText(-1) ?? "";
        var caret = Math.Clamp(prefixText.Length, 0, lineText.Length);

        var selLength = 0;
        var selStart = caret;
        if (selections is { Length: > 0 })
        {
            var selected = selections[0].GetText(-1) ?? "";
            if (selected.Length > 0)
            {
                var fromLine = line.Clone();
                fromLine.MoveEndpointByRange(TextPatternRangeEndpoint.End, selections[0], TextPatternRangeEndpoint.Start);
                selStart = Math.Clamp((fromLine.GetText(-1) ?? "").Length, 0, lineText.Length);
                selLength = selected.Length;
            }
        }

        var hwnd = IntPtr.Zero;
        try
        {
            hwnd = new IntPtr(focused.Current.NativeWindowHandle);
        }
        catch
        {
            // Some UIA providers do not expose a native handle.
        }

        return new CaretTextSnapshot(lineText, caret, selStart, selLength, TextAccessMethod.UiAutomation, hwnd);
    }

    private static bool TryDeleteViaUiAutomation(CaretTextSnapshot snapshot, PinyinResidue residue)
    {
        var focused = AutomationElement.FocusedElement;
        if (focused is null)
            return false;
        if (!focused.TryGetCurrentPattern(TextPattern.Pattern, out var raw) || raw is not TextPattern pattern)
            return false;

        var selections = pattern.GetSelection();
        if (selections is not { Length: > 0 })
            return false;

        var line = selections[0].Clone();
        line.ExpandToEnclosingUnit(TextUnit.Line);
        var lineText = line.GetText(-1) ?? "";
        if (!lineText.Contains(residue.Raw, StringComparison.Ordinal))
        {
            line = selections[0].Clone();
            line.ExpandToEnclosingUnit(TextUnit.Paragraph);
            lineText = line.GetText(-1) ?? "";
        }

        if (snapshot.Text != lineText && !lineText.Contains(residue.Raw, StringComparison.Ordinal))
            return false;

        var start = lineText.IndexOf(residue.Raw, StringComparison.Ordinal);
        if (start < 0)
            start = residue.StartIndex;
        if (start < 0 || start + residue.Length > lineText.Length)
            return false;

        var range = line.Clone();
        range.MoveEndpointByRange(TextPatternRangeEndpoint.End, range, TextPatternRangeEndpoint.Start);
        if (start > 0 && range.Move(TextUnit.Character, start) != start)
        {
            // Some providers cannot move by character accurately; fall through.
        }

        range.MoveEndpointByUnit(TextPatternRangeEndpoint.End, TextUnit.Character, residue.Length);
        if (!string.Equals(range.GetText(-1), residue.Raw, StringComparison.Ordinal))
        {
            // Rebuild from line endpoints more carefully.
            range = SliceRange(line, lineText, start, residue.Length);
            if (range is null || !string.Equals(range.GetText(-1), residue.Raw, StringComparison.Ordinal))
                return false;
        }

        range.Select();
        NativeMethods.ReleaseModifierKeys();
        NativeMethods.SendVirtualKey(NativeMethods.VK_BACK, keyUp: false);
        NativeMethods.SendVirtualKey(NativeMethods.VK_BACK, keyUp: true);
        return true;
    }

    private static TextPatternRange? SliceRange(TextPatternRange line, string lineText, int start, int length)
    {
        var range = line.Clone();
        var endTrim = lineText.Length - (start + length);
        if (endTrim > 0)
            range.MoveEndpointByUnit(TextPatternRangeEndpoint.End, TextUnit.Character, -endTrim);
        if (start > 0)
            range.MoveEndpointByUnit(TextPatternRangeEndpoint.Start, TextUnit.Character, start);
        return range;
    }

    private static CaretTextSnapshot? ReadViaWin32(IntPtr hwnd)
    {
        var packed = NativeMethods.SendMessage(hwnd, NativeMethods.EM_GETSEL, IntPtr.Zero, IntPtr.Zero);
        var selStart = packed.ToInt64() & 0xFFFF;
        var selEnd = (packed.ToInt64() >> 16) & 0xFFFF;
        var caret = (int)selEnd;
        var lineIndex = NativeMethods.SendMessage(hwnd, NativeMethods.EM_LINEFROMCHAR, new IntPtr(caret), IntPtr.Zero).ToInt32();
        var lineStart = NativeMethods.SendMessage(hwnd, NativeMethods.EM_LINEINDEX, new IntPtr(lineIndex), IntPtr.Zero).ToInt32();
        if (lineStart < 0)
        {
            // Not an edit control after all — try the whole window text if short.
            return ReadWholeWindowText(hwnd, (int)selStart, (int)selEnd);
        }

        var lineLen = NativeMethods.SendMessage(hwnd, NativeMethods.EM_LINELENGTH, new IntPtr(caret), IntPtr.Zero).ToInt32();
        var lineText = GetEditLine(hwnd, lineIndex, Math.Max(lineLen, 1));
        var caretInLine = Math.Clamp(caret - lineStart, 0, lineText.Length);
        var selStartInLine = Math.Clamp((int)selStart - lineStart, 0, lineText.Length);
        var selLen = Math.Clamp((int)selEnd - (int)selStart, 0, lineText.Length - selStartInLine);
        return new CaretTextSnapshot(
            lineText, caretInLine, selStartInLine, selLen, TextAccessMethod.Win32Edit, hwnd, (int)selStart);
    }

    private static CaretTextSnapshot? ReadWholeWindowText(IntPtr hwnd, int selStart, int selEnd)
    {
        var len = NativeMethods.SendMessage(hwnd, NativeMethods.WM_GETTEXTLENGTH, IntPtr.Zero, IntPtr.Zero).ToInt32();
        if (len <= 0 || len > 1_000_000)
            return null;
        var sb = new StringBuilder(len + 1);
        NativeMethods.SendMessage(hwnd, NativeMethods.WM_GETTEXT, new IntPtr(sb.Capacity), sb);
        var text = sb.ToString();
        selStart = Math.Clamp(selStart, 0, text.Length);
        selEnd = Math.Clamp(selEnd, 0, text.Length);
        return new CaretTextSnapshot(text, selEnd, selStart, Math.Max(0, selEnd - selStart), TextAccessMethod.Win32Edit, hwnd, selStart);
    }

    private static string GetEditLine(IntPtr hwnd, int lineIndex, int lineLen)
    {
        var buffer = new char[Math.Max(lineLen, 1) + 2];
        var header = (ushort)Math.Min(buffer.Length - 1, ushort.MaxValue);
        var headerBytes = BitConverter.GetBytes(header);
        Buffer.BlockCopy(headerBytes, 0, buffer, 0, 2);
        var handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
        try
        {
            var copied = NativeMethods.SendMessage(
                hwnd, NativeMethods.EM_GETLINE, new IntPtr(lineIndex), handle.AddrOfPinnedObject()).ToInt32();
            if (copied <= 0)
                return "";
            copied = Math.Min(copied, buffer.Length);
            return new string(buffer, 0, copied).TrimEnd('\0', '\r', '\n');
        }
        finally
        {
            handle.Free();
        }
    }

    private static bool TryDeleteViaWin32(CaretTextSnapshot snapshot, PinyinResidue residue)
    {
        var packed = NativeMethods.SendMessage(snapshot.Hwnd, NativeMethods.EM_GETSEL, IntPtr.Zero, IntPtr.Zero);
        var caret = (int)((packed.ToInt64() >> 16) & 0xFFFF);
        var lineIndex = NativeMethods.SendMessage(snapshot.Hwnd, NativeMethods.EM_LINEFROMCHAR, new IntPtr(caret), IntPtr.Zero).ToInt32();
        var lineStart = NativeMethods.SendMessage(snapshot.Hwnd, NativeMethods.EM_LINEINDEX, new IntPtr(lineIndex), IntPtr.Zero).ToInt32();
        int absStart;
        int absEnd;
        if (lineStart >= 0)
        {
            absStart = lineStart + residue.StartIndex;
            absEnd = absStart + residue.Length;
        }
        else if (snapshot.DocumentSelectionStart is int doc)
        {
            absStart = residue.StartIndex;
            absEnd = absStart + residue.Length;
            _ = doc;
        }
        else
        {
            return false;
        }

        NativeMethods.SendMessage(snapshot.Hwnd, NativeMethods.EM_SETSEL, new IntPtr(absStart), new IntPtr(absEnd));
        NativeMethods.SendMessage(snapshot.Hwnd, NativeMethods.EM_REPLACESEL, new IntPtr(1), "");
        return true;
    }

    private static bool TryDeleteViaBackspace(CaretTextSnapshot snapshot, PinyinResidue residue)
    {
        // Move to the end of the residue, then backspace one code unit at a time.
        var caret = snapshot.CaretOffset;
        var end = residue.EndIndex;
        var start = residue.StartIndex;
        if (caret < start || caret > end)
        {
            // Caret is not inside the residue; skip moving (too easy to typo elsewhere).
            if (caret != end)
                return false;
        }

        NativeMethods.ReleaseModifierKeys();
        var right = Math.Max(0, end - caret);
        for (var i = 0; i < right; i++)
            Tap(0x27); // VK_RIGHT
        var count = residue.Length;
        for (var i = 0; i < count; i++)
        {
            NativeMethods.SendVirtualKey(NativeMethods.VK_BACK, keyUp: false);
            NativeMethods.SendVirtualKey(NativeMethods.VK_BACK, keyUp: true);
            Thread.Sleep(4);
        }

        return true;
    }

    private static void Tap(byte vk)
    {
        NativeMethods.SendVirtualKey(vk, keyUp: false);
        NativeMethods.SendVirtualKey(vk, keyUp: true);
    }
}
