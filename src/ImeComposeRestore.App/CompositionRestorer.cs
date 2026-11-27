using System.Text;
using ImeComposeRestore;

namespace ImeComposeRestore.App;

internal enum RestoreMethod
{
    None,
    ExistingCompositionRefresh,
    ImmSetCompositionString,
    KeySimulation,
}

internal sealed record RestoreOutcome(
    bool Success,
    RestoreMethod Method,
    string Message,
    string? Residue = null,
    string? LiveComposition = null);

/// <summary>
/// Orchestrates leftover-pinyin detection, deletion, and IME composition rebuild.
/// </summary>
internal static class CompositionRestorer
{
    public static RestoreOutcome Restore(AppConfig config)
    {
        var hwnd = NativeMethods.GetFocusedHandle();
        if (hwnd == IntPtr.Zero)
            return new RestoreOutcome(false, RestoreMethod.None, "找不到当前焦点窗口。");

        NativeMethods.ReleaseModifierKeys();

        var live = ReadLiveComposition(hwnd);
        if (!string.IsNullOrEmpty(live))
        {
            AppLog.Write($"仍有活动组字串: {live}");
            if (TrySetComposition(hwnd, live) || TryOpenCandidate(hwnd))
            {
                return new RestoreOutcome(
                    true, RestoreMethod.ExistingCompositionRefresh,
                    "检测到尚未结束的组字，已尝试重新显示候选。", live, live);
            }
        }

        var snapshot = CaretTextReader.Read(hwnd);
        if (snapshot is null)
        {
            return new RestoreOutcome(
                false, RestoreMethod.None,
                "无法读取光标附近的文本（当前控件可能不提供 UI Automation 或标准编辑消息）。");
        }

        AppLog.Write($"读取文本 method={snapshot.Method} caret={snapshot.CaretOffset} text={Preview(snapshot.Text)}");

        var residue = PinyinResidueScanner.Find(
            snapshot.Text, snapshot.CaretOffset, snapshot.SelectionStart, snapshot.SelectionLength);
        if (residue is null)
        {
            return new RestoreOutcome(false, RestoreMethod.None, "光标附近没有识别到残留拼音串。");
        }

        AppLog.Write($"识别残留: raw={residue.Raw} norm={residue.NormalizedComposition} likely={residue.LooksLikePinyin}");

        if (!CaretTextReader.TryDelete(snapshot, residue))
        {
            return new RestoreOutcome(
                false, RestoreMethod.None,
                $"已识别到残留「{residue.Raw}」，但未能从输入框中删除，已中止以免重复插入。",
                residue.Raw);
        }

        // Give the host a moment to commit the deletion before we inject keys.
        Thread.Sleep(30);

        var composition = RestoreSequence.CompositionString(residue.Raw);
        if (string.IsNullOrEmpty(composition))
        {
            return new RestoreOutcome(false, RestoreMethod.None, "残留串规范化后为空。", residue.Raw);
        }

        if (config.PreferImmApi && TrySetComposition(hwnd, composition))
        {
            TryOpenCandidate(hwnd);
            return new RestoreOutcome(
                true, RestoreMethod.ImmSetCompositionString,
                $"已用 IMM SCS_SETSTR 重建组字：{composition}",
                residue.Raw);
        }

        var keys = RestoreSequence.ToKeyStrokes(residue.Raw);
        if (keys.Count == 0)
        {
            return new RestoreOutcome(false, RestoreMethod.None, "没有可模拟的按键。", residue.Raw);
        }

        SendKeysThroughIme(keys, Math.Clamp(config.KeyDelayMilliseconds, 0, 80));
        return new RestoreOutcome(
            true, RestoreMethod.KeySimulation,
            $"已通过模拟按键把「{composition}」重新交给当前输入法（若输入法处于英文模式，候选不会出现）。",
            residue.Raw);
    }

    internal static string? ReadLiveComposition(IntPtr hwnd)
    {
        var himc = NativeMethods.ImmGetContext(hwnd);
        if (himc == IntPtr.Zero)
            return null;
        try
        {
            var size = NativeMethods.ImmGetCompositionStringW(himc, NativeMethods.GCS_COMPSTR, null, 0);
            if (size <= 0)
                return null;
            var buf = new byte[size];
            var written = NativeMethods.ImmGetCompositionStringW(himc, NativeMethods.GCS_COMPSTR, buf, (uint)buf.Length);
            if (written <= 0)
                return null;
            return Encoding.Unicode.GetString(buf, 0, written);
        }
        finally
        {
            NativeMethods.ImmReleaseContext(hwnd, himc);
        }
    }

    internal static bool TrySetComposition(IntPtr hwnd, string composition)
    {
        var himc = NativeMethods.ImmGetContext(hwnd);
        if (himc == IntPtr.Zero)
        {
            AppLog.Write("ImmGetContext 返回空，无法使用 SCS_SETSTR。");
            return false;
        }

        try
        {
            var bytes = Encoding.Unicode.GetBytes(composition);
            var ok = NativeMethods.ImmSetCompositionStringW(
                himc, NativeMethods.SCS_SETSTR, bytes, (uint)bytes.Length, null, 0);
            if (!ok)
            {
                AppLog.Write("ImmSetCompositionString(SCS_SETSTR) 失败。");
                return false;
            }

            Thread.Sleep(20);
            var size = NativeMethods.ImmGetCompositionStringW(himc, NativeMethods.GCS_COMPSTR, null, 0);
            AppLog.Write($"SCS_SETSTR 后组字长度={size}");
            return size > 0;
        }
        finally
        {
            NativeMethods.ImmReleaseContext(hwnd, himc);
        }
    }

    internal static bool TryOpenCandidate(IntPtr hwnd)
    {
        var himc = NativeMethods.ImmGetContext(hwnd);
        if (himc == IntPtr.Zero)
            return false;
        try
        {
            return NativeMethods.ImmNotifyIME(himc, NativeMethods.NI_OPENCANDIDATE, 0, 0);
        }
        finally
        {
            NativeMethods.ImmReleaseContext(hwnd, himc);
        }
    }

    internal static void SendKeysThroughIme(IReadOnlyList<KeyStroke> keys, int delayMs)
    {
        NativeMethods.ReleaseModifierKeys();
        foreach (var key in keys)
        {
            if (key.Shift)
                NativeMethods.SendVirtualKey(NativeMethods.VK_SHIFT, keyUp: false);

            NativeMethods.SendVirtualKey(key.VirtualKey, keyUp: false);
            NativeMethods.SendVirtualKey(key.VirtualKey, keyUp: true);

            if (key.Shift)
                NativeMethods.SendVirtualKey(NativeMethods.VK_SHIFT, keyUp: true);

            if (delayMs > 0)
                Thread.Sleep(delayMs);
        }
    }

    private static string Preview(string text)
    {
        var oneLine = text.Replace('\r', ' ').Replace('\n', ' ');
        return oneLine.Length <= 80 ? oneLine : oneLine[..80] + "…";
    }
}
