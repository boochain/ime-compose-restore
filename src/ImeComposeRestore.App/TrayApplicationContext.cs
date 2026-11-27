using ImeComposeRestore;

namespace ImeComposeRestore.App;

internal sealed class TrayApplicationContext : ApplicationContext
{
    public const int HotkeyId = 0x1CE0;

    private readonly NotifyIcon _icon;
    private readonly ToolStripMenuItem _startupItem;
    private readonly ToolStripMenuItem _notifyItem;
    private AppConfig _config;
    private HotkeySpec _hotkey;
    private HiddenHotkeyForm? _hotkeyForm;

    public TrayApplicationContext()
    {
        _config = AppConfig.Load();
        AppLog.Enabled = _config.LogEnabled;
        _hotkey = LoadHotkey(_config);

        _startupItem = new ToolStripMenuItem("开机启动")
        {
            CheckOnClick = true,
            Checked = _config.StartWithWindows,
        };
        _startupItem.CheckedChanged += (_, _) =>
        {
            _config.StartWithWindows = _startupItem.Checked;
            _config.Save();
        };

        _notifyItem = new ToolStripMenuItem("恢复后显示通知")
        {
            CheckOnClick = true,
            Checked = _config.ShowNotification,
        };
        _notifyItem.CheckedChanged += (_, _) =>
        {
            _config.ShowNotification = _notifyItem.Checked;
            _config.Save();
        };

        var menu = new ContextMenuStrip();
        menu.Items.Add(new ToolStripMenuItem($"立即恢复（{_hotkey.Display}）", null, (_, _) => TriggerRestore()));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(_startupItem);
        menu.Items.Add(_notifyItem);
        menu.Items.Add(new ToolStripMenuItem("打开配置文件", null, (_, _) => OpenConfig()));
        menu.Items.Add(new ToolStripMenuItem("打开日志", null, (_, _) => OpenLog()));
        menu.Items.Add(new ToolStripMenuItem("关于 / 使用说明", null, (_, _) => ShowAbout()));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(new ToolStripMenuItem("退出", null, (_, _) => ExitThread()));

        _icon = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            Visible = true,
            Text = $"IME 组字恢复  {_hotkey.Display}",
            ContextMenuStrip = menu,
        };
        _icon.DoubleClick += (_, _) => TriggerRestore();

        RegisterHotkey();
        AppLog.Write("已启动，快捷键 " + _hotkey.Display);
    }

    private static HotkeySpec LoadHotkey(AppConfig config)
    {
        try
        {
            return HotkeySpec.Parse(config.Hotkey);
        }
        catch (Exception ex)
        {
            AppLog.Write("配置中的快捷键无效，已回退到默认值: " + ex.Message);
            config.Hotkey = AppConfig.DefaultHotkey;
            config.Save();
            return HotkeySpec.Default;
        }
    }

    private void RegisterHotkey()
    {
        _hotkeyForm = new HiddenHotkeyForm(this);
        if (!NativeMethods.RegisterHotKey(_hotkeyForm.Handle, HotkeyId, _hotkey.NativeModifiers, _hotkey.VirtualKey))
        {
            _icon.ShowBalloonTip(
                4000,
                "IME 组字恢复",
                $"无法注册快捷键 {_hotkey.Display}（可能已被占用）。可用托盘菜单「立即恢复」，或编辑配置文件后重启。",
                ToolTipIcon.Warning);
            AppLog.Write("RegisterHotKey 失败: " + _hotkey.Display);
        }
    }

    internal void TriggerRestore()
    {
        try
        {
            var result = CompositionRestorer.Restore(_config);
            AppLog.Write($"结果 success={result.Success} method={result.Method} {result.Message}");
            if (_config.ShowNotification)
            {
                _icon.ShowBalloonTip(
                    2500,
                    result.Success ? "IME 组字恢复" : "IME 组字恢复未完成",
                    result.Message,
                    result.Success ? ToolTipIcon.Info : ToolTipIcon.Warning);
            }
        }
        catch (Exception ex)
        {
            AppLog.Write("恢复过程异常: " + ex);
            if (_config.ShowNotification)
            {
                _icon.ShowBalloonTip(4000, "IME 组字恢复出错", ex.Message, ToolTipIcon.Error);
            }
        }
    }

    private void OpenConfig()
    {
        _config.Save();
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = AppConfig.ConfigPath,
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show("无法打开配置文件：" + ex.Message, "IME 组字恢复");
        }
    }

    private void OpenLog()
    {
        AppLog.Write("打开日志");
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = AppConfig.LogPath,
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show("无法打开日志：" + ex.Message, "IME 组字恢复");
        }
    }

    private static void ShowAbout()
    {
        MessageBox.Show(
            "IME 组字恢复  v0.1.0\n\n" +
            "窗口突然失焦后，输入法选字框消失，打了一半的拼音（可能含分词符）常常会留在输入框里。\n" +
            "按下快捷键后，本工具会识别这些残留串、删掉已上屏的半串，并尽量把它重新交给当前输入法，恢复正在组字、选词的状态。\n\n" +
            "默认快捷键：Ctrl+Alt+R（可在配置文件里修改）。\n" +
            "详细调查、限制与自测步骤见程序附带的 README 与 docs。\n\n" +
            "本程序平时在后台运行。首次在真实输入法上的效果需要在 Windows 上按文档自测。",
            "关于 IME 组字恢复",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }

    protected override void ExitThreadCore()
    {
        if (_hotkeyForm is not null)
        {
            NativeMethods.UnregisterHotKey(_hotkeyForm.Handle, HotkeyId);
            _hotkeyForm.Close();
            _hotkeyForm.Dispose();
        }

        _icon.Visible = false;
        _icon.Dispose();
        base.ExitThreadCore();
    }
}

/// <summary>
/// Message-only window that receives WM_HOTKEY without stealing focus.
/// </summary>
internal sealed class HiddenHotkeyForm : Form
{
    private readonly TrayApplicationContext _owner;

    public HiddenHotkeyForm(TrayApplicationContext owner)
    {
        _owner = owner;
        ShowInTaskbar = false;
        FormBorderStyle = FormBorderStyle.FixedToolWindow;
        Opacity = 0;
        Size = new Size(0, 0);
        StartPosition = FormStartPosition.Manual;
        Location = new Point(-32000, -32000);
        Text = "IME 组字恢复";
    }

    protected override void SetVisibleCore(bool value) => base.SetVisibleCore(false);

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == NativeMethods.WM_HOTKEY && m.WParam.ToInt32() == TrayApplicationContext.HotkeyId)
        {
            BeginInvoke(_owner.TriggerRestore);
            return;
        }

        base.WndProc(ref m);
    }
}
