# 独立目录 App 启动验收构建与离线包准备

父有限核准 `7f9df38fa381759c0b437d655b4f9b03f337fb0b` 后，本机前置回报为 Blocked：固定版本的 Program/Shell 使用真实 KnownFolder，修改进程 `LOCALAPPDATA` 无法隔离；本机只有 .NET 7，没有 .NET 10 WindowsDesktop/SDK 或已核自包含闭包，桌面执行工具缺失，CIM 只读拒绝已停。该本机任务已终态、四项均 NotRun。本轮只在原 `/workspace/KuaiYi` Linux 保存环境继续，唯一写入者、单 worktree，正常分支 `dev/isolated-session-data`；未连接或改写本机。

## 最小方案与生产边界

新增显式构建属性 `KuaiYiIsolatedSessionTest=true`，编译验收专用选择器；默认生产 App 不编译此类型，不新增通用数据根参数或环境变量合同。验收构建只允许无参数正常入口或原精确非空 GUID 握手，白名单在诊断/OCR/review/测试服务分流前执行。必须是真实安装布局，错误不能回落 KnownFolder 或开发入口。测试构建的菜单/标题显示“独立目录启动验收 · 非生产安装包”。

布局固定为 `KuaiYi-SessionTest-<runId>/application/versions/<generation>` 与其同级 `user-data`。容器和版本内两份严格、有界、规范 JSON descriptor 必须同字节绑定 runId；descriptor 不接受路径。拒绝网络/非绝对/根目录、所有祖先和文件 reparse point、尾点/尾空格/短名 `~`/冒号流拼写，以及与真实生产 KnownFolder KuaiYi 的双向包含。缺少数据目录、描述或错名都拒绝，启动选择器不创建或修复目录。

Program、普通会话父协调器、controlled child 和 Shell 的 SettingsCoordinator 使用同一个选定根。Shell 只在未提供 coordinator 时延迟构造原生产默认值。实际 SecretStore 仍是 CurrentUser DPAPI 与原目录策略；没有假秘密后端。原 scope/mutex、catalog/current、launch/owner/lease、晚到任务、双收据及清理/失败保留门未改。测试构建拒入不发 ShowExisting，避免唤回同用户生产窗口；没有另造并行生产身份。

这是显式验收构建，不是默认生产二进制对真实生产数据目录的验收。没有身份注册前置门不等于保证 Windows 可以启动；任何实际拒绝保留为阻塞。原同账户恶意并发换名/篡改边界仍存在，不能声称绝对文件系统隔离。

## 包准备与真实结果

在已有、已激活的固定 SDK `10.0.401` 云构建环境，从仓库根运行：

```text
python3 scripts/prepare-session-test.py
```

脚本不接受输出覆盖或其他参数。先用已有资产 `--no-restore` 重建既有 fixture，再由 .NET helper 使用真实 KnownFolder 和相同路径门批准固定仓库 artifacts 下的全新 workspace，之后才写准备日志。输入发布目录也必须在遍历/读取前通过生产数据和输出目录双向隔离检查。未知文件、失败工作目录和日志保留，无目录清理、App 启动、安装或注册。

缓存齐备后才执行：精确既有 alpha 锁文件、独立 NuGet 配置清除继承的 package/audit 源和映射，仅保留本地空源、审计继续 `NuGetAudit=true`，locked restore；失败不抑制或绕过。随后 `--no-restore` 发布 win-x64、自包含、多文件、不裁剪、不 ReadyToRun 的显式验收 App，由既有 fixture 建 catalog 并调用实际 `InstallationStore.ApplyAsync`，输出固定命名目录 ZIP。创建器核实际 x64 PE、App 元数据中的验收类型、双 included-framework 10.0.12 声明和关键运行时文件，并对复制后的输入重核。该检查是结构检查，不是完整 native 依赖闭包或可运行保证。

**r09 实际结果为 Blocked/Exit2/Bundle=null**：缓存缺少 `Microsoft.NETCore.App.Runtime.win-x64/10.0.12` 和 `Microsoft.WindowsDesktop.App.Runtime.win-x64/10.0.12`；apphost 和 WindowsDesktop ref 已有。没有下载、恢复、发布或生成 ZIP，没有安装 SDK/运行时/系统组件。带本地审计源的恢复/发布阶段尚未执行。这里交付的是可审构建准备和隔离合同，不声称已有自包含产物。

