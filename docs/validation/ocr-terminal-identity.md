# OCR 原请求身份终结修复

从父 `f78401d8803f80f8302e19d8d88c7c0bf9b5e354` 的 clean 工作树、单 worktree、唯一 root 写入者及实际默认远端核对后，建立 `dev/ocr-terminal-identity`；原 UNKNOWN 实例不恢复。只读 `ocr_terminal_review` 独审。生产仅修改 `OcrOnlyCoordinator`，Core Gate、Provider、App、所有既有断言及安全门不改。

两项真实 P2：Settings/Mode 改变使 `Current=false` 时，完成交付跳过 `MarkRequestTerminal`，却释放局部 active，canonical Gate 仍认为 B 正在生成，连接测试永久被阻挡；另外 Gate 先安装 B，再同步取消旧 A，A 的回调抛错会发生在协调器保存 B 之前，留下无人结算的 B。

修复在 `RunAsync` 的 finally 中无条件 `MarkRequestTerminal(operation.Identity)`，与是否可以显示预览分离，保留成功预览身份；Gate 只对相等身份置终态，因此不会标停后继 C。Start 在调用 Gate 前保留 default 身份，try 覆盖安装、旧 A 取消及后续准入证明；out 已写入的 B 即使遇旧回调异常仍可精确结算。catch 只 `StopRequest(B)`，不清共享 active/Running/worker/revision、不调用全局 Stop；C 可以在 A 或 B 清理回调中成功建立。无清理错时 bare throw 保原异常对象/堆栈，清理也抛错时 AggregateException 保留原错与清理错。未扩大 Core 原取消/Dispose API 范围。

| 执行 | 结果 |
|---|---|
| r01 真实生产红测 | **1 通过 / 6 失败**；两条孤儿路径、未执行 B 清理及安装后来源证明抛错均暴露；先建 C 后抛错对照通过。父生产/测试及精确红测试源码、原 log/TRX 保全 |
| r02 首轮修复 | 7/7 |
| r03 OCR 扩展专项 | 77/77（原 62 + 当时新增 15） |
| r04 最终完整 Provider | **1499/1499 = 父 1481 全保留 + 新 18**，0 失败/跳过；独审另逐项比较 testName 多重集 |
| r05/r06 | App 与 Worker Release 交叉构建分别 0 警告 / 0 错误 |

新 18 项涵盖：真实异步启动前 Settings/Mode 失效，factory/worker/HTTP 都为 0，完成后恢复配置/模式，实际 ApiConnectionTestWorkflow.Prepare 及 canonical 连接测试准入重新可用（不启动 HTTP）；A 回调抛错、安装后来源证明抛错、A 或 B 清理回调创建 Gate C / Local.Start C 后抛错；两异常按对象引用核验，B 不再可应用，C 身份/生成状态/未取消令牌及 Running 不变。Local C 的实际合成 worker 仅在明确 Drain 之后运行，先证明失败 B 的零工作，再证明 C 可正常预览，始终 0 HTTP。另覆盖已进入 worker 及排队交付阶段的 Settings/Mode/C，清理已确认/未确认都只终结 B，未确认保原 owner，后续 Clear 不伤 C，借用 PNG 在原 Run 返回后清零。

故障注入只在测试中用反射只读实际 Gate 的已安装 B identity/CTS，为该真实 CTS 注册会抛错的回调；不替代或修改 Gate 行为，不接用户桌面。同步上下文/worker 输出是明确合成合同。父级真实托管 Linux Worker UnsupportedPlatform 测试随完整回归执行；不能外推实际 Windows OCR/Job/包身份/语言/质量、WPF 窗口/测量/选择/阅读位置、COM 或 250/600ms 时限。

固定证据见 [JSON](../test-evidence/2026-10-16-ocr-terminal-identity.json)。10 SDK log/TRX、3 父版/红版源码、1 preflight 共 14 份原件保留原字节并使用精确 `-text` 路径。父级 37 份原件及累计 256 原回执、1843 既有证据保持；43 锁、8 workflow、8 模板与项目引用/全部第三方版本不改。只读路径误猜完整记入 JSON，不冒充 SDK 失败。

P6-03 InProgress/NotRun、P6-02 InProgress/Blocked、04/05 不变；生产离线复用 Off，250/600ms、原 15 秒与全部来源/密码/完整性/Job/process/取消清理门保持。所有 dotnet 激活前后及命令显式 false 证书变量；旧存储/信任 Unknown，不读库/配置/trust/删除。Skia 许可待答、源码 403 与 Actions 拒绝门保持；不 restore/install/load/execute/绕拒绝，不查询调度 Actions，run 数 Unknown。无新依赖/协议接受/真实模型/凭据/费用/权限、Windows/CMake 探针、新 CI、main/强推/部署。
