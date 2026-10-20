# 浏览器原图读取取消与缓冲清理

基线 `ade0b4a8e0c3db15f096a54205246a330706799b`；正常开发分支 `dev/browser-image-read-cleanup`，远端 `https://github.com/T1doo/KuaiYi.git`。本实例从远端取回基线，确认初始 `work` HEAD 是其祖先、工作区干净后创建分支；未修改 main，未恢复其他实例。

对照 SPEC §12.4、§13.2、P7-02/P7-03，真实原实现有四处缺口：HTTP/MIME/声明大小拒绝在 reader 清理范围外；超限末块未进入清零集合；取消未独立终止挂起的 body read，也未在读取/返回边界复核；reader 锁未释放。正常 PNG 路径原本可读，不将它写成未实现的新功能。

生产改动仅在原 `original` 读取函数：已有取消先于 fetch 拒绝；拿到 reader 后所有响应头校验进入同一清理路径；已交付的块先接管再检查取消/大小；abort 清已拥有缓冲并请求一次原 reader.cancel，保留该任务结果；原 read 和 cancel 结算后才释放锁。读取与清理异常同时保留，失败或迟到取消清拼接结果，成功转移独立 PNG 字节。没有新权限、重试、重定向、凭据、截图抓取或桌面下载路径。

依据 [WHATWG Streams Standard](https://streams.spec.whatwg.org/#readable-stream-cancel) 核对原生取消关闭 read 与底层取消 promise 的区别；用真实 ReadableStream 实测，而非以 token 当作 I/O 已结束证明。标准只支撑 API 语义，产品字节所有权及门控由测试验证。

实际验证（Linux，Node v24.19.0）：最终同字节新测试对基线 12 项为 1 Pass / 11 Fail / 0 Cancelled；修复后独立 12/12 Pass，现有可运行 Node 回归连同新增项 64/64 Pass。包括四类响应头拒绝、预先取消零 fetch、超限块清零、已交付块与取消竞争、挂起 read 取消及原 cancel 等待、双异常、成功锁释放/独立缓冲、一次真实 loopback HTTP PNG 请求。生产 Service Worker 导入后用明确受控 Chrome/Native Port 事件触发导航，实际 stream cancel 一次、锁释放，仅 OfferImage/CancelImage，无 Begin/Chunk/Commit。Chrome 事件与 Native Port 是测试端口，不声称真实浏览器端到端。

最终回归命令：

```sh
node --test --test-concurrency=1 --test-skip-pattern='actual extension sender to independent host loop commits ordered maximum chunks|actual extension/host/production receiver waits|actual ServiceWorker/native host/production receiver confirmation boundary' tests/browser/protocol.test.mjs tests/browser/confirmation.test.mjs tests/browser/acquisition.test.mjs
```

11 个需已构建 .NET 宿主的既有测试未包含于最终64项。首轮负向 test-name-pattern 匹配文件祖先，未过滤这些用例，实际 64P/11F，失败全为缺少 dotnet 的 ENOENT；未执行旧401，也不以最终纯Node绿覆盖它。一次基线筛选同样失效，导航反例超时后仍待原流，已仅终止本轮两个Node子进程1278/1284；最终夹具增加自身受控流关闭与原任务join，同最终字节基线12项在有限时间真实结算。初版迟到块测试曾因stream自动预取在读取前取消而断言错误，修为highWaterMark=0，在真实read交付边界触发；中间10P1F原件保留。全部日志在[证据目录](../test-evidence/2026-10-20-browser-image-read-cleanup/)，索引含SHA256。

`test.ps1 -Suite Offline` 已登记新增文件；本机无 PowerShell，未运行该脚本；75项首次尝试64P11F，11项宿主执行未完成，未取得完整75项通过。node语法检查、git diff --check通过；生产C#、锁、SDK、九workflow、SPEC及原验收证据保持不变。本轮只修源码子项，P7-02/P7-03源码InProgress、整体validation NotRun；R13/完整P7不签收。

独立只读审查核代码/新测试/脚本接线，未发现剩余P1/P2，最终另跑12项。Windows CI 39347646426 第3步内部首因仍未知，Actions Forbidden与credential_observation_restricted未重试或换路；未触发新CI。真实Chrome/Edge权限/安装NativeHost、Windows交互、PNG之外解码（含Skia原许可/403门）、模型质量/费用、真实WPF/原生及完整P6/P7、签名安装/升级卸载门均保持未验/Hold。