r13 实际准备调用拒绝默认生产 App（元数据不含验收选择器）；r14 拒绝框架依赖的验收 App（没有自包含 runtime 声明）；二者 Exit2 且未创建输出。r15 实际拒绝已移除的 `--output-parent`，不创建目录。

## 红绿、回归和证据范围

原件、逐次源码与二进制绑定、失败记录和缓存前置输出见[机器索引](../test-evidence/2026-10-18-isolated-session-data.json)。每次源码原字节以 ZIP 容器保留，条目与逐源 SHA 一一核对；未裁剪失败输出。

- r01 是新测试 SaveAsync 缺 basis 参数的编译错误，无测试执行/TRX；r02 为新测试误把 InvalidDataException 当 IOException，16P14F，改为明确允许两种拒绝异常，未改旧测试。r05 验收 WPF 实际编译暴露选择器缺显式 System.IO，44 errors，修正后构建通过。这些都不冒称产品红绿。
- 独审发现创建器先遍历发布输入、后校验输出，可能把生产数据打包。新增五个公开反例 r03 实际 **5F30P**，在遍历前加入 source-v-production 和 source-v-output 双向包含检查后，r04 同完整测试源 SHA `7be20fa1f38b99a4308324ccaf21b4cccc858ff3782ade3b809a9a23d47599a7` 实际 **35P**。
- 后续加入四种路径别名和六个构建 workspace 门用例，最终 r07 **45P**；r08 完整 Core **1888P**，是父 **1843** 全名多重集加新 **45**，无缺失/skip。r10 完整 Provider **2332P**，与父全名多重集完全相同。45 已包含在 1888，不能相加。
- r06 验收 App、r11 默认生产 App、r12 Worker 实际交叉构建均 **0 warnings / 0 errors**。r06 `-o` 目标 App 哈希由 r14 实际读取/拒绝记录绑定；不把 r06 记录中旧默认 bin 的哈希当其独立输出的运行证明。
- 最终六个测试/构建轮次各 73 源相同；r09/r13/r14/r15 另绑定相同源码与实际调用结果。这里只是便携验证和交叉构建，没有运行 Windows App。

独立源码复审在输入隔离、输出 workspace 门和离线审计修正后未发现剩余 P1/P2。最终封存审查另记录，不代替 Windows 验收。父 655 原件本轮没有逐份重新计算哈希，不扩大父有限准入。

## 仍需的条件与精确待验

云构建机需提供上述两个精确运行时 pack，并在现有许可/来源授权范围内完成实际 locked restore、self-contained publish、结构和完整包散列核对；目前没有可交付运行包。随后需要获准的 Windows 11 x64 解锁桌面及执行/观察能力。本轮不重试 CIM，不替代查询绕拒绝，不加 Windows CI。成功准备的自包含包无需目标机另装 .NET 10 或 SDK。

保留容器目录名，解压到获准独立目录，直接无参数启动 `application/versions/<generation>/KuaiYi.App.exe`：核真实红色浮钮/托盘与 Off；重复启动没有第二 Shell，原会话存活，不预设重复调用 Exit0；用户正常退出后核实际 parent/child Exit0、原 IO 结算、对应 owner/launch 精确归档和 current 原字节不变；第二次同版本新会话重复成功并核新身份。保留真实进程、公开记录及界面证据。任何窗口/Console/身份/运行时拒绝按实际状态结算，不改信任或安全权限。

真实 WPF/Dispatcher/STA/Console、键盘可见性、Windows、安装器及完整 P7 均 NotRun；身份/NativeHost 注册 rollback、稳定 launcher、完整原生 payload、Inno、自启卸载、浏览器正常 Shell 收件链未补齐或未验。历史图片/idle/Core/Job 与 r10/r17/r20 根因仍各 Unknown，绿色回归不等于解决旧间歇故障。生产图片分类复用 Off、250/600ms、来源/密码/完整性/取消/清理门保持。Skia 四包许可与来源 403、Actions 拒绝不查不绕；无真实模型、main/强推/部署/费用/凭据/安全权限变更，未知数据/锁/日志/进程保全。
