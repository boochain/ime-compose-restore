# IME 组字恢复

Windows 后台小工具：中文输入法打字时窗口突然失焦，选字框没了，打了一半的拼音（有的还带着分词符）却留在输入框里。按下快捷键后，本工具会识别这些残留串，清掉已经上屏的半串，并尽量把同一串拼音重新交给当前输入法，恢复到“正在组字、可以选词”的状态。

平时在托盘里挂着，只有用到的时候才起作用。

> **请注意：** 仓库里的单元测试和 GitHub Actions 只能保证“残留串识别 + Windows 构建”是通的。开发时无法在真实的微软拼音 / 搜狗 / 微信 / 豆包 / 讯飞 / 小狼毫上按键验证。第一次使用请按 [docs/windows-verification.md](docs/windows-verification.md) 在自己的 Windows 电脑上过一遍。

## 安装

1. 打开 [Releases](https://github.com/boochain/ime-compose-restore/releases)，下载 `ImeComposeRestore-win-x64.zip`。
2. 解压到任意目录，双击 `ImeComposeRestore.exe`。无需安装，也无需管理员权限。
3. 托盘出现图标即表示已在后台运行（图标可能被收进任务栏的 `^` 里）。

需要开机自动运行时，右键托盘图标，勾选「开机启动」。

配置文件位于 `%AppData%\ImeComposeRestore\config.json`，日志在同目录 `restore.log`。

## 默认快捷键

**Ctrl+Alt+R**

已避开 `Ctrl+Space`、`Win+Space`、`Shift` 切换、`Ctrl+.`、`Ctrl+\``、小狼毫 `F4` 等常见输入法快捷键。可在配置文件里改 `hotkey`（例如 `"Ctrl+Alt+P"`），保存后重新启动程序。

也可以双击托盘图标，或用右键菜单「立即恢复」。

## 它会做什么

1. 若当前窗口里**还有未结束的组字**（IMM composition 仍在），先尝试把候选界面重新唤出，不会先删正文。
2. 否则用 UI Automation，必要时再对标准 `Edit` / `RichEdit` 发消息，读取光标附近（或当前选区）的文本。
3. 识别残留拼音，包括：
   - 普通全拼、简拼、尚未打完的音节；
   - ASCII 单引号 `'`（微软拼音、搜狗、Rime 等最常见的分词符）；
   - 微软拼音 / Rime 里用空格切开的音节（如 `xi an`），恢复时会改写成 `'`，避免 Space 变成“选中第一候选”；
   - 微软拼音可用的声调数字（如 `xi1an`）；
   - 已上屏汉字后面还剩的拼音（如 `你好ma`）。
4. 删掉这段已上屏的半串。
5. 优先调用 `ImmSetCompositionString(SCS_SETSTR)` 直接建立组字；若当前输入法不接受，再按**虚拟键**（而不是 Unicode 字符）把字母和 `'` 交给输入法。

各家输入法失焦后究竟留下什么、依据是什么、哪些查不到，见 [docs/ime-focus-loss.md](docs/ime-focus-loss.md)。

## 已知限制

- **不能对别人进程的 TSF 上下文直接 `StartComposition`。** 这是 Windows 文字服务框架的设计：组字只能由该文档里的输入法（text service）创建。外部程序只能走 IMM 兼容层或模拟按键。
- 现代 TSF 输入法可能忽略 `SCS_SETSTR`，此时会自动改走按键模拟。
- 模拟按键要求输入法处于**中文模式**。英文模式下字母会再次直接上屏。
- 小狼毫在失焦时按源码会取消并清空组字，输入框里常常什么都不剩，这时会提示找不到残留串——属于输入法自身行为。
- 微信 / 豆包 / 讯飞 Windows 端的残留形态在公开资料里查不到，是否有效必须实机确认。
- 读不到文本的控件（部分游戏、自绘窗口、少数网页框）会失败并提示，而不会盲目退格。
- 以不同完整性级别运行的进程之间，模拟按键可能被 UIPI 挡住。本程序与目标窗口请使用相同的权限级别。
- 不会恢复已经选过的候选词本身，只恢复“这串拼音正在被输入法编辑”的状态；候选排序以当前输入法为准。

## 如何自测

完整步骤和记录表见 [docs/windows-verification.md](docs/windows-verification.md)。最短路径：

1. 打开记事本或写字板，用你的主力中文输入法输入 `xi'an`，不要选字。
2. Alt+Tab 切走再切回，确认选字框消失、拼音还在。
3. 光标放在这串拼音末尾，按 `Ctrl+Alt+R`。
4. 看候选框是否回来，以及托盘通知 / `restore.log` 里的 `method=`。

## 从源码构建

需要 .NET 8 SDK。单元测试（识别与分词符）可在 Windows 或 Linux 上运行：

```bash
dotnet test ImeComposeRestore.sln -c Release
```

发布单文件 exe 请在 Windows 上：

```bash
dotnet publish src/ImeComposeRestore.App/ImeComposeRestore.App.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish
```

CI 在 `windows-latest` 上构建并测试；打 `v*` 标签会发布 Release 资产。

## 许可

MIT。调查文档中引用的第三方 issue、官方 API 说明归原作者所有。
