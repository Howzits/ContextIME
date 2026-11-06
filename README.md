# ContextIME

**聊天中文，终端英文。让输入法跟随你的 AI 编程工作流。**

向 AI 描述需求，转到终端执行命令，再回到聊天框继续讨论。ContextIME 根据输入焦点请求中英文切换，减少反复按 Shift，以及把命令打成拼音的打断。

适用于 **Windows 桌面版 VS Code / VS Code Insiders**。沿用系统配置的默认输入法，不绑定某个 AI 助手，不要求固定的中英文键盘编号。

> 当前为早期版本，主要适配暴露标准辅助功能信息的输入控件。第三方聊天界面和输入法的兼容性需要实际验证，不能保证所有对话框都能识别或切换。

## 按输入场景切换

| 焦点位置 | 默认行为 |
| --- | --- |
| 集成终端 | 请求英文输入 |
| 可识别的聊天输入区域 | 请求中文输入 |
| 其他可编辑输入框，包括搜索、命令面板及部分 Webview | 请求中文，可配置为保持不变 |
| 代码编辑区、密码框、只读或未识别控件 | 保持当前输入法 |
| VS Code 之外的应用 | 不处理 |

切换由焦点变化触发。在同一个输入框中仍可手动切换中英文，扩展不会持续强制覆盖；返回输入框时重新按场景请求切换。

ContextIME 关注界面间的输入焦点，不分析代码语法、注释或字符串，也不接管 Vim 模式。不需要维护聊天插件品牌名单。

## 开始使用

