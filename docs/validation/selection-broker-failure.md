# Broker 非零退出诊断

恢复核验：HEAD `b904cbf8fa7b2c5a933f01e407dc96546bd28098`，tree `2c8e7143a8f9c8c6283f3fa97c074bfda8e6e43d`，工作区干净，无构建/探针进程及排队或运行 CI。保持用户手动模型设置；未操作用户桌面、安装软件、配置凭据、增加权限或调用翻译模型。

[几何修复回归 run 38355942766](https://github.com/T1doo/KuaiYi/actions/runs/38355942766)，[job 115158735868](https://github.com/T1doo/KuaiYi/actions/runs/38355942766/job/115158735868)，Actions 源码 SHA 与上述 HEAD 相同。真实 WPF 几何四项 Pass；实际取词 **Fail 0/120**。Chrome HTML 为 2 HardTimeout、4 Crash、14 Paused；其余五视图各 6 Crash、14 Paused：合计 **34 Broker 非零退出、2 硬超时、84 按原规则暂停**。所有序列、Broker、Job/root/profile、runner 原始任务清理均确认。未知退出不能沿用前次 `RangeBounds` 观察猜测根因。

原始日志仅留忽略目录：28,673 bytes，SHA256 `094ee8abcae04c4d1fc3149a22f18ca2b4ed26c49d35fe8ab86af941e0ec6854`。尚未证明非零退出究竟来自 API 参数、空引用、程序集还是元数据序列化。无屏幕/标题/路径/原文/异常消息导出。

本次最小取证仅增加预期代码故障的固定类别（Argument、NullReference、NotSupported、TypeLoad、FileLoad）与元数据序列化阶段；失败时在写出任何帧之前生成无文本的 `BrokerFailure`。完整、nonce/type/exit 验证后的该标记仍映射为 Crash，并计入原有三次失败/60 秒、暂停 30 秒规则；真正非零退出、未完整接收、硬超时、取消与协议失败仍为 Unknown。不得转换为 Success 或绕过保护。

六视图各一次的新诊断为 DiagnosticOnly，基础设施正常结束仅为 Observed。密码/排除/权限/PID/start/HWND 校验、自动几何关联、250/600ms 时间预算、固定矩阵与正确率门槛保持不变。未识别故障仍由原有进程退出路径报告，不增加泛化的 catch-all。

前一成功原生 GetData 代码完整 SHA 与 Actions 执行 SHA 均为 `220060f813a3640487d593920a1b1824aa779a00`；[run 38347142627](https://github.com/T1doo/KuaiYi/actions/runs/38347142627)，[job 115131066822](https://github.com/T1doo/KuaiYi/actions/runs/38347142627/job/115131066822)。该闭环不重跑、结论不改写。P0 整体、用户 Win11 物理划词、PDF 与 Markdown 仍无通过结论。

本地 Contracts 正常构建及实际 Broker/client/fixture/probe 源码类型检查均 0 warning / 0 error；原 xUnit 断言直接执行 165/165，含三次完整 BrokerFailure 仍触发暂停及实际序列化退避不得输出捕获文本；PowerShell 摘要边界 35/35。此段为该版本运行前验证，实际 Windows 结果见下文。新代码的 Crash 可为真实非零退出，也可为正常退出但完整报告代码故障，后续报告须据 BrokerObservation 区分，不能统称非零退出。

## 六次代码故障诊断结果与原进程退出码取证

执行源码完整 SHA `50018c754c055a228c70019e7de098540c036a2a`，tree `5e1beaac1b9b277bd30fa9be650d95fe48a71605`，sole parent `b904cbf8fa7b2c5a933f01e407dc96546bd28098`。[run 38376111033](https://github.com/T1doo/KuaiYi/actions/runs/38376111033)，[job 115224775371](https://github.com/T1doo/KuaiYi/actions/runs/38376111033/job/115224775371)，attempt 1。

基础设施成功结束，摘要仅为 **DiagnosticOnly / Observed 0/6**。Chrome HTML、只读框各一次 HardTimeout；Chrome 编辑框及 Edge 三视图各一次 Crash；六个观察均 Unknown，没有完整 BrokerFailure 元数据。按该版本现有串行读取分支，四个 Crash 来自原子进程非零退出，但没有记录具体退出码，尚不能确定异常或故障原因。真实 WPF 几何四项 Pass，六视图序列与 Broker/application/profile 清理以及 RunnerSettled 均 true。ModelCalls 0、复制回退禁用；不能由 workflow success、零错译或清理通过推导取词兼容通过。

日志只保存本地忽略目录：19,921 bytes，SHA256 `3428acff9cef85d6237de9b9c1f42be5e2980d4514c2148abfa2287e1b476263`。独审核对唯一摘要、来源及此哈希，确认结论。

下一最小诊断复用原有 WaitForExitAsync 与绝对预算检查后的 held process.ExitCode 读取，只缓存非零 int?，每次入口先清空；不在 finally、Kill 或清理阶段补填，不增加等待、不读 stderr、不取原文或异常消息。Crash 状态、250/600ms 预算、归属校验与暂停规则不变。摘要仅允许已清理 Crash 且无 BrokerObservation 时携带有符号非零 Int32；完整 exit 0 BrokerFailure、超时、取消、暂停不得携带该码。退出码仅缩小故障范围，不证明 API 或应用兼容性。

该退出码版本的实际 client/probe 源码类型检查 0 warning / 0 error；原 xUnit 断言直接执行 **166/166**，含真实 child exit 23、完整故障的无退出码、Kill 后超时不回填、下一次预取消/暂停清空；PowerShell 白名单 **44/44**，拒绝字符串/布尔值/越界/0/硬超时/完整故障/未清理退出码，并丢弃附加私有字段。Windows 退出码诊断尚未运行，独审冻结后仅执行一次新的六调用诊断，不重跑原 120 项或 GetData。
