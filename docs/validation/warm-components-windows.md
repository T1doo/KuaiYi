# 固定默认 Program／真实 Shell 的 Windows 分段验证

2026-10-21：**三条独立产品预览链 Pass，整轮仍 Fail**。固定生产源码 `92e3548845bd07ee7717b53df9588c412d128f9d` 未改，投递 harness 源为 `10d5fb747e44a4cf2a612597f2aa31d91176e1dc`。这不是正向 warm、独立 App.exe、安装包或 Explorer 验收。

## 实际结果

| 输入／组件 | 本轮实际结果 |
|---|---|
| PNG 640×160 | Pass：原安全文件打开→真实 Worker→默认 Shell WPF 预览 |
| 双帧偏移透明 GIF 4×3 | Pass：首帧完整逻辑画布、偏移、透明白底、可见“使用首帧”，未显示第二帧 |
| 字典增长／交错 GIF 256×128 | Pass：全像素独立 palette oracle、可见“使用首帧” |
| 三例清理 | Pass：原 Worker finalize、首次可见 Rendering 时已退出且原 IO 结算；原 pixels、PNG 和显示 WriteableBitmap 逐字节归零，来源／owner 清空，Off／无生成 |
| 原 Shell 退出 | Pass：原 Clear、Shell.Close、closeRequests.Current、startup／positionSave 已 join，窗口关闭，默认 Program 返回 |
| 真实 High／原 DACL／first-instance | Pass：Current 原 High 门拒绝；原实际句柄 protected／唯一 logon ACE／精确0x12019b；第二次原 CreateServer 返回 Win32 5 |
| 客户端附加 bit4 实验 | **Fail**：请求0x12019f得到有效客户端句柄，触发保留断言；未查询 GrantedAccess，不证明服务器创建权限或跨身份访问获授 |
| 后续 exact-mask／双向 PID/session／双方 peer 构造器分支 | NotRun：仍位于失败断言之后，不能用 Current High 拒绝代签 |

本次 [run39565330615/job119106116430](https://github.com/T1doo/KuaiYi/actions/runs/39565330615/job/119106116430) 实际 job **failure**、wrapper **Blocked**、probe **Fail**、actual-components **exit3**，没有超时或循环重跑。其余21条记录命令均exit0；双 runtime 正常在线验签、批准 SDK402 精确 ZIP/许可/hash、安全解包、locked restore／审计、harness 与 Worker 构建、18文件精确部署都已实际执行。三例是本轮 harness 场景，不是 xUnit 数量，也不与其他轮相加。未重刷旧 Core／Provider／Codec。

## 执行与所有权边界

既有 GrowthHarness 的显式 CI 模式在**同一进程调用默认 `Program.Main([])`**，引用固定默认生产 App；无 isolated／平台实验编译开关，不走 `--platform-review`，不放宽 `--prepare-image`。默认 Shell、KnownFolder Settings、Off、原 Shell 服务与清理真实运行。测试只绕过文件选择对话框：factory 仅记录并返回原 CreateFileImageOperation，原 LocalImageFile→ImageWorker／Skia→finalize→AcceptFileImage→WPF 全部保留。首次可见 Rendering 的原 SettlementSnapshot 与固定源码发布前 finalize 合同共同支撑时序；不把单次后验 ExitState 当独立证明。

真实 KnownFolder 的 KuaiYi 目录以原生 CreateDirectory 独占新建；已存在、reparse、未知目录均拒绝。没有覆盖 LOCALAPPDATA，也没有删除未知数据；marker 与中性 fixture 留给临时 runner 销毁。测试消息泵仅设 OnExplicitShutdown 等待原关闭任务；失败不强清 owner，不用 Environment.Exit 假报成功。

管道组件仅用本进程真实 token／原生产方法和随机 fixture 名；没有造 token、降 IL、借其他 token、改 UAC／ACL／桌面权限或凭据。原 bit4 红断言保留，原等待 Task 已 join、原 DisposeAsync Task 成功且原 SafeHandle 关闭后，才继续独立 Shell，失败仍留 Errors／总 Fail。正文与 dispose 独立错误按单错 EDI／多错 Aggregate 保全。Windows 已确认该门全部满足。

[官方管道权限说明](https://learn.microsoft.com/en-us/windows/win32/ipc/named-pipe-security-and-access-rights)明确服务器 CreateNamedPipe 需额外 FILE_CREATE_PIPE_INSTANCE 检查；当前客户端有效句柄不足以证明服务器实例权限。此次不新增 Authz 或修改生产安全策略，不为全绿删除实验断言。其含义保留待定；不据此宣布 DACL 回归，也不宣称这项拒绝已验收。

## 原始证据与来源

前轮源 `8adf512bb30ba855ced011ea156327dd99237a6c` 的 [run39563827752/job119101200547](https://github.com/T1doo/KuaiYi/actions/runs/39563827752/job/119101200547) 同一复合断言失败，未记录句柄／错误值，Cases=[]，Shell/Worker/WPF NotRun。其25件75,863原字节与源码已保全：ZIP260,050B SHA256 `27d1a24d7eb327e9cd55d85360e21d9528328c61630b221aeec3c58e7f2f1a5e`。不把新观测回填历史唯一根因。

本轮26件101,494原字节已逐件独立重组核对 chunk／base64／gzip／解压 SHA，raw job126,385B SHA256 `0afa04bff527dbc9b589b5c50427cbf79a82eed3e4649098b69fe95bdb2e248d`。372个实际 Windows checkout 输入全部匹配源：7个原字节、365个正常CRLF换行、0差异。原件ZIP273,627B SHA256 `2a10070e95ea0e446167f456fb06a4c6c5d42d78ae990b31092cc4ef03e91db4`。只读独审限定 ACK 三条实际链；整体失败保留。

完整原件包含两轮失败ZIP、raw日志、26件原件、命令／源码／产物摘要、本地必要构建／3项CI来源与碰撞检查及初始失败；97成员，730,564B，SHA256 `97acd17779aa17c8f83e3695a7db57c439d523fcf4888fb23948acd9f7600d7b`。已确认 Library ID **`libfile_c32e11ee722c8191b4303512b346a51e`**，version0；无私有下载 URL。详见 [机器可读索引](../test-evidence/2026-10-21-warm-components-windows.json)。

## 未验收项

正向 warm 因未有既有 medium 入口仍 Blocked；没有尝试造／降／借 token。独立 App.exe、安装包／Explorer 来源身份、外部第二 CLI 到同一 WPF、跨身份实际 DACL 拒绝、OLE／OCR、分阶段外部断线与 held-owner、250/600ms、600MiB、真人画质和完整 P7 均未由此片签收。PackageHold、生产复用Off、默认Off，无自动 OCR／发送或真实模型调用。
