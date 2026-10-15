# §4.6/B06：末尾原生区域跟随与 WPF 验收准备

本批从 `8226f11d66719afab2413e06166591c3e245af54` 建立普通开发分支 `dev/overlay-tail-follow`，起始工作树干净，远端该提交一致，main 仍 `51d077481cf6303602fcf5d2718213da9cf05178`。核实际文件、Git/祖先、单一 worktree/写入者和 Linux 环境；独审只读，不恢复旧实例或删除未知进程/锁。Actions 先前 Forbidden 后继续不查询、不换路、不 dispatch。

## 原回执独立核验与保全

七份表格批原始日志/TRX逐字节 SHA256/长度匹配提交内 manifest；XML真实 Result 节点、Counters 和类计数一致：最终740/740、专项218/218、最终相关类219、表格滚动40→41；两次App均0W/E；首xUnit2031是0执行编译失败。22源文件同时绑定基线提交blob/原manifest，前29份原回执hash保持。没有把历史执行称为新代码复跑。

七份原始字节复制到 `docs/test-evidence/2026-10-15-table-scroll-originals__*` 唯一新路径，原 artifacts 文件保持；本次因此形成持久化证据，而非只有hash。仅给这七份副本及固定语料追加精确路径的 `-text` Git属性，避免Windows检出换行破坏hash。两份TRX只有runner StdOut，没有 ErrorInfo/StdErr；内容是合成测试名称/参数、构建路径、容器/GUID等运行元数据，没有采集用户原文或真实秘密。独立审查原始字节、XML及副本，详见[独立核验记录](../test-evidence/2026-10-15-table-scroll-independent-audit.json)。

## 最高价值生产缺口及实现

SPEC §4.6/B06要求到底跟随和暂停阅读。旧“有新内容”只调用外层 `body.ScrollToEnd()`；嵌套代码/表格每次追加却恢复旧纵偏移，导致内部新行仍看不到。本批直接修正式 `MarkdownFlowView → MarkdownCodeRegion/MarkdownTableRegion → OverlayScrollFlow`，保留原模型、限制、原始复制和门控。

FlowView提供当前全局follow策略，并按**incoming**块结构、在更新前和新控件挂载前标记唯一末块。仅末尾代码/表格可参与跟随；前面稳定区域或以普通段落结尾时不滚旧内层。明确“有新内容”先用原入口重置阅读修订，再请求当前末尾区域跟随并滚外层；区域 Loaded 恢复在原局部/全局输入修订、result、锁屏、退出和Dispose守卫内执行。

普通追加在原全局follow且局部原距底 `ExtentHeight-ViewportHeight-VerticalOffset <=24 DIP` 时使用 `ScrollToEnd()`；其余情况保原纵偏移。多次追加先于布局时合并首个有效快照和首次底部决定，最终回调用新布局的末尾；保持原选区及水平偏移。明确跟随可覆盖旧恢复，输入抢先则拒绝旧回调。局部用户阅读仍保持原暂停政策，需明确跟随/新结果恢复；本批未将“任何外层到底”当作表内阅读完成。

首次真正Loaded可在follow状态显示新区域末尾；每区域只执行一次，主题/重挂Loaded不会绕过24DIP规则强制跳底。待回调区域变为非尾时，取消滚到底但保留原选区/水平/阅读偏移恢复；真正的新输入、版本、锁屏、退出、Dispose仍拒绝整次旧恢复。测量视图无reader guard/follow策略，不排用户恢复任务。Dispose解绑新增Loaded事件并释放follow委托。

