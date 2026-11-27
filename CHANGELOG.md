# 更新记录

## 0.1.0 — 2026-11-27

- 首次发布：后台托盘程序，默认快捷键 `Ctrl+Alt+R`。
- 识别光标附近残留的汉语拼音（含 `'`、弯引号、空格分词、声调数字等）。
- 删除已上屏半串后，优先尝试 IMM `ImmSetCompositionString(SCS_SETSTR)` 重建组字，失败则按虚拟键重新交给当前输入法。
- 单元测试覆盖拼音识别与分词符处理。
- 调查记录见 `docs/ime-focus-loss.md`。真实 IME 效果需在 Windows 上按 `docs/windows-verification.md` 验证。
