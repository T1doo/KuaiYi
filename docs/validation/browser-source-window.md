# 浏览器图片操作的原窗口绑定

基线 `c7b3396e80871b64266dace0b44d1904d17b2583`；正常开发分支 `dev/browser-source-window`，远端 `https://github.com/T1doo/KuaiYi.git`。本次新环境初始仅有 main 早期文档提交，实际 fetch 后确认初始 HEAD 和图片读取清理提交 `64745d23f689d4fc9507507fb580679d6a8de7f4` 均为该远端基线祖先；工作区起始干净，无其他运行 writer。

## 原规格与缺陷

对应 SPEC §13.2、P7-02/P7-03、R13/R16 和 K05/K06 的来源失效子项。旧 Worker 仅监听同窗标签激活，读图后只检查 `active/url`；用户切到另一浏览器窗口时，原标签仍可能 active，旧操作可以继续读图并提交。标签移到另一窗口后也可能沿用原操作。

[Chrome tabs 文档](https://developer.chrome.com/docs/extensions/reference/api/tabs#property-Tab-active)明确 active 仅表示所在窗口内的活动标签；[windows 文档](https://developer.chrome.com/docs/extensions/reference/api/windows#event-onFocusChanged)定义焦点事件以及浏览器失焦的 WINDOW_ID_NONE。生产 `DesktopLocalConfirmation` 会主动展示并聚焦 WPF 确认窗口，因此不能要求浏览器始终 focused 或在 NONE 时取消。设计独审先核此冲突，通过后才实现。

## 实现与约束

在 Ready 后、读图前以及读图后、发送前，查询原 tab 与最后浏览器窗口：固定 tab ID、window ID、active、原 page URL；每次 await 返回后复核原操作与取消状态。查询失败拒绝。无需获取其他标签、网页内容或新权限。切到其他非负浏览器窗口 ID、原标签 detach/attach 或原窗口关闭均永久取消该操作；回到原窗不会复活。NONE 和同窗事件保留桌面确认流程。取消时立即清除已取得的 PNG 缓冲，最终清理也保留。

原同源 HTTPS、无凭据、拒重定向、PNG/20MiB、Ready 先于图片读取、65/30/5 秒预算、分片/摘要、截图像素归属不足时拒绝等规则保持。未改 C#、Windows 250/600ms、来源/密码/完整性门、锁、SDK 启动器、manifest 权限或 workflow。这里的查询与事件组合不能声称原子来源认证；NONE 不证明操作系统前台属于快译，只保留原有逐次桌面确认语义。

## 本次实际结果

Linux / Node v24.19.0。最终同字节新测试在基线副本实际 **1P/15F/0cancel/0skip**，修复后 **16P/0F**。独立只读审查再次执行同一定向测试16P，未发现剩余P1/P2。生产 Worker/nativeConnection/acquireImage 被直接导入，使用真实 ReadableStream 检查取消、reader 锁释放和已拥有块清零；Chrome 事件/窗口数据和新测试 Native Port 为明确合成。覆盖读图前失败、读图后其他窗口、跨窗再返回、移窗/关闭、查询中撤销、Chunk 确认时取消无 Commit，以及正常桌面确认后完整字节交付。

本实例重新下载已授权的同一 Linux SDK402 归档，240279509B、完整 SHA512 和 MIT/notices 均等既有批准原件；通过未改 `scripts/session-sdk.py` 正常初始化项目隔离状态。未执行旧401、伪造 sentinel、改变 HOME/权限、接受新协议或生成证书。五项目 locked restore（audit=true/all/low、warnings as errors）与 Release build 成功，0警告/0错误。

最终完整浏览器回归命令在该 launcher 环境中运行：

```sh
node --test --test-concurrency=1 --test-reporter=tap tests/browser/protocol.test.mjs tests/browser/confirmation.test.mjs tests/browser/acquisition.test.mjs tests/browser/source-window.test.mjs
```

**91P/0F/0cancel/0skip，8.390秒，Exit0**；包含原75项（其中11项真实 stdio/localpipe 宿主集成）和新16项。原有3个测试文件仅补新使用的 Chrome API 端口，旧断言不变。原11项生产 BrowserSession/DesktopPipeChannel/接收状态机与合成 Chrome/peer/decision 的边界保持，不算 Windows 或实际浏览器证据。此次没有重复整个 .NET 套件。PowerShell 不在本机，`test.ps1` 只登记入口，未执行该脚本。

初轮基线夹具等待旧代码没有调用的查询，出现一次2秒测试超时；进程已退出，随后让夹具同时观察原操作完成并结算自身任务。另移除正向用例对内部查询次数的断言，最终同字节红绿另存；初轮及中间16F原件未覆盖。最终新测试、生产源码和已执行回归输入的141文件 SHA256、16宿主输出及全部日志见[证据索引](../test-evidence/2026-10-20-browser-source-window.json)。Node语法和源码空白检查通过。

## 剩余条件

P7/R13源码子项 InProgress，K05/K06整体及106整体验收仍 NotRun，Package Hold。实际 Chrome/Edge 需安装回执、精确 extension ID 注册及同用户同 session 的 Windows 生产接收端；还需实际窗口切换、桌面确认、移窗/关闭和取消交互测试。Windows原生/UIA/OLE/OCR、完整P6/P7、模型质量、签名安装升级卸载仍未验。

本次仅读到 Chromium sandbox owner65534/mode4755，未启动浏览器，也未禁用 sandbox 或修改其权限。旧 CI39347646426 第3步内部首因仍 Unknown；未读取或绕过被拒日志，未触发新CI。无模型调用、新费用、凭据、发布或用户电脑操作。

原始 Node 失败日志含尾部空格，首次 stage 空白检查已如实报告；仅本轮 raw `.tap/.log` 依既有原件惯例设置 `-text -whitespace` 保留原字节与摘要。产品源码和测试的空白检查未放宽。