**[下载已编译插件 ContextIME v0.4.0（VSIX）](https://github.com/Howzits/ContextIME/raw/refs/heads/main/dist/ContextIME-0.4.0.vsix)**，无需自行编译。

1. 在 Windows 配置可用的中文输入法。
2. 从 VS Code 扩展页 `… → 从 VSIX 安装…` 安装本地包，必要时运行 `Developer: Reload Window`。
3. 点击集成终端，等待焦点检查后输入英文命令。
4. 点击聊天输入框，试打中文，确认输入法接受了切换请求。

不需要另外运行托盘工具。若仍在运行早先的 AutoInput 托盘版，请先退出，避免两套规则同时生效。

## 沿用默认输入法

- 每次处理焦点变化时，重新读取 Windows 默认输入语言及已安装的键盘布局。
- 默认是中文输入法时，优先沿用它：终端请求英文模式，对话框请求中文模式。
- 默认是英文布局时，终端采用该默认布局；中文输入优先保留当前中文布局，否则从已安装列表中选择。
- 找不到可用输入方式时保持现状并记录日志，不安装新键盘、不加载固定布局、不修改系统默认设置。

无需额外安装美式英语键盘，不指定微软拼音、搜狗等品牌。Windows 输入语言接口不能准确区分同语言下所有 TSF 输入法配置，部分输入法也可能忽略跨进程模式切换请求。

## 状态与控制

点击状态栏的 **ContextIME** 暂停或启用。`目标 EN` / `目标 中` 表示布局请求已发出，**不代表已确认实际输入状态**；`保持` 表示当前控件不干预。出现请求失败或缺少输入法时，可查看日志。

命令面板提供：

- `ContextIME: 暂停 / 启用自动输入法`
- `ContextIME: 查看运行日志`
- `ContextIME: 重启输入法助手`
- `ContextIME: 诊断输入框（3 秒后采集）`
- `ContextIME: 此输入框记住英文 / 中文 / 保持不变（3 秒后采集）`
- `ContextIME: 清除本次会话的输入框偏好`

### 输入框偏好记忆

在聊天或普通输入框手动切换中英文后，如果 Windows 接口能观察到状态变化，ContextIME 会记住该控件的偏好；离开再返回时优先恢复。不会把一次布局请求直接当作用户偏好，也不会在同一控件中不断强制切换。

输入法的状态读取不总是可靠，尤其是 TSF 模式。可使用“此输入框记住英文 / 中文 / 保持不变”命令，然后在 **3 秒内点击目标输入框**，显式指定偏好。终端仍默认英文；代码区、密码框和只读控件不接受偏好操作。

偏好仅保存在当前助手会话内，最多 128 个控件；不会写入磁盘或记录聊天内容。控件重建、重载窗口、修改设置或助手重启后可能失效或清空。新版本安装后建议重载所有 VS Code 窗口，避免旧助手占用共享控制通道。

### 输入框诊断

运行诊断命令后，在 **3 秒内点击目标输入框**，结果会显示在 ContextIME 输出面板：

```text
diagnostic reason=editable-input default=zh preference=en observed=en remember=True interval=250
```

`reason` 表示规则依据：`terminal`（终端）、`editable-input`（输入框）、`code-editor`（代码区）、`password`（密码框）、`readonly-or-noneditable`（只读或非输入控件）、`scope-preserve`（按范围保持）或无法获取可访问焦点。`default` 为规则目标，`preference` 为会话偏好；`observed` 仅是传统 Windows IME 接口的参考值，不保证是实际 TSF 状态。诊断不输出控件文字、窗口标题、代码或聊天内容。

## 设置

| 设置 | 默认值 | 说明 |
| --- | --- | --- |
| `autoInput.enabled` | `true` | 启用自动切换；关闭后停止助手 |
| `autoInput.rememberPreferences` | `true` | 记住当前会话的输入框偏好；关闭后始终按默认规则 |
| `autoInput.otherInputs` | `chinese` | 其他可编辑输入框也请求中文；设为 `preserve` 可减少对搜索、命令面板等区域的干预 |
| `autoInput.pollInterval` | `250` | 焦点检查间隔，150–1000 ms；实际切换延迟受控件与输入法影响 |

如果只想处理终端与能识别出聊天语义的输入区域：

```json
{
  "autoInput.otherInputs": "preserve"
}
```

没有暴露聊天语义标识的第三方输入框也可能因此不再切中文，改回 `chinese` 可恢复通用输入框规则。

设置项和内部命令 ID 保留原有 `autoInput` 前缀，方便已有本地版本升级；界面名称统一为 ContextIME。

## 兼容范围与限制

- 当前构建在 Windows x64 本机验证，要求 VS Code 1.96 或以上；不支持 macOS、Linux、浏览器版 VS Code 或系统全局切换。
- 扩展运行在本机 UI 侧。Windows 本机打开 SSH / WSL 工作区的焦点路径设计上适用，但这些流程尚待实际验证。
- 输入框必须暴露可访问的可编辑控件。自绘控件、不提供可写状态的 Webview 可能无法识别。
- Monaco 聊天编辑器还需有可识别的聊天容器标识；否则可能按代码编辑区保持不变。
- 跨进程输入法请求可能被应用或输入法忽略。日志中的 `layoutRequest=True` 仅表示请求发出，不等于中文已上屏。
- 建议先完成候选词上屏，再切换区域；未完成输入的处理由输入法决定。
- 多窗口共享一个活动助手；设置为用户级，暂停会同步到其他窗口。

## 没有自动切换？

1. 确认状态栏未暂停，聚焦目标输入框后稍等再试打。
2. 运行 `ContextIME: 诊断输入框（3 秒后采集）`，返回目标控件后查看规则原因；日志可进一步区分缺少输入法与请求失败。
3. 没有识别到控件时，可手动尝试把 `editor.accessibilitySupport` 设为 `on` 后重载窗口。这会启用 VS Code 辅助功能行为，扩展不会替你修改。
4. 已发出请求但没有中文上屏，先手动切换确认输入法本身正常；可能需要进一步适配该输入法。
5. 助手停止时运行重启命令。异常退出最多自动重试两次，暂停或禁用会取消重试。

同时启用操作同一区域的其他输入法工具可能互相覆盖。反馈时提供 Windows、VS Code、输入法版本、触发区域和日志即可，不需要聊天正文。

具体检查项目和待实测场景见 [兼容性验证记录](docs/COMPATIBILITY.md)。自动测试通过不能替代真实界面上屏测试。

## 隐私与实现

ContextIME 通过 Windows UI Automation 查询焦点控件的类型、可写状态及结构标识，再发送 Windows 输入法请求。不读取输入值、代码正文或聊天内容，不监听按键，不联网，不上传遥测。日志仅留在 VS Code 输出面板，记录目标语言、请求结果和错误类型。

扩展会读取前台进程名以判断是否为 VS Code，仅对支持的 VS Code 进程应用规则。安装包中的 `InputHelper.exe` 由扩展启动，暂停或禁用时停止；退出或崩溃时也会通过进程生命周期清理。

## 开发

```powershell
./build.ps1
```

需要 Windows .NET Framework 4.x 编译器和 Python 3，无 npm 运行时依赖。构建原生助手、执行规则自检并在 `dist/` 生成 VSIX。仓库保留该目录中的已编译安装包。使用 Node.js 可运行 `node --test test/extension.test.js` 检查扩展生命周期。

| 文件 | 职责 |
| --- | --- |
| `extension.js` | VS Code 命令、设置、状态栏与助手生命周期 |
| `native/InputHelper.cs` | 焦点采集、切换请求与轮询 |
| `native/InputPolicy.cs` | 终端、聊天及普通输入框判断规则 |
| `native/LayoutSelector.cs` | 默认及已安装输入法选择 |
| `native/Preferences.cs` | 有上限的会话偏好缓存 |
| `native/CommandBus.cs` | 多窗口命令路由与结果回复 |
| `native/NativeMethods.cs` | Windows 接口声明 |
| `test/extension.test.js` | 暂停、配置更新、状态消息和异常重试测试 |

## English

**Chinese for chat. English for the terminal.** ContextIME requests input-method changes as focus moves between chat inputs and the integrated terminal in Windows VS Code. It uses configured Windows input methods rather than fixed keyboard IDs. Code editors and password fields are left unchanged. Set `autoInput.otherInputs` to `preserve` to leave ordinary inputs alone. Accessibility and IME support vary; requested state is not verified input state.

## License

MIT. Independent project; not affiliated with any AI assistant vendor.
