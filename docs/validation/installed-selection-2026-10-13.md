# 已安装 Chrome / Edge 第一轮真实 UIA 结果

执行源码完整 SHA：`5f9fe1f537489c5a343526ab6823431b8bceee34`，tree `45cb419b8c68444e6b0abd7a1154544a6f0d5b41`，正常分支 `dev/p0-installed-selection`。

[Windows run 38351356248 / attempt 1](https://github.com/T1doo/KuaiYi/actions/runs/38351356248)；[job 115144257097](https://github.com/T1doo/KuaiYi/actions/runs/38351356248/job/115144257097)。既有免费 `windows-2022` 镜像，仅现有官方签名浏览器、离线合成文本和独占临时 profile。默认 on-demand accessibility、原有生产保护与 250/600ms 预算。

| 应用版本 | 视图 | 正确 / 固定动作数 | Broker 结果 | 能力状态 |
| --- | --- | ---: | --- | --- |
| Chrome 154.0.8037.98 | HTML | 0/20 | HardTimeout 2，Sensitive 18 | Fail |
| Chrome 154.0.8037.98 | Readonly textarea | 0/20 | Sensitive 20 | Fail |
| Chrome 154.0.8037.98 | Editable textarea | 0/20 | Sensitive 20 | Fail |
| Edge 152.0.4191.66 | HTML | 0/20 | Sensitive 20 | Fail |
| Edge 152.0.4191.66 | Readonly textarea | 0/20 | Sensitive 20 | Fail |
| Edge 152.0.4191.66 | Editable textarea | 0/20 | Sensitive 20 | Fail |

总结果 Fail，0/120；模型调用 0，复制回退关闭。全部六视图观察到正常序列结束，Broker owner、应用 Job/root、临时 profile 清理均确认；runner 原始 stdout/stderr 丢弃任务与实际进程退出已结算。没有成功采集文本，因此 0 mismatch 不能解释为取词正确。

当前不能确定拒绝根因。生产 client 将 `Sensitive`、`SensitiveOrElevated`、`UnsupportedOrProtected` 合并为 `Sensitive`；Broker 的 COM / Win32 / InvalidOperation / 权限异常也映射至后者。不能仅据此指责 Chrome 多进程 PID 过滤，也不能解除保护、扩大预算或删掉失败用例。

下一批明确为六次**诊断**：只在现有调用前记录闭合阶段枚举，保留白名单原始 Status、异常类别和数值 HRESULT，不增 UIA 查询、不重复 GetSelection、不含异常消息/路径/标题/原文。client 每次清空旧观察；未完整接收、超时、取消或协议失败为 Unknown。六次结束仅为 Observed，不能替代本轮 120 次能力结果或产生兼容性 Pass。

原始日志仅留忽略目录、未上传仓库：19,235 UTF-8 bytes，SHA256 `27d2d5bed8f87daab37d1b2c6a3b4d6274630b9167e2037a601fdc346f059033`。结果经只读独审核对。P0-02 整体、P0 阶段、物理 Win11/E06、PDF/Markdown 均未因此通过；先前 hosted GetData 通过记录没有重跑或改写。

诊断修订的本地验证：Contracts 正常离线构建及实际 Broker/client/probe/fixture 源码类型检查均 0 warning / 0 error；PowerShell 摘要边界 33/33；原 xUnit 断言直接执行 152/152，包含取词真实子进程边界、旧观察清除与新增诊断安全测试。这些不是 Windows UIA 通过证据。

本地回归曾为 151/152：旧 OLE 用例等待交付后只 `Task.Yield` 就取消子进程，后续 `DropWritten` 断言竞态；原用例隔离 32 次有 19 次失败，失败位置与缺失枚举已确认。仅在测试取消前加入原四秒预算内的真实诊断到达屏障，保留原全部断言和生产 OLE 行为；修复后隔离 32/32、完整 152/152。修复前日志保留在忽略目录，未删除失败或更改 Windows GetData 记录。
