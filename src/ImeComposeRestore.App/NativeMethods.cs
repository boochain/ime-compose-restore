using System.Runtime.InteropServices;
using System.Text;

namespace ImeComposeRestore.App;

internal static class NativeMethods
{
    public const int WM_HOTKEY = 0x0312;
    public const int HWND_MESSAGE = -3;

    public const uint INPUT_KEYBOARD = 1;
    public const uint KEYEVENTF_KEYUP = 0x0002;
    public const uint KEYEVENTF_EXTENDEDKEY = 0x0001;
    public const uint KEYEVENTF_SCANCODE = 0x0008;

    public const uint MAPVK_VK_TO_VSC = 0;

    public const int WM_GETTEXT = 0x000D;
    public const int WM_GETTEXTLENGTH = 0x000E;
    public const int EM_GETSEL = 0x00B0;
    public const int EM_SETSEL = 0x00B1;
    public const int EM_REPLACESEL = 0x00C2;
    public const int EM_LINEFROMCHAR = 0x00C9;
    public const int EM_LINEINDEX = 0x00BB;
    public const int EM_LINELENGTH = 0x00C1;
    public const int EM_GETLINE = 0x00C4;
    public const int WM_CHAR = 0x0102;

    public const uint SCS_SETSTR = 0x0009;
    public const uint SCS_SETRECONVERTSTRING = 0x00010000;
    public const uint GCS_COMPSTR = 0x0008;
    public const uint GCS_RESULTSTR = 0x0800;
    public const uint NI_OPENCANDIDATE = 0x0010;
    public const uint NI_COMPOSITIONSTR = 0x0015;
    public const uint CPS_CANCEL = 0x0004;

    public const byte VK_BACK = 0x08;
    public const byte VK_SHIFT = 0x10;
    public const byte VK_CONTROL = 0x11;
    public const byte VK_MENU = 0x12;

    [DllImport("user32.dll")]
    public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll")]
    public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    [DllImport("user32.dll")]
    public static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    public static extern IntPtr GetFocus();

    [DllImport("user32.dll")]
    public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("kernel32.dll")]
    public static extern uint GetCurrentThreadId();

    [DllImport("user32.dll")]
    public static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

    [DllImport("user32.dll")]
    public static extern bool GetGUIThreadInfo(uint idThread, ref GUITHREADINFO pgui);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, StringBuilder lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, string lParam);

    [DllImport("user32.dll")]
    public static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    [DllImport("user32.dll")]
    public static extern uint MapVirtualKey(uint uCode, uint uMapType);

    [DllImport("user32.dll")]
    public static extern short GetKeyState(int nVirtKey);

    [DllImport("imm32.dll")]
    public static extern IntPtr ImmGetContext(IntPtr hWnd);

    [DllImport("imm32.dll")]
    public static extern bool ImmReleaseContext(IntPtr hWnd, IntPtr hIMC);

    [DllImport("imm32.dll", CharSet = CharSet.Unicode)]
    public static extern bool ImmSetCompositionStringW(
        IntPtr hIMC, uint dwIndex, byte[]? lpComp, uint dwCompLen, byte[]? lpRead, uint dwReadLen);

    [DllImport("imm32.dll", CharSet = CharSet.Unicode)]
    public static extern int ImmGetCompositionStringW(IntPtr hIMC, uint dwIndex, byte[]? lpBuf, uint dwBufLen);

    [DllImport("imm32.dll")]
    public static extern bool ImmNotifyIME(IntPtr hIMC, uint dwAction, uint dwIndex, uint dwValue);

    [DllImport("imm32.dll")]
    public static extern bool ImmGetOpenStatus(IntPtr hIMC);

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left, Top, Right, Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct GUITHREADINFO
    {
        public int cbSize;
        public int flags;
        public IntPtr hwndActive;
        public IntPtr hwndFocus;
        public IntPtr hwndCapture;
        public IntPtr hwndMenuOwner;
        public IntPtr hwndMoveSize;
        public IntPtr hwndCaret;
        public RECT rcCaret;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct INPUT
    {
        public uint type;
        public InputUnion U;
    }

    [StructLayout(LayoutKind.Explicit)]
    public struct InputUnion
    {
        [FieldOffset(0)] public KEYBDINPUT ki;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    public static IntPtr GetFocusedHandle()
    {
        var fg = GetForegroundWindow();
        if (fg == IntPtr.Zero)
            return IntPtr.Zero;

        var threadId = GetWindowThreadProcessId(fg, out _);
        var info = new GUITHREADINFO { cbSize = Marshal.SizeOf<GUITHREADINFO>() };
        if (GetGUIThreadInfo(threadId, ref info))
        {
            if (info.hwndFocus != IntPtr.Zero)
                return info.hwndFocus;
            if (info.hwndCaret != IntPtr.Zero)
                return info.hwndCaret;
        }

        var current = GetCurrentThreadId();
        if (current != threadId)
            AttachThreadInput(current, threadId, true);
        try
        {
            var focus = GetFocus();
            return focus != IntPtr.Zero ? focus : fg;
        }
        finally
        {
            if (current != threadId)
                AttachThreadInput(current, threadId, false);
        }
    }

    public static string GetWindowClass(IntPtr hwnd)
    {
        var sb = new StringBuilder(256);
        _ = GetClassName(hwnd, sb, sb.Capacity);
        return sb.ToString();
    }

    public static bool IsEditLike(string className)
    {
        if (string.IsNullOrEmpty(className))
            return false;
        return className.Equals("Edit", StringComparison.OrdinalIgnoreCase)
               || className.Contains("RichEdit", StringComparison.OrdinalIgnoreCase)
               || className.Contains("RichText", StringComparison.OrdinalIgnoreCase)
               || className.Equals("Scintilla", StringComparison.OrdinalIgnoreCase)
               || className.StartsWith("WindowsForms10.EDIT", StringComparison.OrdinalIgnoreCase)
               || className.StartsWith("WindowsForms10.RichEdit", StringComparison.OrdinalIgnoreCase);
    }

    public static void SendVirtualKey(byte vk, bool keyUp)
    {
        var scan = (ushort)MapVirtualKey(vk, MAPVK_VK_TO_VSC);
        var input = new INPUT
        {
            type = INPUT_KEYBOARD,
            U = new InputUnion
            {
                ki = new KEYBDINPUT
                {
                    wVk = vk,
                    wScan = scan,
                    dwFlags = keyUp ? KEYEVENTF_KEYUP : 0,
                    time = 0,
                    dwExtraInfo = IntPtr.Zero,
                },
            },
        };
        SendInput(1, new[] { input }, Marshal.SizeOf<INPUT>());
    }

    public static void ReleaseModifierKeys()
    {
        // The hotkey (Ctrl+Alt+…) may still be physically down when we start
        // simulating keys. Release them so the IME sees plain letter keys.
        foreach (var vk in new byte[] { VK_CONTROL, VK_MENU, VK_SHIFT })
        {
            if ((GetKeyState(vk) & 0x8000) != 0)
                SendVirtualKey(vk, keyUp: true);
        }
    }
}