独审补出并修复主题重载、部分pipe行将保留Table从非尾提升为末块、失去末尾资格时连锚点恢复也丢弃三条边界。官方 [ScrollToEnd](https://learn.microsoft.com/en-us/dotnet/api/system.windows.controls.primitives.textboxbase.scrolltoend?view=windowsdesktop-10.0)与[Loaded](https://learn.microsoft.com/en-us/dotnet/api/system.windows.frameworkelement.loaded?view=windowsdesktop-10.0)用于API/生命周期核对；真实WPF行为仍需实测。

## 实际测试及失败

最终 Provider **774/774，0失败/跳过** = 原740 + **32**项生产跟随协议 + **2**项固定合成语料Core检查；相关子集253。新测试直接链接实际FlowView/CodeRegion/TableRegion/Viewport/OverlayMarkdown/OverlayScrollFlow；数据端口只提供显式geometry/selection和方法调用记录，不模拟WPF排版、路由或选择。覆盖24/25DIP、后布局extent、明确跟随、暂停/水平/选区保留、逆序/合并、输入/结果/锁屏/退出/清空、初次/重载、尾部升降及混合区域。前740项全部复跑。

App和既有GrowthHarness最终Windows目标Release交叉构建各 **0 Warning / 0 Error**。GrowthHarness原assets缺失，因此按已有锁做一次locked restore，8/9引用已有；无新依赖或锁变更。所有dotnet均显式 `DOTNET_GENERATE_ASPNET_CERTIFICATE=false` 并export；证书存储/信任仍Unknown，不读库/配置，不trust/delete。

```bash
export DOTNET_GENERATE_ASPNET_CERTIFICATE=false
source /workspace/.kuaiyi-setup/activate.sh
DOTNET_GENERATE_ASPNET_CERTIFICATE=false dotnet test tests/KuaiYi.Provider.Tests/KuaiYi.Provider.Tests.csproj --no-restore -c Release --logger 'trx;LogFileName=provider-frozen.trx' --results-directory artifacts/tail-follow
DOTNET_GENERATE_ASPNET_CERTIFICATE=false dotnet build src/KuaiYi.App/KuaiYi.App.csproj --no-restore -c Release -p:EnableWindowsTargeting=true
DOTNET_GENERATE_ASPNET_CERTIFICATE=false dotnet restore tools/KuaiYi.GrowthHarness/KuaiYi.GrowthHarness.csproj --locked-mode -p:EnableWindowsTargeting=true
DOTNET_GENERATE_ASPNET_CERTIFICATE=false dotnet build tools/KuaiYi.GrowthHarness/KuaiYi.GrowthHarness.csproj --no-restore -c Release -p:EnableWindowsTargeting=true
```

失败和不完整溯源不隐藏：

- 首专项 **247通过/1失败**：闭围栏转prose后错误丢弃所有恢复，水平偏移80→0；生产修为仅撤销跳底，补选区和纵偏移断言。原 `focused-first.log/.trx` 保留；修后248/248为中间结果，后来补独审3例使全量772/772。
- 首合成语料全量 **772通过/2失败**：固定表格cell使用下划线，受限复杂语法正确整表字面回退，未形成Table；改合成cell为短横线，没有放宽解析器或结构断言。原 `provider-corpus-final.log/.trx` 保留；随后774/774。
- 冻结命令曾遇 exec-server transport disconnected。最初误判断未启动，后续复用同名文件，VSTest明确提示覆盖 `provider-frozen.trx`。首次是否执行及覆盖前字节 **Unknown/未保全**；现存冻结log/TRX为774/774，保留覆盖警告和诊断，不补造首轮结果。七份要求保全的原始回执及其副本、其他本批回执未受影响。`provider-final-fixed.log/.trx`也是先前独立774通过回执，但不包含最后加入的固定文本SHA断言；最后该断言由现存冻结774执行。

以上运行及hash见[固定证据](../test-evidence/2026-10-15-overlay-tail-follow.json)，当前实际日志在 `artifacts/tail-follow/`。完整Core.Tests工程本批NotRun。

## 原生 WPF 准备及未完成项

本环境 Linux x86_64、无DISPLAY/WAYLAND、shared仅NETCore/AspNetCore、缺WindowsDesktop；未尝试运行WPF/Windows/兼容层/远端探针。原生选择、横纵滚、真实字体/列宽/测量/Loaded时序/模板空间、阅读锚点、输入路由与系统剪贴板均 **NotRun**；B12、B06/B07、P2-04与完整§11.5未完成。

用户要求的验收准备只扩既有 `--actual-overlay`工具的独立 `--actual-markdown` 固定输入入口，不建新外围工具、CI或外部文件加载。复用正式ShellWindow，Selection/Translation/Drop/Hotkeys后端禁用、NoKeys拒绝秘密；原CI/default入口输入和行为保持。固定语料覆盖128行长代码、12列65行表、1200无空格字串及中文标点/样式/字面公式，明确惰性字符串只在围栏内。工具summary仍DesktopAcceptance=NotEvaluated；交互Exit0和自动几何布尔不代表B12。固定输入逐字节SHA由Core测试与保存文本匹配。

[更新的Windows清单](../p2-overlay-windows-checklist.md)给现有入口的命令及各场景、交互/自动前台区别与证据要求；本次没有Windows执行、截图或伪造测量值。另识别到用户停止未排终态Flush/版本标签的生产缺口（StopResult未置dirty；主动取消不会补Stopped），作为下一项，未混入本批修复或宣称已完成。“查看原文”折叠区等完整SPEC能力也未因本批而通过。

250/600ms、来源/密码/完整性/取消/清理全部安全门和生产离线复用Off保持；全部Core生产、8workflow、43锁及既有证据原字节不变。无main/旧分支修改、强推、部署、新费用/凭据/权限/协议、真实模型、Windows/CMake实验或Actions访问。最终SHA、远端和未提交差异另见交接回执。
