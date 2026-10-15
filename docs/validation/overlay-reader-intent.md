# P2-04：实际浮层的阅读意图接线

接续已独审的4a99e26078ff9ac5dcbbc2ef64f7dd5421fbedf5，唯一集成写入者持锁，独审只读。正常产品分支dev/overlay-reading-input不匹配保留的八个Windows workflow；八文件与已发布5acccc75352499e0b0193cebb765584b208c6fa9逐字节一致。首次正常推送已包含4a99及此前f741归档，两提交相对该远端基线共10文件、954增加/6删除，无workflow改动。旧本地成果的“不推送”描述是前一批历史，不适用于本次明确授权的产品推送。

## 实际遗漏与修复

原ScrollChanged订阅在ExtentHeightChange==0且VerticalChange非零时直接计算follow。该事件不能证明来源为阅读输入：文本赋值、程序化ScrollTo和布局夹取也能走此路径；相反，阅读移动与extent变化合并时又会被原条件忽略。旧专项端口直接设置follow=false，不能验证这条生产接线。这是源码路径发现，尚无Windows事件回放，不能写成实机复现。

移除普通ScrollChanged订阅。wheel、八种导航键、左键选择/拖动、touch和ScrollBar.Scroll都进入生产ObserveBodyReaderIntent：先使旧回调失效并立即暂停跟随，然后以Background优先级读取当前extent/viewport/offset，仍用24DIP阈值决定是否恢复跟随、隐藏“有新内容”。回调同时检查scroll修订、result修订、退出和锁屏；输入只观察，不设置Handled。复制等非导航键不再被当作阅读滚动。

[ScrollBar.Scroll官方文档](https://learn.microsoft.com/en-us/dotnet/api/system.windows.controls.primitives.scrollbar.scroll?view=windowsdesktop-10.0)说明该冒泡事件用于滚动条交互，代码改变Value不会触发它；用handledEventsToo覆盖已处理的滚动条操作。它提供比鼠标捕获推断更窄的入口，不从任意ScrollChanged归因用户。Background优先级低于Input/Render/Loaded，依据[DispatcherPriority官方文档](https://learn.microsoft.com/en-us/dotnet/api/system.windows.threading.dispatcherpriority?view=windowsdesktop-10.0)，但不把优先级当作完整布局完成证明。

新Flush若先于Background观察执行，会使观察失效，保守保持暂停；后续阅读操作或“有新内容”可恢复。隐藏先Invalidate，重显不复活旧观察；Begin和成功SelectVersion仍递增resultRevision。已发给WPF的ScrollTo不能被此改动撤销。触摸惯性等没有后续输入通知时也可能保守暂停。没有新计时器、轮询、窗口、缓存或网络。

## 实测与边界

新增6项实际生产方法policy用例（含24/25DIP两例）：输入立即暂停、extent与阅读同时变化、返回底部阈值、刷新抢先的保守暂停、隐藏/结果切换失效。原11项延迟回调测试不扩充；Provider完整538/538通过，0失败/跳过。App Release locked/no-restore Windows targeting交叉构建0警告、0错误。独审检查真实接线及端口测试并通过，本批独审未另启SDK。普通程序化ScrollChanged无接线是源码核验；端口ChangeGeometry并非WPF事件实测。

Windows实际布局、路由、触摸惯性、阅读体验和完整B组仍NotRun；P2-04仍InProgress。原CMake生命周期根因、实际debug格式、精确MSVC版本和ABI命令仍Unknown；不补造四原文件、不增加实验。250/600ms、来源/密码/完整性/取消/清理门保持。没有真实模型调用、main修改、强推、部署或新增费用/权限。

## SDK证书提示保全

前批独审日志和TRX原字节保留并复核hash；日志明确报告安装ASP.NET Core HTTPS开发证书。未执行trust或delete，实际存储与信任状态Unknown，未读取证书库。前批精确独审CLI命令未记录在现有日志中，本次不能恢复或补造。当前两次实际命令另存私有commands.json与日志，均在首次dotnet调用前显式export DOTNET_GENERATE_ASPNET_CERTIFICATE=false，复用既有CLI home，且子进程继承该设置。

[微软dotnet环境变量文档](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-environment-variables#dotnet_generate_aspnet_certificate)规定该变量默认true、false关闭生成；DOTNET_CLI_HOME只是CLI支持文件位置，不能推导证书隔离。当前两日志没有重复安装提示，这不证明私有存储状态未变。公共Actions API默认网络只读返回URLError，未重试或新增权限；已核八个workflow均不匹配产品分支，不能把API不可用写成实查零run。

固定测试和源码核验见[证据](../test-evidence/2026-10-15-overlay-reader-intent.json)。
