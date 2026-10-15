# 生产清除当前内容

本批唯一父为已接收 `74ad0c9b0266f33b3708bf3ecb55d870306aff70`，正常分支 `dev/overlay-clear-content`。起始HEAD/远端相同、工作树干净、单worktree；本任务唯一写入者、独审只读，未知锁/进程不动。main保持 `51d077481cf6303602fcf5d2718213da9cf05178`。

## 命令与保留范围

SPEC8/14.1的“清除当前内容”现接生产主按钮菜单、浮窗工具/更多菜单与设置隐私页，调用同一命令，立即生效且不依赖保存设置草稿。复用TextTranslationCoordinator.Clear，新增ClearAsync仅同步清除后等待本次捕获的旧jobs，不Dispose Provider；未来有效任务不归旧Clear所有。

每次Clear均先推进原Gate epoch/hotkey输入屏障，取消旧请求、使旧capture ticket/offer失效；即使取消回调抛错，也继续各个独立清理步骤并记录失败。清全部保留版本/快照、原文/Markdown/正文/测量控件、阅读/提示/等待/自动展开/Drop hover及恢复意图，撤销Selection和Drop候选。保留On/Off、Hidden和Settings等既有状态，不启用、不上传、不删除磁盘文件、配置或密钥。清后仍可接受新的有效用户任务；旧版本与重生成入口无法恢复已清内容。

先同步清文字和控件，再等待旧Coordinator jobs、共享截图cleanup及既有Drop reconciliation。每次Clear都有独立结果修订，只共享截图所有权结算；重复Clear不会漏掉其间新开始的任务。完成/错误提示同时检查结果修订、退出/锁屏、新generation/reservation与Capture，允许本轮ScreenshotIntent.Dispose自身推进epoch，不覆盖后来的截图/长文本确认或新正文。

Drop仍由原reconcile结算Run/Control/owner，Clear不并发TryFinalize；旧peer未换出就返回未确认。退出在任何await前锁存dropStopping，Refresh立即返回，Clear只等待已有reconcile，在途reconcile不得创建新peer；失败保持锁存和原owner，退出重试只结算该owner。这样截图finally等迟到Refresh不会与退出对同一owner并发TryFinalize。已清数据不回填，但新On/Drop接收器可按原门控重新等待未来主动投放。Selection的原250/600ms、COM/broker边界没有改变；未宣称取消令牌能中断真实COM或证明进程已退出。

## 现有图片任务的释放

本批没有新增图片原文预览或模型路由，只清理既有本地截图路径。真实Prepare任务保留cleanup fault，observer只报告且受结果修订保护，不把错误吞成成功。截图Stop共享同一pending Task，Clear/重复Clear/退出不并发核同一owner；阻止未结算期间的新截图。单纯owner待确认仍保留同一owner供重试，成功重试可恢复；位图/mask等其他未确认故障继续fail closed。

位图从创建后、首次填充前进入Own保护，覆盖copy、UI构造和Show失败。Release尝试关闭并断开Image.Source、Panel.Children、Window.Content，再逐行尝试写零；部分失败仍尝试后续行，并保留未确认。Prepare finally分步尝试selection/snapshot/intent/窗口恢复/handler解绑及owner结算，不让前一失败跳过后续资源。恢复窗口回调绑定截图owner/修订、Hidden/锁屏/退出；finally撤销资格，旧回调不能重显窗口或影响新截图。Stop即使cancel callback或原task故障仍尝试已知owner，无法确认时不报告“当前内容已清除”。

原文/译文是托管字符串，清除拥有的引用和版本缓存，不承诺运行时、HTTP系统缓冲或未知副本被物理擦除。真实受控Snapshot/Crop/PNG字节缓冲的Dispose归零可在Linux验证；WPF位图调用只通过数据端口验证协议，不能称原生像素或窗口已实测。

## 实际测试与审查

最终Provider **884/884**（原840测试名多重集合完整保留，新增41项Clear和3项Drop调度，失败0/跳过0），App Release交叉构建 **0警告/0错误**。测试链接真实清除/Hide/Reopen/原文/截图生命周期与预览清理生产方法、真实Core/Coordinator/Provider及实际ScreenshotPixels/Intent/ImagePolicy源码；新增Clear用例的native UI/owner/Drop adapter明确为数据调用端口，使用无socket connector，不调用模型或真实截图；完整884套件仍含既有MockProvider本地HTTP测试。Drop专项直接链接实际Refresh/Settle/Stop调度，原生reconcile/receiver/capture操作为显式端口，不能冒充真实OLE所有权验证。

覆盖：排队/已渲染停止、四种迟到事件、四次生成的三版本和offer清除、原文控件及旧定位、capture ticket与真实hotkey input失效、On/Off/Settings/Hidden、反复清除和新任务穿插、Clear与退出共享结算、旧完成/错误、无Begin的新截图/确认、真实Gate与截图取消回调抛错、已知owner重试、Drop结算失败、Snapshot/Crop/PNG缓冲归零、preview close/逐行wipe失败、首次copy/部分UI构造失败、旧Restore与新owner隔离。新增Drop顺序覆盖：暂停Run/Control/finalizer时的迟到Refresh/Clear、已有reconcile与退出共享等待、失败保held owner及显式退出重试。App交叉构建覆盖实际菜单/设置/截图调用；WPF交互仍未运行。

首专项r01 **17通过/12失败（共29）**，全部在同一Empty检查：命令未显式清“有新内容”状态，端口不会执行NativeFrame的隐藏副作用。补命令层重置及canonical Compact（Hidden保留），r02为29/29；这是同一状态在12例显露，不是12种独立缺陷。随后独审补齐同步异常、首次bitmap填充保护、原task故障保留、可重试owner和恢复/完成资格，r03为881/881；最终保留全部错误聚合与hotkey断言后r05仍881/881。独审确认退出等待owner期间迟到Refresh会重复调度reconcile后，补最小停止锁存及3项真实调度测试，r07最终884/884。r04/r06 App均0W/E；r08命令漏交叉构建属性EnableWindowsTargeting，NETSDK1100、0W/1E且未编译；r09补回既有参数后最终App0W/E。14份所有成功/失败log/TRX使用唯一新名称、预检和noclobber，原字节持久提交并设精确-text；前批55份已提交回执原样核验。详见[固定证据](../test-evidence/2026-10-15-overlay-clear-content.json)。

独审只读，未运行SDK/Actions/Windows。真实WPF菜单/设置/窗口/位图/选择/截图、Drop/COM/broker/进程结算和UI/HTTP取消时限均 **NotRun**；B06/B12/B07、完整P2和Core.Tests全工程不因此通过。图片原文预览按用户要求留下一轮，不增外围工具或Windows CI。旧覆盖前字节仍Unknown/未保全，旧证据不回填。

所有dotnet前先export且命令前显式 `DOTNET_GENERATE_ASPNET_CERTIFICATE=false`；证书存储/信任Unknown，不读库/配置/trust/delete。250/600ms、来源/密码/完整性/取消/清理全部安全门和生产离线复用Off保持。无新依赖/费用/凭据/安全权限/协议/真实调用，Actions不查询不绕Forbidden，远端run数Unknown，无CMake/Windows实验、main/强推/部署。
