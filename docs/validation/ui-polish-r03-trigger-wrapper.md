# r03 触发包装候选：本地准备，未推送

本候选直接基于 f2b7c57acfedf21cd8d684e7b075e345c8da5597，仅修改四文件：原已注册 workflow 路径 .github/workflows/windows-ui-polish.yml、Python 精确 parent/FILES 常量、progress 及本说明。没有产品或探针源码变化；before e4d57e77426ab235c7219b375debd76bc072e00b / after e12c1ed8ff351f8300dbdaca18585d7994493317 原样，Windows Job supervisor、Popup/程序键路由、Full/Reduce、中间几何+fade、原窗口/tray/tasks结算和UNKNOWN持有门全保持。现有15分钟job、14分钟supervisor、8分钟prepare、视觉总计6分钟不改。

已知事实：2026-10-26执行分支 dev/ui-polish-visual-r03 仅一次成功推送原 f2；完成时刻未单独记录，只能界定13:35:02–13:35:46UTC。13:50:26UTC新鲜查询，两remote仍f2、工作树当时clean、branch runs空、checks0，仓库queued/in_progress均空；r03 workflow API404，但对应branch文件Contents API200。注册12入口全active，包含旧UI路径ID395782622，不含新r03路径。旧run40217224688确由原路径push调度，最终Fail，不是本次新结果。原始观察/诊断stdout+stderr及长度SHA在本原环境artifacts/2026-10-26-ui-polish-r03保全，没有上传原件。

无实际PushEvent payload，GitHub最终采用的path diff未知。不能把404当权限拒绝、不能确定新路径注册、路径过滤或服务延迟哪个是根因。公共Events API非实时，缺事件也不能证明未交付。本包装仅是复用已注册入口的最小候选，并不保证解决未知调度问题，不是变更权限或绕过拒绝。

旧注册路径改用同原r03 supervisor步骤及artifact范围，on.push严格限定同执行分支和旧路径自身。原新r03 workflow文件保留原字节；对既有执行分支f2的后续正常四文件更新，只有旧注册路径匹配changed-path，新r03路径不在diff中。Python parent必须f2、完整四文件差异、clean和固定before/after；C#原ref/attempt1门不变。无dispatch/schedule/自动重试入口，不改main，不force，不新凭据/费用/安全或DPI/theme设置。

本地准备分支 dev/ui-polish-visual-r03-preparation 不匹配任何执行入口，当前不推送任何分支，不运行Windows/原生/进程树探针或复跑既有测试。只做必要AST/YAML、完整diff门和不变字节静态核对，并独立只读审查。原f2单次push没有可见job，所以原生运行NotRun，不能称已消费Windows执行或成功。

后续若原f2延迟run出现，应先识别唯一原run并跟进原批准预算，本候选保持不推。若确需本新包装触发，必须先确认原push调度是否仍未知、无延迟在途，给出新完整候选SHA及独审结论并获明确触发许可；不能自动再推或把原批准扩成两次并行窗口。缺少平台事件交付证据的限制继续保留。
