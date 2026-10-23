# WPF 设置窄窗布局 — 2026-10-23

基于 `fcb70966cacc927084a02cb7736b11b560ff7d15`，分支 `dev/p7-fixed-signoff`。仅设置窗口布局；未修改浮窗生长、250/600 ms、发送、取消、来源、密码、完整性与清理合同。

## 实际生产差异

- 原 `PreferenceRow` 的固定 180 DIP 右列改为原 labels/input 实例组成的 `PreferencePanel`。探测真实控件字体/template 的有界 DesiredSize；空间不足时上下排列，Measure 和 Arrange 共用同一计划。七个偏好控件的硬 MinWidth 改为面板偏好宽度，标题与说明继承实际字体。Automation LabelBy/HelpText 和原事件保留。
- 原 192 DIP 侧栏改由 `SettingsChromePanel` 分配：宽时侧栏，窄时原 ListBox 在顶部横向滚动。ItemsPanel 只创建一次，其 Orientation 随布局切换；不替换 ItemContainer、选择或业务控件，不主动 Focus/Activate。
- 页面标题与正文置于同一有限滚动视口；底部状态与原恢复/取消/保存/诊断控件也在有限滚动视口。导航上限 24%、底部上限 35%，正文取得剩余高度。快捷键和集成的原按钮行改为 WrapPanel，事件不变。
- 极窄工作区有真实水平滚动兜底。外层 VerticalDisabled、HorizontalAuto，根宽单向绑定 ViewportWidth；最小横向 extent 同时考虑当前字体与实际 `SystemParameters.VerticalScrollBarWidth` 动态资源，覆盖恢复控件的硬最小宽、Margin、Padding。没有调整原窗口工作区/DPI定位逻辑。

## 实际验证

- 新 `SettingsLayoutTests` 仅 7 方法，直接链接生产布局计划：窄列/200% 字体参数、实际模板宽、精确断点、返回宽窗、Collapsed、长反馈/短高度、实际滚条最小宽以及零/无限约束。最终 **7P / 0F / 0skip**，29 ms；属于平台独立算法测试，不是 WPF 实机测试。
- 首轮 **6P / 1F** 原件保留：`LongFeedbackCannotConsumeThePageOrNavigationViewport` 期望238、实际237.99999999999997，堆栈原行57。只是测试精确浮点比较错误，改为6位小数精度，不称产品红绿。未删除测试或改变生产预算。
- 初始候选 App 交叉 build **0W / 0E**，23.23 s；在实际滚条增量之前。冻结产品 `b2ac823ed7b1b45aee2e6dda7001080a1ddf91e9` 后最终 App 构建 **0W / 0E**，25.27 s。测试时的 487 个输入摘要与固定 Git 源逐项一致；这是候选测试源码绑定，不声称测试在提交后重跑。
- 两次只读源码审查限定 ACK；未发现确定新增 P1/P2。第二次核实际滚条动态资源与单向 MultiBinding。
- 没有重跑旧109/2、安装/完整Core/Codec。不同轮数不相加。

原始日志/TRX/首轮测试源码及输入摘要在 `artifacts/2026-10-23-settings-layout/`。不含个人内容，均为合成几何输入。

## 明确未验

当前 Linux 无可运行 WPF；本轮没有原生截图/录屏、200% Windows 文字、键盘 Tab/BringIntoView、ComboBox popup、焦点、真实多屏/DPI或高对比像素验收。200% 字体参数测试不等同系统200%文字或200%DPI。没有重试被拒的 Chromium 路径，没有使用 HTML 响应式代签 WPF，没有使用用户电脑或新 CI。旧 native/runtime/完整安装/P7与真实250/600ms门仍未签。

布局实现依据 WPF 的 [MeasureOverride](https://learn.microsoft.com/en-us/dotnet/api/system.windows.frameworkelement.measureoverride?view=windowsdesktop-10.0)、[ArrangeOverride](https://learn.microsoft.com/en-us/dotnet/api/system.windows.frameworkelement.arrangeoverride?view=windowsdesktop-10.0) 与 [MinWidth 优先级](https://learn.microsoft.com/en-us/dotnet/api/system.windows.frameworkelement.minwidth?view=windowsdesktop-10.0)。源码/数学测试不能替代原生布局验收。

原始证据已保存 Library `libfile_a3d05462cb7c81919c6f37fba2d86cda` v0：1,107,931 B，26成员/25manifest/11源码，SHA256 `9829e1f99424c8feeeea6260ca01b9ad9294cb79c6b10c860358ebd5b2e406dd`。包含最终 App DLL 与测试 DLL 本体用于摘要复核，**不是可运行成品**。原Library身份与版本已写入本地文件元数据。

最终独立只读复核：ZIP CRC/25摘要、11份Git源码、487捕获输入、两DLL本体及两轮原始TRX/固定构建日志一致；未重跑产品。
