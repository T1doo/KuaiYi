# 隔离验收包运行时安全补丁准备

状态（2026-10-19）：**SDK 初始化、代码回归与交叉构建 Pass；自包含包 Hold，未 publish/生成 ZIP**。正常开发分支 `dev/session-runtime-security`，基线 `b21a1e3bd06bda43a005b95bf5f62aecd8278e50`。下文 2026-10-18 调查作为历史保留，最新结论以本节及新索引为准。

## 2026-10-19：实际修复与验证

新增 `scripts/session-sdk.py`，让已获批的独立 SDK402 走正常 dotnet 入口。根据 [10.0.13 官方变更说明](https://github.com/dotnet/docs/issues/56396) 与 [维护者说明](https://github.com/dotnet/runtime/issues/135726)，Linux 用户级 named mutex 使用 `XDG_RUNTIME_DIR`，未设置才回退 HOME/.dotnet。启动器固定用户自有 0700 工作区 runtime/data/cli 目录，拒绝链接、错 owner 或既有宽权限；不 chmod 既有目录，HOME 保持 `/home/agent`，默认安全用户作用域不变。NuGet 的真实迁移记录用 XDG_DATA_HOME，CLI 状态用 DOTNET_CLI_HOME。未伪造 sentinel 或跳迁移。

原 r02/r03 的 `NuGet-Migrations` / EROFS 初始化失败原件仍在；r04 同一安全 SDK 实际正常完成迁移、编译和61项定向测试。SDK 自己创建迁移记录及 first-use sentinel，HOME 仍只读且 HOME/.dotnet 仍不存在。没有改挂载、权限、信任或系统工具链。直接执行 MSBuild 等避免 first-use 的入口未采用；正常入口现已可用。

实际安全恢复揭示 NU1403：原手拟锁错误使用完整签名 nupkg 哈希。现 [锁文件](../../packaging/session-runtime-audit/packages.lock.json) 使用 NuGet 实际内容哈希，[独立完整性清单](../../packaging/session-runtime-audit/runtime-pack-integrity.json) 继续固定原官方 catalog 的完整归档 SHA-512。两种哈希经实际 verify/metadata/assets 与独审内存重算交叉确认；[NuGet 源码](https://github.com/NuGet/NuGet.Client/blob/b260088dc6b3d3a1c3e76ad0422ce4ceb03bbcdd/src/NuGet.Core/NuGet.Packaging/Signing/Archive/SignedPackageArchiveUtility.cs) 对签名归档去签名并修正 ZIP 后计算 contentHash。没有忽略 NU1403、force-evaluate 或降低 locked-mode。

r11 runtime locked restore、恰两项10.0.13的普通库存与实际漏洞查询成功。两份 `dotnet nuget verify --all` 虽 Exit0，却各报8条 NU3018/NU3028、CRL 不可达及撤销状态未知。准备器现拒绝任何警告/错误及 RevocationStatusUnknown；并在任何 dotnet 前拒绝继承的 offline 或禁用签名配置，保持正常在线默认。实际 r11 在签名门拒绝、Bundle=null，未进入 publish。两份真实日志送最终门均拒绝。微软 [NU3018](https://learn.microsoft.com/en-us/nuget/reference/errors-and-warnings/nu3018) / [NU3028](https://learn.microsoft.com/en-us/nuget/reference/errors-and-warnings/nu3028) 说明这些状态不构成完整证书链/撤销准入；未切 offline、补信任或换路访问。

| 实际验证 | 结果 |
|---|---|
| r04 定向 / r05 完整 Core | 61P / 1904P，0F、0skip；父1888全部保留，恰新增16例 |
| r07 完整 Provider | 2332P，0F、0skip；父全名多重集相同 |
| r25 Python 安全门 | 22个测试方法通过，含 subTest，不冒充.NET/Windows测试 |
| r16 默认 App / r18 隔离 App / r20 Worker | 实际 SDK402 交叉构建均0警告、0错误 |
| r15 App locked restore、r23/r24 库存/漏洞查询 | Pass；同项目实际 assets 七项均 project，PackageCount=0 |
| runtime 签名、self-contained publish、ZIP | 签名撤销状态未知；准备器拒绝，publish/ZIP NotRun |

过程错误全部保留：r06 Provider路径拼写导致 MSB1009；r13/r14 错将全局 RID 传给无 RID 的普通库锁，NU1004；正常 App 以其已有项目 RID 恢复，r15成功。r19 list 未继承 restore 的 EnableWindowsTargeting 上下文而出错，按 [MSBuild 官方环境属性语义](https://learn.microsoft.com/en-us/visualstudio/msbuild/how-to-use-environment-variables-in-a-build) 让相同属性贯穿子进程后，r23/r24 实际成功，未丢弃 problems。最初 Python 测试 fixture 受 umask077 影响的失败单列保留，仅给本轮自建临时 fixture 明确0755，生产目录守卫未改。

13份最终修改源码有 SHA-256 与每轮源码 ZIP 绑定；原已有测试及8个 workflow 字节不变。封存独审另发现原始 NU1403 日志一处尾空格，原字节保留；.gitattributes 只给本片每个精确原件路径加 -text，且只给该日志加 -whitespace，完整 cached diff --check 随后通过。r16/r18 原执行元数据只采 bin 输出，因此额外封存带时间的两个实际 `-o` 目录56文件哈希，明确为构建后采样，不倒填原元数据。源码独审无剩余P1/P2，原56份安全调查证据原字节保全。新证据见 [2026-10-19 原件索引](../test-evidence/2026-10-19-session-sdk-initialization.json)。

当前首先需满足的签名门条件是：获准环境中正常在线 NuGet 签名验证能够取得有效撤销状态、无警告。后续 publish/打包尚未实跑，不能保证没有其他阻塞；当前日志只证明服务器/CRL不可达，确切网络原因未实证，不冒称访问审批拒绝。旧 Actions Forbidden、CIM 拒绝、runtime GitHub API 目录树 tunnel403 分别保留，未重试或换路。仓库现有有界 Windows CI 的分支/路径门与当前分支不适用，8个 workflow 不变；未查询服务端运行状态，记 Unknown。

Windows/WPF/实际 Dispatcher/STA/键盘/Console/Job/OCR/DPAPIACL/截图/物理250600/模型及完整P6P7仍 NotRun。历史图片/idle/Core/Job/r10/r17/r20根因各 Unknown，生产图片/分类复用 Off，来源/密码/完整性/取消/清理门不变。Skia四包许可待批与原403不绕过；无 main、强推、部署、费用、新凭据或安全权限变更，未知锁/数据/日志/进程保全。

## 2026-10-18 历史调查（原结论保留）

## 补丁依据与授权边界

官方 [XML 公告 #447](https://github.com/dotnet/announcements/issues/447) 和 [WPF 公告 #475](https://github.com/dotnet/announcements/issues/475) 明确 .NET / Desktop 10.0.12 受影响，10.0.13 修复。2026-10-18 09:14 UTC 实取的 [10.0 发布元数据](https://builds.dotnet.microsoft.com/dotnet/release-metadata/10.0/releases.json) 列出 2026-10-13 安全发布、runtime 10.0.13 / SDK 10.0.402，10.0 为 active LTS，支持期至 2028-11-14。原始元数据与 SHA-256 收据封存于本轮证据。

拟将 `global.json` 精确 SDK 锁由 10.0.401 更新为 10.0.402（保留 `rollForward=disable`），两个包装合同和验收包框架检查更新为 runtime 10.0.13，保持 .NET 10 / Windows x64 / WPF / 多文件自包含、无裁剪等已有选择。旧各项目锁没有这两个 framework download pack 的 PackageReference 锁；新增仅 restore/list/verify 的审计库存项目，将两个 runtime 包精确声明为 10.0.13 并以官方 catalog SHA-512 锁定，排除全部资产，不参与编译、发布或产品项目依赖。该新锁尚未通过实际 locked restore，不能标为已验证。

原有云工具链 SDK 10.0.401 的宿主、共享运行时及私有 `System.Security.Cryptography.Xml` 为 10.0.12。未运行该工具链，也未下载/执行已知受影响的 10.0.12 包。安全准备器在调用任何 `dotnet` 前读 SDK runtimeconfig/deps 和共享 runtime 目录，要求宿主与私有 XML 都是指定 10.0.13。批准前实际准备 Exit 2、`BootstrapExitCodes=[]`、`Bundle=null`，在缺少 SDK 10.0.402 元数据时即拒绝。

用户授权普通官方依赖 restore/self-contained 准备，但要求“新 SDK 先报告”。此前子任务 Pending 请求未视为批准；对应原件保留。父级后转来用户 **2026-10-18 09:22:58 UTC 明确“允许”**，仅批准官方 SDK 10.0.402 在快译云工作区独立目录下载、校验、解压用于构建。已据此下载和解压，SDK 批准不再是当前阻塞。官方 tar.gz 的 SHA-512 为 `1904a98dda2db34ba78ba3c4fb1b224455329af91c341d83547843c1839e112b04ae699ee92fa2449a8f62249196a938723078c0c60b9f65c88539d5687729c2`，实际全符。

直接依据为 [官方 10.0 下载页](https://dotnet.microsoft.com/en-us/download/dotnet/10.0)、[10.0.402 Linux x64 页面](https://dotnet.microsoft.com/en-us/download/dotnet/thank-you/sdk-10.0.402-linux-x64-binaries-targz) 和上述发布元数据。SDK 项目 [LICENSE.TXT](https://github.com/dotnet/sdk/blob/main/LICENSE.TXT) 为 MIT；[README License](https://github.com/dotnet/sdk/blob/main/README.md#license) 明确实际归档中的 LICENSE.txt/ThirdPartyNotices.txt 才是权威文本。获批后先在未解压归档中读取这两个文件：LICENSE.txt 1,116 bytes，SHA-256 `cfc21f5e8bd655ae997eec916138b707b1d290b83272c02a95c9f821b8c87310`，与官方 SDK 源码许可相同；ThirdPartyNotices.txt 77,436 bytes，SHA-256 `2dc8f8c5a39401e928b5784ab564eb8b3ceb99ead3df8f260e0cab7e0bbecc7a`，为信息性第三方 notices，原字节保留。未出现新的显式接受流程，没有代接受新协议。

官方候选文件只做 HTTP **HEAD**：200，Content-Length **240,279,509 bytes（229.15 MiB）**，BodyBytesRead=0。现有 401 目录只读 `du -sk` 为 623.59 MiB；据此估计 402 解压目录约 0.6–0.8 GiB，保留下载档案时新增约 0.85–1.1 GiB，建议为 SDK 预留 1.5 GiB，连同后续 restore/build/包副本预留约 4 GiB；这些是容量估计，不是 402 实际解压结果。当前 `/workspace` 可用约 28.69 GiB。HEAD 原件、直接来源和估算理由另存 `sdk-download-plan.json`。

上述 HEAD/容量估计发生于批准前。获批后的实际 SDK 下载为 **240,279,509 bytes**，新目录 `artifacts/session-runtime-security/sdk/sdk-10.0.402` 解压 **5,631 项，文件展开总量 640,467,796 bytes**；逐项拒绝绝对路径、上跳和特殊类型后解压。原 `/workspace/.kuaiyi-setup/dotnet` 未替换；只对子进程设置新 DOTNET_ROOT/PATH/HOST_PATH，保持禁证书生成及无遥测。

## 当前新增环境阻塞

新 SDK `--info` 与 `--version` 实际 Exit 0，报告 SDK10.0.402、Host10.0.13；静态 SDK runtimeconfig/deps 同时确认私有 XML 为10.0.13。r02/r03 两轮定向测试均在首次使用初始化、进入 MSBuild 编译之前 Exit 1、**无 TRX**，不能算产品红或已执行测试。报错为 `NuGet-Migrations` 命名互斥量 IOException。

本轮仅追踪自有新 SDK child 的文件访问：`r03-firstuse-filetrace.log` 明确先查询 `/home/agent/.local/share/NuGet/Migrations/1` 不存在，随后 `mkdir("/home/agent/.dotnet",0700) = -1 EROFS (Read-only file system)`。这是当前实证的直接文件系统失败；不把之前对共享 `/tmp` 的推测当根因。共享 `/tmp/.dotnet`、未知锁和原目录均保留。没有改 HOME、伪造 first-use sentinel、跳过迁移、修改系统/信任/审计或申请越界权限来绕过。

用户要求出现额外权限先停止，故此前已报告新 SDK 用户级初始化目录条件；父级随后限定为只读调查。**尚未批准额外 `/home/agent` 写入，未重试写该路径，本轮也未请求放宽该权限**。已批准 SDK 下载/解压部分完成，真实 restore/audit/签名、C# 定向和完整回归、App/Worker 构建、发布/ZIP/提交推送依赖初始化环境条件，仍 Blocked/NotRun。

### 后续只读核查：目录来源与配置边界

父级随后明确只允许调查官方语义和当前路径，不重跑失败命令、不请求放宽 HOME 权限，也不试用替代路径。本次新增仅为调查和证据，产品代码没有变化，没有新增 .NET 执行或测试结果。

`statvfs` 显示 `/home/agent` 所在挂载 `ST_RDONLY=true`；`findmnt` 的非敏感字段显示根 `/` 为 overlay / ro，而 `/workspace` 为 overlay / rw。原 trace 第4079行是**内核 EROFS**，原执行工具没有返回自动审批拒绝消息；但只读挂载本身执行着当前文件系统边界，不能据此声称不存在平台策略限制，更不能据此换路写入。

原 SDK 子进程的配置由未改的 `artifacts/session-runtime-security/run.py`、r01 `--info` 与 r03 trace 互核：

| 项目 | 原子进程设置或观察 |
|---|---|
| HOME | `/home/agent`，未改 |
| DOTNET_CLI_HOME | `/workspace/.kuaiyi-setup/cli-home` |
| DOTNET_ROOT / HOST_PATH | 本轮独立 `sdk/sdk-10.0.402` 及其中 `dotnet`；PATH 只增加该 SDK 前缀 |
| NUGET_PACKAGES | `/workspace/.kuaiyi-setup/cli-home/.nuget/packages` |
| XDG_DATA_HOME / CACHE_HOME / RUNTIME_DIR | 未设置 |
| TMPDIR / NUGET_SCRATCH / NUGET_MIGRATION_MODE | 未设置 |
| 禁证书、遥测 | `DOTNET_GENERATE_ASPNET_CERTIFICATE=false`、`DOTNET_CLI_TELEMETRY_OPTOUT=1` |

已尝试的两个定向命令 r02/r03 使用同一配置。CLI 已在工作区查找 `cli-home/.dotnet/10.0.402.dotnetFirstUseSentinel`；NuGet 又查询默认用户位置的 `Migrations/1`（ENOENT），随后 Mutex 初始化创建 `/home/agent/.dotnet` 被拒。未尝试工作区 XDG_DATA_HOME、另一个 CLI_HOME、TMPDIR、HOME 覆盖或任何迁移跳过参数。

[官方 CLI 环境变量文档](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-environment-variables#dotnet_cli_home) 支持 DOTNET_CLI_HOME 定位 CLI supporting files / first-run sentinel，但不证明它覆盖所有 CLR 命名互斥文件。原 runner 还保留了 `DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1` 这个旧设置；同一官方文档明确该变量自 .NET Core 3.0 后不再受支持，本轮并未用它跳过迁移，原栈也证明 MigrationRunner 实际进入。

[官方 NuGet MigrationRunner 源码](https://github.com/NuGet/NuGet.Client/blob/dev/src/NuGet.Core/NuGet.Common/Migrations/MigrationRunner.cs) 的 Unix 迁移**记录**路径显式使用 XDG_DATA_HOME；未设置时取 UserProfile/.local/share/NuGet/Migrations。但同一 Run 先创建 `new Mutex(false,"NuGet-Migrations")`，成功获取后才创建迁移目录并执行实际迁移。故“迁移记录可选工作区”不等于“已证当前 CLR mutex 目录可迁移”。此链接是官方当前 dev 源码，未与 SDK 所带 `NuGet.Common 7.9.0-rc.611+a8dd0577…` 建立精确源码版本绑定；本地字符串存在不当成配置已验证。

调查中有一项单独的网络拒绝：Python urllib 查询 `https://api.github.com/repos/dotnet/runtime/git/trees/v10.0.13?recursive=1` 返回 `Tunnel connection failed: 403 Forbidden`。无法证明该请求已到 GitHub；该目录树查询已停止，未重试或改用其他工具/路径获取同目录树。与本地 EROFS 分开记录。

目前最小安全动作是回报这些事实、保持 SDK 依赖步骤暂停，等待平台明确受支持的可用初始化条件；不提出执行 HOME 覆盖、权限/挂载修改、伪造 sentinel 或跳迁移。没有已证实、可直接在当前授权下执行的完整工作区替代方案。

## 已完成的独立准备

按已有依赖授权从官方 NuGet flat-container 下载两个 **10.0.13** runtime nupkg，仅检查字节与 ZIP，没有执行 DLL。NETCore 为 39,979,592 bytes，Desktop 为 39,195,063 bytes；两者 SHA-512 均逐字节等于官方 catalog 与新增锁。nuspec 均为 MIT expression，未声明 `requireLicenseAcceptance=true`；包内 LICENSE 文本已有 SHA-256。**NuGet 签名验证仍 PendingSafeSDK**，不将仅 hash/CRC 合格写成签名通过。

实际获取官方 NuGet VulnerabilityInfo 索引/base/update。当前数据包含两个旧 runtime 和旧私有 XML 的适用告警；对稳定版本 10.0.13 的辅助区间检查为零匹配。这是调查证据，不能替代尚未运行的真实 NuGetAudit。准备器现明确使用官方包源和官方 auditSources，`NuGetAudit=true / all / low` 与 `TreatWarningsAsErrors=true`；没有空目录审计源、Suppression、NoWarn 或警告降级。[NuGet 官方说明](https://learn.microsoft.com/nuget/concepts/auditing-packages) 说明 auditSources 应提供实际 VulnerabilityInfo。

独审要求的两道门已写入准备器：

- 审计结果需绑定精确项目、合法 JSON 形状和独立普通 list 库存，拒绝任何 problems/errors/warnings 或漏洞。runtime 库存必须恰为两项 10.0.13。App 合法零 PackageReference 只能由同项目实际 assets 的纯 project 库存证明；不能将任意空审计当成功。
- 发布目录及 Store 准备后的选定 generation 均逐文件核对已校验 nupkg 的 RuntimeList SHA-256。Core 187 项＋WPF 223 项含 WindowsBase 唯一重复：精确 Core/Desktop 同版本对明确选择 Desktop WPF 实现，其余不同 hash 冲突拒绝。MIT notices 入包。新 C# 声明检查还核 `.deps.json` 声明的实际 runtime/native/resource 文件及路径、数量上限。

## 实际验证与仍未验证

旧 helper 对两个无效审计实测接受，封存红原件 `audit-incomplete-red.json`（2F）；修复后同两份输入均拒绝，绿原件另存。Python 离线安全门最终 **15 个测试方法通过**，含空/错项目审计、实际 runtime 库存漏包/旧版本、漏洞/错误、私有 XML 门和发布文件篡改。包含多组 subTest，不将方法数当展开 case 数。

真实两份 13 nupkg 的离线映射曾因 WindowsBase 冲突拒绝合法合成输入，红原件 `runtime-windowsbase-red.json` 保留；修正合并规则后 409 个唯一路径全部与包字节相符，交换包顺序图相同；将 CoreLib 改成公开 marker 后拒绝。该实验仅解压公共 runtime 字节到本轮自有临时目录后清理，**不是 dotnet publish，也没有执行 runtime**。

新增 C# 16 个声明文件用例尚 **NotRun**。本轮未进入 .NET 编译、完整 Core/Provider 回归、App/Worker 构建、locked restore、NuGet list 实际输出验证、签名验证或 self-contained publish。准备器和新审计库存仍须在完成用户级初始化的安全 SDK 上实跑；没有 ZIP、可运行包摘要或 Windows 安装/加载闭包证明。不能借用父轮 1888P/2332P 声称本轮回归通过。

Windows/WPF/实际 Dispatcher、STA、可见键盘与 Console 取消、Job、OCR、DPAPI/ACL、截图、物理 250/600ms、真实模型与完整 P6/P7 仍 NotRun。旧图片/idle/Core/Job/r10/r17/r20 间歇根因各保持 Unknown；生产图片/分类复用 Off、来源/密码/完整性/取消/清理门不变。Skia 四包新许可未接受、403 未绕过；拒绝的 Actions/CIM 未重查或换路。未部署/Release、未改 main/强推、未动凭据/安全权限、未知锁/数据/日志/进程保全。

机器索引：[`2026-10-18-session-runtime-security.json`](../test-evidence/2026-10-18-session-runtime-security.json)。本轮源码仍为未提交工作，待 SDK 用户级初始化目录条件解决、完成真实验证后再封存提交和正常推送；SDK402本身已经明确批准。


## 2026-10-19：父审所提全局 RID 冲突核验

父审怀疑 prepare 的全局 `-r win-x64` 会重现普通恢复 r13 的六库 NU1004。按已提交脚本 AST 原样取全部12项 props 实跑：r26 Exit0，8项目成功；仅删 `-r win-x64`、其余参数不变的 r27 反而使六库 NU1004；r28恢复原样再次 Exit0，八份真实 assets 与 r26成功快照逐字节相同。根本差异是 prepare 的 `KuaiYiTextAlpha=true` 经 Directory.Build.props 选用全部包含 win-x64 的 text-alpha-locks，而 r13/r15 用普通锁。故这一具体P2未复现，不删除匹配当前锁的参数；生产源码及所有锁不改。restore/publish 的 RID 与12项 props 静态一致，publish未执行。

本次只核此调用疑点；不重试在线签名、不跳签名门、不提前publish。签名仍为首个已知阻塞，后续打包与Windows验收仍NotRun，不能据本次restore成功保证整条包链路通过。见[精确调用、负对照与恢复原件](../test-evidence/2026-10-19-session-rid-verification.json)。
