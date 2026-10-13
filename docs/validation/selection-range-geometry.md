# UIA managed Empty 矩形修复与验证范围

六次诊断执行源码 `dd6bf3e6a971d7364c41823844778e6b295a61be`；[run 38354193290](https://github.com/T1doo/KuaiYi/actions/runs/38354193290)，[job 115153168812](https://github.com/T1doo/KuaiYi/actions/runs/38354193290/job/115153168812)。结果 **Observed，0/6，不是能力 Pass**。前两个 Chrome 视图 HardTimeout、无完整元数据为 Unknown；Chrome editable 与 Edge 三视图均主动返回 `UnsupportedOrProtected / RangeBounds / Exception=None / HResult=null`。六个序列、Broker、应用 Job/root、profile 和 runner 原始任务结算全部确认。模型 0，复制关闭。

独审与官方源码核实：[.NET 10 UIA 适配器](https://raw.githubusercontent.com/dotnet/wpf/v10.0.0/src/Microsoft.DotNet.Wpf/src/UIAutomation/UIAutomationClient/MS/Internal/Automation/UiaCoreApi.cs) 会把非正宽高转换为 `Rect.Empty`；[Rect.Empty 官方定义](https://learn.microsoft.com/en-us/dotnet/api/system.windows.rect.empty?view=windowsdesktop-10.0) 的四元组为 `(+Infinity,+Infinity,-Infinity,-Infinity)`。现有 Broker 直接展开再拒绝非有限数，因此会把合法 managed 空矩形误当成非法几何。诊断没有导出原始矩形，尚不能断言四次运行全是此标记触发，也未证明两次硬超时原因。

修复仅将精确 canonical managed Empty 归为无可见几何；不伪造坐标。过滤前按原始条目跨所有 selected ranges 累计，仍最多 256 个矩形；其他 NaN、非规范无穷值、有限负尺寸、null 与数量超限拒绝。非法尾部不能提交部分成功。自动选择仍必须具有有限的真实选择范围并关联 down/up 手势；显式选择沿用原有允许空 range 几何的设计，同时保留其余来源、密码、权限、时间、文本和 runtime identity 检查。

便携测试覆盖 sentinel/有限混合、异常尾部、257 个 Empty、跨范围累计和自动/显式差异。Windows 入口额外使用真实 WPF `Rect.Empty` 检验适配器；随后对新修复源码运行固定 120 动作的原矩阵。原首轮 **Fail 0/120** 与六次 **Observed 0/6** 永久保留，不改写成通过；物理 Win11、PDF、Markdown、P0 整体验收仍需独立证据。

诊断原始日志仅在忽略目录：19,934 bytes，SHA256 `64160b2d23fbaf7f13b920f471d0703701380155958104142cec559d617aa64a`，未上传公开仓库。

本地 Core 正常离线构建、实际 Broker/probe 源码类型检查均 0 warning / 0 error；原 xUnit 断言直接执行 160/160（含 8 个新几何用例）；PowerShell 摘要边界 33/33。真实 WPF 四项类型语义检查和修复后的 120 次 Windows UIA 结果仍待 runner 执行。
