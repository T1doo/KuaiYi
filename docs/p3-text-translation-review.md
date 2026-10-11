# P3-06 真实文字快照到流式浮窗的限定接线

基线 `561ee5779ac546ee8a0ddad6526e7e1494c545d3` 已获监督限定放行。本轮完成 P3-06 的生产调度接线及本地测试服务验证；不是完整 P3/P4、Windows 文字 Alpha 或 v1.0 验收。P0/E06 仍 Blocked，P1/P2/P3 InProgress，106 整体验收仍 NotRun。

正式 Shell 的 UIA/复制兼容/实际 OLE Drop 成功回调生成不可变来源快照，冻结捕获 ticket、配置、profile binding、原文、四种版本化提示词及请求身份，退出捕获提交栈后在 UI 线程发起调度。复用原 OpenAI/Anthropic HTTP、SSE/JSON、超时/取消/重试与目的队列门控；每次实际发送在 Gate 锁中检查来源和配置，再检查身份资格，避免检查回调重入后仍发送。HTTP 与密钥读取都是实际实现，没有硬编码生产成功或译文。

默认仍 Off。启用需要有效配置和本次会话端点说明；请求资格成立后才可读取该请求的确切加密秘密绑定，读取前后及 HTTP 前都复核，过期租约清零。测试只打开临时 fixture 的 AES 密文，不运行 Windows DPAPI、不读取任何真实密钥。新来源 latest-wins，取消/Off/锁屏/删配置/换密钥排除旧请求及 UI 事件。超过 12000（可配置）Unicode 标量需明确长度/目的地址确认；50000 为硬上限，不截断、不分拆。确认打开后资格变化拒绝发送。

重复同源、同文、同风格与语义配置不增加调用；配置反序列化产生新的字典对象也按实际字段去重。普通重新生成复用所示版本的原文、profile 与模板；“按当前设置重新翻译”另有入口，两者不重新捕获。当前来源最多显示三版本，生成槽计入上限。失败版本不标完成；新来源失败回滚至先前可读结果。连续替换尚未完成的来源仍保留最后可读缓存；来源快照按结果与回滚引用淘汰，生成期间至多四个（最多三个显示版本加一个回滚保护），完成后至多三个。锁屏/退出清理全部会话内容，无默认内容文件。

生产 `KuaiYi.App` 直接引用 `KuaiYi.Providers`，不引用 `KuaiYi.MockProvider`。只有显式 `--translation-test-provider` 才允许指定格式的回环 fixture 地址，创建自己的随机临时设置目录和无密钥测试 profile，关闭后移除；界面持续显示“本地测试输出”。默认启动不选 Mock、不创建测试服务、不改用户正式配置。公开 GrowthHarness 仍关闭真实取词/热键/Provider。

本轮 66 个新用例：54 个生产调度/真实 loopback HTTP（双协议 SSE/JSON、实际采集协调器和 Drop 字节快照、六段流式驱动纯结果/Frames、队列晚到、latest-wins、限额、重复/回看、版本/失败/连续替换、旧/新配置重生成、测试入口隔离）以及 12 个实际临时文件加密租约/发送门控用例。完整回归 Core1216 + Provider131 = 1347/1347，Node63/63；27 工程 locked Release 零警告/错误。原 Core1204/Provider77 的逐项名称保留。27 个锁文件中只有 App 与 GrowthHarness 增加 Providers 项目引用，NuGet 版本与包集合未变；没有安装依赖或新增项目。

保留首轮编译失败（测试 JSON 重载歧义、xUnit 禁止阻塞等待、MonitorGeometry 构造参数）和实际失败 TRX（44 项中 7 失败：fixture 路径/Anthropic 数组；49 项中 2 失败：测试把 DPI 原始值当缩放因子）。修正测试输入/读取方式，未删除用例或降低最终断言。复查还补正发送检查后重入资格、连续来源替换快照上限与回滚、合法 2048 字符阅读背景，以及反序列化后的语义去重。最终回归后运行源码与测试未改。

独立审查构建由 `scripts/stage-p3-text-translation.ps1` 输出 `artifacts/KuaiYi-P3-text-translation-managed-win-x64.zip`，包含实际 App/捕获组件、隔离 GrowthHarness、测试专用服务器及逐文件 manifest。框架依赖，无安装器/原生 DLL；包内测试服务器需要 ASP.NET Core Runtime。19 份旧审查 ZIP 最终字节核验，15 个历史摘要按精确 commit 验证，字节核验不代表监督签收。原剪贴板轮误覆盖后恢复旧 ZIP 的事故继续记录于证据，不声称历史全过程未改。复核结果、摘要与回归证据见 `docs/test-evidence/2026-10-11-text-translation-*`。

Windows UIA/剪贴板/OLE/原生时间队列、焦点、长文本确认、实际跨应用、多屏 DPI、流式动画及性能、DPAPI/ACL、安装卸载与远端 CI 均未执行；真实模型与付费测试没有授权也没有调用。真实协议在 loopback 的通过不证明真实模型质量或日常可用性。真实桌面检查步骤见 [Windows 清单](p3-text-translation-windows-checklist.md)。
