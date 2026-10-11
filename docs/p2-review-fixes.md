# P2 三项监督缺口修复复核

基线 `5a55d0474cbe1b500f047ff5b05a1c8dceda1918` 的P2尚未完整签收。本轮只修监督方指出的工具条可达性、包含生成中的版本上限及正文测量宽度，未新增安装、权限、模型调用或平台通过声明。规格准确条款为4.1/B11及9.9“重新生成和版本”（监督消息称9.5；本文原文9.5为专业提示词）。

旧版第四版Begin反例在真实OverlayResults/RuntimeGate上实际运行并失败，原TRX/日志保留。工具条与正文测量的旧版反例读取精确Git基线，保存原OverlayPresentation源码及可重跑脚本：旧220 DIP工具条扣48按钮，五个40 DIP操作仅能显示前四个；设置隐藏且无更多。旧MeasureBody优先窄条Plan，再Open改成420 DIP；30/300字符确定性换行夹具分别测成152/680而正确宽度应为128/368。这是源码及测量合同反例，未执行WPF字体测量，不能称Windows失败/通过。

OverlayToolbarLayout现在先保留设置，再为剩余项预留28 DIP“更多”。生产ShellWindow实际使用该计划放置按钮，更多打开真实ContextMenu且包含所有操作及当前会话版本；菜单打开时阻止悬停收起，关闭后恢复延时。设置点击直接调用原OpenSettings。极窄到没有28 DIP余量时保留原主钮右键/键盘入口；没有通过扩大卡片绕过工作区约束。等待状态的文字只放在余量足够区域，不挤掉设置。

OverlayResults的Versions现在始终包含当前生成中的记录，即使还没有正文。Begin先保存前版、记录当前显示版的rollbackVersionId，并立即将历史裁至两个；新增版占第三个槽位。流片段仅修改当前版缓冲，查看旧版不会被流拉回；切旧版不采集、不启动请求，可在Off缓存回看。下次Begin保护正在阅读的回退版并按创建顺序淘汰其余项。完成解除回退保护，停止保留部分结果/状态及回退入口；合法新来源清全部旧版本，非法Begin保持缓存。完整P4原文快照/API重新生成调度尚未交付，本轮只实现生产结果容器和本地版本选择。

OverlayFrames.OpenMeasured先生成BodyPlan，把同一对象传给实际WPF MeasureBody，再用该对象展开，避免等待/悬停Plan混入正文宽度。后续Grow使用实际正文Plan。新增生产合同测试覆盖短文、长文、窄条转正文及收起中反转，断言测量只调用一次、计划对象相同、显示高度正确、锚点包含与重复Grow不超高。测量夹具只隔离宽度合同，Windows字体/布局/帧率仍NotRun。

新增15项便携测试；两项旧断言随Versions明确包含生成中而加强为精确当前身份/旧停止身份及总数，未删旧用例或弱化失败断言。针对P2用例49/49；完整离线Core683+Provider77=760/760，Node63/63，无失败/跳过；25托管工程locked Release零警告/错误。第一次新增测试编译的参数顺序及分析器错误日志另存，修正后回归通过。

当前精确包、源码/证据/文档摘要和历史Git验证见[test-evidence/2026-10-11-p2-review-fixes-summary.json](test-evidence/2026-10-11-p2-review-fixes-summary.json)。独立新包与原P2包分开，十份旧包不覆写；待执行桌面检查见[p2-review-fixes-windows-checklist.md](p2-review-fixes-windows-checklist.md)。P0/E06仍Blocked、P1/P2仍InProgress，106整体验收仍NotRun。

新审查ZIP：1456604字节、68文件，SHA256 `6df677eae7778ed8669b81c81dd065987b5a76124676dda9223aa31590f69ced`；逐文件manifest及8个PE32+ x64入口通过静态检查，十份旧包大小/摘要未变。
