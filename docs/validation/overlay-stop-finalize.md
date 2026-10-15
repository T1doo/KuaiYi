# 本地停止生成的终态刷新

本批从已接收 `70c03a5f2d40d294ff04a3bdd9396526bf77cb7e` 建立正常开发分支 `dev/overlay-stop-finalize`，补前批明确留下的 Stop 生产缺口。新任务为唯一写入者；`/root/inline_review` 只读独审，不恢复旧实例。实际核工作树、唯一 worktree、HEAD/远端祖先和默认 HTTPS 网络；远端 main 保持 `51d077481cf6303602fcf5d2718213da9cf05178`，未知锁与进程不动。

## 原因和行为

原 StopResult 会取消请求并标记本地结果停止，但没有置 dirty、启动 Flush 或刷新版本标签。TextTranslationCoordinator 的主动取消路径不会再投递 Stopped；迟到投递也应被原 Gate 拒绝。因此已排空的最后一段无法进入终态解析，标签可能仍显示“生成版本”。例如没有末尾换行的闭围栏，流中暂作代码字面文本，停止后应识别闭符，同时保留精确原始 Source。

修复仍在原 ShellWindow StopResult：退出或无生成任务时返回；先 Gate.StopGeneration，再 MarkStopped，清 pendingReveal/等待状态，刷新真实版本标签，并复用原 50ms coalesced Flush。已有待刷新的 delta 不另开定时器。Flush 从当前选择版本读取，保留已接纳文本、三版本缓存、原始复制路径、暂停阅读/选区/双偏移、收起状态及旧取消和迟到守卫。不调用 Fail、不回滚版本，也不引入第二套状态机。

为在 Linux 直接回归这条生产链，现有 Receive/Apply/Measure/Open/Flush/Collapse 移至 `OverlayResultFlow.cs`，现有标签和 helper 移至 `OverlayResultLabel.cs`；除 Stop 外逐段与基线精确相等。原 timer lambda 等价命名为 FlushResultTick。Provider.Tests 链接这两个正式 partial，与先前正式 Markdown/滚动链共同执行；端口仅提供 Dispatcher/控件数据/计时器调用及显式 DesiredSize，不复制 Stop/Flush/标签逻辑。

## 实际验证和失败记录

最终 **Provider 802/802，失败0、跳过0**，包含前批774及新28；Markdown/阅读相关合计281。App Release Windows 交叉构建 **0 Warning / 0 Error**。独审增强了“Stop 排队后切换旧完成版”的实际渲染断言：不仅核缓存/标签，还核 MarkdownModel Source 和原生 Paragraph/Run 文本。

最终同一28用例、同一修正夹具，临时仅换回基线 Stop 的 `r09` 为 **7通过/21失败**，全部为断言失败、无夹具 timeout；EXIT 恢复后的生产文件与事先备份字节相同。修后 `r10` 全量802通过。`r06`旧实现7/28、`r07`修后802/802为增强断言前的中间回执，均原样保留。App `r08` 后生产代码未变化，因此不重复构建。

28用例覆盖：排空停止、待刷 delta、无换行闭围栏与基础表格的终态转换、真正未闭围栏/未闭行内及不支持语法的精确保文、空结果等待、重复/无任务/已完成/退出 Stop、四类已排队迟到事件、旧 Stop Tick 前新 Begin、收起/展开、代码与表格暂停阅读及选区/横纵偏移、较新输入隔离旧恢复、真实 Coordinator 主动取消不发 Stopped、四次生成仅留三版本、旧停止版与旧完成版选择，以及 pending confirmation 和独立 connection test 边界。

全部失败回执保留，不能把夹具失败计作生产 Stop 缺陷：

| 回执 | 实际结果 | 解释 |
| --- | --- | --- |
| r01 legacy | 3/23通过，20失败 | 19项旧行为断言失败，另1项合成连接夹具在第二次生成等待超时 |
| r02 fixed | 22/23通过 | 同一夹具等待超时；Stop 修复已生效 |
| r03 diagnostic | 0/1通过 | 诊断记录版本2 Begin已出现、Running未完成，但合成 connector 未进入；网络内部根因未证实 |
| r04 fixture fixed | 23/23通过 | 仅把夹具自身无限等待显式链接原请求 Gate 的取消令牌，未改生产 HTTP 代码 |
| r05 expanded | 27/27通过 | 增补待确认、connection test、旧完成版和阅读输入 |
| r06 / r07 | 7/28；802/802 | 增强最后两项 render 断言前的旧/新对照 |
| r09 / r10 | 7/28；802/802 | 最终相同28用例旧/新对照 |

真实 TextTranslationCoordinator/ProviderClient/RuntimeGate/cache/SelectedSnapshot 参与取消与标签测试。transport 使用 AuthNone、固定 public.invalid 和无 socket 的本地等待；增量是明确的合成 presentation 输入，绝不称真实 HTTP 或服务端取消验证。端口手动触发原 Tick，50ms 是既有配置，不是实测 UI p95。

每个 dotnet 执行均先 export 且命令前显式 `DOTNET_GENERATE_ASPNET_CERTIFICATE=false`。最终命令：

```bash
export DOTNET_GENERATE_ASPNET_CERTIFICATE=false
source /workspace/.kuaiyi-setup/activate.sh
DOTNET_GENERATE_ASPNET_CERTIFICATE=false dotnet test tests/KuaiYi.Provider.Tests/KuaiYi.Provider.Tests.csproj --no-restore -c Release --logger 'trx;LogFileName=r10-provider-final.trx' --results-directory artifacts/stop-finalize
DOTNET_GENERATE_ASPNET_CERTIFICATE=false dotnet build src/KuaiYi.App/KuaiYi.App.csproj --no-restore -c Release -p:EnableWindowsTargeting=true
```

实际 stdout 均重定向到唯一未存在路径；使用 noclobber 和预检，无本批同名覆盖。完整Core.Tests工程本批 NotRun；相关正式Core模型由Provider用例实际执行。

## 回执保全与限制

前批774批次的全部16份现存原始log/TRX/诊断逐字节复制到唯一 `2026-10-15-tail-follow-originals__*` 路径并提交；旧七份740批次副本再次核hash，均保持。前批 frozen 覆盖警告与 transport 诊断也原样保存，首次执行及覆盖前字节仍 **Unknown/未保全**，不能用现存774回执恢复未知内容。旧证据不回填或改写。

本批19份原始成功/失败log和TRX也保存为唯一 `2026-10-15-stop-finalize-originals__*`。这35份新增副本逐路径设 `-text`，避免 Windows 检出改变字节；[固定证据](../test-evidence/2026-10-15-overlay-stop-finalize.json)保存SHA/字节数/XML计数、精确方法迁移与32个生产/测试文件manifest。

8 workflows、43依赖锁、1581既有证据、45 Core生产文件及2312个非修改既有文件与父提交原始blob字节相同。250/600ms及来源/密码/完整性/取消/清理全部安全门保持，生产离线复用 Off。未新增依赖、图片/链接打开/远程加载或执行入口，无真实模型/凭据/费用/权限/协议、Windows/CMake实验或新CI；Actions此前Forbidden，本批不查询、不绕路，服务端运行数Unknown。证书存储/信任仍Unknown，不读库/配置/trust/delete。

真实 WPF 排版、选择/系统复制、阅读位置/滚动/Loaded与输入路由，以及 UI/HTTP取消时限均 **NotRun**；B12、B06/B07、P2-04和完整SPEC11.5不因此通过。原生验收仍使用已有清单与固定合成入口，本批不新增Windows验证工具。
