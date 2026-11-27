# Windows 中文输入法失焦后的残留串调查

调查目的：弄清窗口失焦、选字框消失之后，输入框里留下的究竟是什么样的拼音串（是否含分词符、是否仍是活动组字、如何从光标附近取串），再据此设计恢复工具。

**范围**：微软拼音、搜狗、微信输入法、豆包输入法、讯飞输入法、小狼毫（Rime / Weasel），均指 Windows 桌面端。

**方法**：查阅官方文档、Win32 / TSF 行为说明、输入法源码和可复核的社区报告。本环境是 Linux，**没有在真实 Windows 输入法上复现**；查不到的事项会明确写出，不以猜测冒充实测。

**调查日期**：2026-11-27。

---

## 1. 先要区分的三种“留下”

失焦之后，表面上“拼音还留在输入框里”，至少有三种不同的底层状态：

1. **组字被提交（commit / complete）**  
   输入法或宿主把预编辑串当成结果串送进文档，成为普通文本。此时 **TSF / IMM 活动组字通常已经结束**，`ImmGetCompositionString(GCS_COMPSTR)` 应为空，光标附近看到的是已经上屏的拉丁字母（常带 `'`）。  
   官方依据：编辑控件可用 `EM_SETIMESTATUS` 设置 `EIMES_COMPLETECOMPSTRKILLFOCUS`，在 `WM_KILLFOCUS` 时**完成（上屏）**当前组字串。[Microsoft Learn: EM_SETIMESTATUS](https://learn.microsoft.com/en-us/windows/win32/controls/em-setimestatus)

2. **组字被取消（cancel / abort）**  
   预编辑从文档中被清掉，选字框关闭，输入框里**不应留下**那串拼音。  
   官方依据：同一消息的 `EIMES_CANCELCOMPSTRINFOCUS` 会在获得焦点时取消组字。小狼毫 TSF 在失去前台焦点时则主动 `_AbortComposition()` 并把 composition range 写成空串（见第 7 节）。

3. **组字会话残留或损坏**  
   界面上仍可能看到带下划线的预编辑字母，但候选框没了，空格无法出字。这更接近“IME 会话失效”，而不是“拼音已作为普通文本上屏”。社区在小狼毫、微软拼音、Flutter 宿主里都有此类报告（见各节），**不能假定**此时文档里一定有可复制的残留串。

默认的标准 Win32 编辑框：**不会**在失焦时自动完成组字（`EIMES_COMPLETECOMPSTRKILLFOCUS` 需显式打开）。实际行为因此高度依赖宿主（记事本、Office、Chrome、VS Code、游戏、自绘控件）以及输入法自己的“失焦提交 / 防丢字”策略。同一串拼音，在 A 程序里会留下 `xi'an`，在 B 程序里可能被清空。

---

## 2. 分词符（隔音符）对照

下面只记录**有依据**的符号。工具识别时会对这些符号做兼容，并把它们规范成 ASCII `'` 再交给输入法。

| 符号 | 码位 | 有依据的用途 | 依据 |
| --- | --- | --- | --- |
| `'` | U+0027 | 最常见的音节切分符；失焦/中断组字后也常随拼音一起上屏 | 微软拼音帮助（空格或单引号作音节切分符）；搜狗官网 FAQ 提到组字区出现分隔符“‘”；Rime `luna_pinyin` 默认 `delimiter: " '"`；Godot #110138 在微软拼音与搜狗下观察到上屏串如 `ni'hao`、`fen'hao`、`w'w'w'w`、`w'yao'kai'shi` 一类形态 |
| 空格 | U+0020 | 微软拼音官方把空格也列为音节切分符（例：`xi an`、`pi ao`）；Rime 默认分隔符包含空格 | 微软拼音帮助「音节切分符」；[luna_pinyin.schema.yaml](https://github.com/rime/rime-luna-pinyin/blob/master/luna_pinyin.schema.yaml) |
| `’` `‘` | U+2019 / U+2018 | 未见某家官方把弯引号定为分词符。纳入识别是为防止排版引号混入后漏检，恢复时规范成 U+0027 | 无官方依据，属兼容处理 |
| `` ` `` | U+0060 | 现行各家官方文档**未**把它列为 Windows 端分词符。旧版微软拼音双拼曾用 `` ` `` 标记零声母显示（如输入 `oa` 显示 `` `a``） | 旧版微软拼音帮助「双拼输入 / 零声母」。不能据此断言现代输入法失焦后会留下反引号 |
| 数字 `1`–`5` | — | 微软拼音支持用声调兼作切分（如 `xi1an`） | 微软拼音帮助：可以键入音调来代替音节切分符 |

手机输入法里有时用 `` ` `` 或屏幕上的“分词”键，**不能**直接当成 Windows 桌面端的残留形态。

---

## 3. 微软自带拼音

### 分词符

官方帮助写明：微软拼音使用空格或单引号 `'` 作为音节切分符，也可以用音调代替。典型例子：希望得到“西安”时输入 `xi an` 或 `xi'an`（以及 `xi1an` 这类带调形式）。见早期随输入法提供的帮助（「音节切分符」一节）及百科对同一帮助文本的转述。Microsoft Q&A（Win8.1）亦确认按引号键（分号右侧）可手动输入分隔符，组字区可见 `k's'h'y'z` 这样的简拼切分。

现代 Windows 10/11「微软拼音」仍使用 `'` 作为预编辑中的音节分隔。可复核的第三方观察：

- Flutter 引擎讨论在 Microsoft Pinyin 下记录到组字更新里出现 `w'yao'kai'shi` 这样带 `'` 的串（[flutter/flutter#191196](https://github.com/flutter/flutter/issues/191196)）。
- Godot 编辑器 issue 在 **Microsoft Pinyin / Sogou Pinyin**、Windows 10/11 下记录：组字被鼠标点击或焦点变化打断后，预编辑里的 `'`（U+0027）会随拉丁字母一起作为结果串上屏，例如 `ni'hao`、`fen'hao`（[godot#110138](https://github.com/godotengine/godot/issues/110138)）。评论还提到：在 VS Code 里点击后“不会清空，而是保持之前带有单引号的上屏”。

### 失焦后是否仍有 TSF/IMM composition

未找到微软官方说明“失焦一定提交”或“失焦一定取消”。能够确定的是：

- 若宿主设置了 `EIMES_COMPLETECOMPSTRKILLFOCUS`，标准编辑框会在失焦时**完成**组字，拼音（含 `'`）成为普通文本，活动组字结束。
- 若组字被取消，拼音不应留下。
- 社区另有“切换窗口后微软拼音候选异常 / 无法打字”的报告（[Microsoft Q&A](https://learn.microsoft.com/zh-cn/answers/questions/2284471/question-2284471)、[appinn](https://meta.appinn.net/t/topic/72371)），描述的是输入法状态损坏，**不是**残留拼音串的形态，不能用来推断上屏内容。

### 光标附近如何取串

组字已提交时：残留是文档中的普通文本，通常紧贴光标左侧（用户正在输入），可能是纯拼音 `ni'hao`，也可能跟在已上屏汉字后面（例如 `你好ma`、`我要开始w'yao'kai'shi`）。应从光标（或当前选区）向两侧扩展字母、`'`、合法声调数字，并在确认两侧都是拼音音节时才跨越空格，以免把英文句子整行吃进去。

组字仍活动时：应先读 `GCS_COMPSTR` / TSF composition range，而不是把它当成已提交文本删掉再打一遍。

---

## 4. 搜狗输入法

### 分词符

搜狗官网 FAQ《打字时每隔 2 个字母就显示分隔符“‘”》把这种分隔符出现解释为误开了双拼，可见**组字区使用单引号形态的分隔符**。[pinyin.sogou.com/help.php?list=10&q=3](https://pinyin.sogou.com/help.php?list=10&q=3)

Godot #110138 在测试环境中明确包含 **Sogou Pinyin**，并给出中断组字后上屏 `ni'hao` / `fen'hao` 的例子。因此：搜狗在“组字被提交或泄漏为结果串”时，留下的同样是**拉丁拼音 + ASCII `'`**，而不是中文弯引号或空格（该 issue 未描述空格分词的上屏形态）。

### 失焦后留下什么

**未在搜狗官网帮助中心检索到**名为“失焦提交 / 防丢字”的正式条目。部分非官方博客提到高级设置里有类似开关，**不能当作官方依据**，本调查不加采用。

能采用的是：当组字因鼠标点击或焦点变化被结束，且宿主把 `GCS_RESULTSTR` 写入文档时，搜狗与微软拼音一样，可能留下带 `'` 的拼音串（Godot 实测报告）。至于搜狗是否在所有窗口失焦路径上都会提交、或可被设置关闭，**查不到官方说明，未做实机验证**。

活动组字是否仍在：查不到搜狗在失焦时调用 cancel 还是 complete 的公开规范。工具必须同时处理“普通文本残留”和“仍有 GCS_COMPSTR”两种情况。

---

## 5. 微信输入法（Windows）

[微信输入法官网](https://z.weixin.qq.com/) 介绍了 Windows 版本与候选选择键（分号 / 引号选第 2、第 3 候选等），但 **未找到 Windows 端官方文档说明**：

- 拼音分词 / 隔音符用哪个键、组字区如何显示；
- 窗口失焦后预编辑是提交、取消，还是保持；
- 残留串里会不会出现 `'`、空格或其他符号。

移动端“分词键”的说明（例如点击“词”）**不能**外推为 Windows 残留形态。

**结论：Windows 微信输入法失焦残留串的具体形态，查不到可引用的官方或可复核的桌面端报告。** 工具仍按通用规则识别拉丁字母 + 常见分词符；是否对微信有效，必须在实机上验证。

---

## 6. 豆包输入法（Windows）

[豆包输入法](https://shurufa.doubao.com/) 与第三方评测（如介绍 Windows 版新增双拼 / 五笔）都没有写出隔音符或失焦提交规则。

**结论：分词符形式、失焦后是否上屏拼音、是否残留 TSF/IMM 组字，均查不到。** 不作猜测。工具用同一套通用识别，实机结果记入验证记录。

---

## 7. 讯飞输入法（Windows）

未找到讯飞 Windows 版官方用户手册中关于隔音符或失焦提交的条目（检索到的“用户手册”转载页也未列出该项）。V2EX 等讨论里，讯飞带分词功能的截图与说明**主要针对手机键盘**，不能当作 Windows 桌面残留串证据。

**结论：Windows 讯飞输入法失焦后留下何种拼音串，查不到可引用的桌面端资料。** 处理方式同第 5、6 节。

---

## 8. 小狼毫（Rime / Weasel）

### 分词符

Rime 方案可配置。官方主流方案「朙月拼音」默认：

```yaml
speller:
  delimiter: " '"
```

即**空格和单引号**都是音节分隔符（[rime-luna-pinyin / luna_pinyin.schema.yaml](https://github.com/rime/rime-luna-pinyin/blob/master/luna_pinyin.schema.yaml)）。用户自定义方案可能改成其他字符；工具无法在不读用户方案的情况下枚举全部可能。

预编辑（preedit）还可以按 `preedit_format` 显示成“汉字+剩余拼音”等样式。失焦后若发生提交，残留形态会受方案和宿主影响。这一点没有针对“失焦提交结果”的官方样例。

### 失焦时源码在做什么（现行 TSF 实现）

现行 weasel 仓库以 `WeaselTSF` 为主（已无旧的 `WeaselIME` 目录）。`WeaselTSF/KeyEventSink.cpp` 中：

```cpp
STDMETHODIMP WeaselTSF::OnSetFocus(BOOL fForeground) {
  if (fForeground)
    m_client.FocusIn();
  else {
    m_client.FocusOut();
    _AbortComposition();
  }
  return S_OK;
}
```

`_AbortComposition()`（`WeaselTSF/Composition.cpp`）会 `m_client.ClearComposition()`，并在仍有 composition 时 `_EndComposition(..., clear)`。`CEndCompositionEditSession` 在 `clear` 为真时对 composition range 执行 `SetText(..., L"", 0)`，然后 `EndComposition`，并结束候选 UI。

因此，**按源码设计**，失去 TSF 前台焦点时小狼毫是**取消并清空组字**，而不是把 `ni'hao` 提交进文档。若宿主配合正确，输入框里往往**没有**可恢复的拼音残留。

### 与社区现象的差别

[weasel#1445](https://github.com/rime/weasel/issues/1445) 等 issue 描述：切换应用后候选框消失，数字键选词失灵；有人看到“仍能显示输入中的字符（即有下划线的英文字母），但按空格后变成英文字符串”。这属于会话/候选 UI 异常，**不是**一份“失焦后标准残留串”说明书。其中“下划线英文字母”说明有时预编辑还在文档里，但输入法已无法出字。

另：[weasel#342](https://github.com/rime/weasel/issues/342) 与维护者说明：较新 nightly 在 Ctrl+Space 关闭键盘时会清空 composition。这与“窗口失焦”是不同路径。

**结论**：小狼毫分词符有方案依据（默认空格 + `'`）。失焦后“留下什么样的拼音串”没有官方样例；源码路径是取消清空，社区则有预编辑残留但候选失效的报告。工具在“还有可识别拼音”时尝试重建，在“已经被 Abort 清空”时会提示找不到残留串——这是符合源码行为的预期，不是实现遗漏。

---

## 9. 如何从光标附近取串（对工具的含义）

| 状态 | 取串方式 | 说明 |
| --- | --- | --- |
| 仍有活动组字 | `ImmGetCompositionString(GCS_COMPSTR)`；或 TSF composition range | 不要先当普通文本删掉。可尝试 `ImmSetCompositionString` / `ImmNotifyIME(NI_OPENCANDIDATE)` 把候选再唤出来 |
| 组字已提交为普通文本 | UI Automation `TextPattern`（按行/选区）；失败再对 `Edit` / `RichEdit` 使用 `EM_GETSEL`、`EM_LINEINDEX`、`EM_GETLINE` | 残留通常在光标左侧或当前选区 |
| 自定义控件 / 游戏 / 无障碍缺失 | 可能读不到 | 应明确失败，而不是盲目 Backspace |

跨进程注意：`ITfContextComposition::StartComposition` 只能由该文档上下文里的 **text service（输入法本身）** 在编辑会话中调用（[TSF Compositions](https://learn.microsoft.com/en-us/windows/win32/tsf/compositions)）。后台托盘程序**不能**对别人进程的 TSF 上下文直接“创建一个合法的输入法组字”。这是架构限制，不是本工具没调用某个 API。

因此重建策略只能是：

1. 优先对目标窗口的 HIMC 调用 `ImmSetCompositionString(..., SCS_SETSTR, ...)`（IMM 兼容层；现代 TSF 输入法可能忽略或只部分生效）。文档：[ImmSetCompositionStringW](https://learn.microsoft.com/en-us/windows/win32/api/imm/nf-imm-immsetcompositionstringw)。
2. 若组字长度仍为 0，则用 **虚拟键**（字母、`VK_OEM_7` 代表 `'`，以及声调数字）注入，让**当前**输入法像用户重新敲过一遍那样开始组字。不可使用 `KEYEVENTF_UNICODE`，因为 Unicode 注入通常绕过输入法，会再次把字母直接上屏。
3. 模拟按键时把空格分词改写成 `'`：直接发 Space 很容易变成“选中第一候选”。

`SCS_SETRECONVERTSTRING`（重新转换）是给“已上屏的汉字再拿回组字”用的，输入法未必会把纯拼音当重新转换对象。本版本不把它当作主路径。

---

## 10. 调查结论摘要

| 输入法 | 分词符（有依据） | 失焦后残留形态（有依据） | 是否仍可能有活动 composition | 缺口 |
| --- | --- | --- | --- | --- |
| 微软拼音 | `'`、空格；可用声调 `1–5` 兼切分 | 组字被完成/泄漏为结果串时：拉丁拼音 + `'`（如 `ni'hao`、`w'yao'kai'shi`）；空格切分的上屏形态未见同等的失焦报告 | 取决于宿主是否 complete/cancel；无官方“一定怎样” | 未在本环境实机确认 Win10/11 新版微软拼音在记事本/Chrome 等具体宿主中的表现 |
| 搜狗 | 组字区使用 `'` 形态分隔符 | 与微软类似，有中断组字后留下 `ni'hao` 的报告 | 官方规范未找到 | 官网未找到“失焦提交”说明；未实机 |
| 微信 | **查不到** Windows 官方说明 | **查不到** | **查不到** | 需实机 |
| 豆包 | **查不到** | **查不到** | **查不到** | 需实机 |
| 讯飞 | **查不到** Windows 桌面端依据（手机端讨论不可外推） | **查不到** | **查不到** | 需实机 |
| 小狼毫 | 默认方案：空格 + `'`（可配置） | 源码在失焦时 Abort 并清空，按设计常无残留；社区有预编辑残留但候选失效的报告 | 按源码应结束；异常时可残留失效组字 | 未实机；自定义 schema 的分隔符未知 |

对恢复工具的直接含义：按“拉丁字母 + `'` / 弯引号 / 可选反引号 + 有条件的空格 + 声调数字”识别残留，紧贴光标或当前选区；先处理仍活动的 composition，再删除已上屏半串并用 IMM 或虚拟键重建。对各家输入法、各个宿主的真实成功率，必须以 Windows 实机验证为准，见 [windows-verification.md](windows-verification.md)。
