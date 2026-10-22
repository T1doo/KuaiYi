# 单张 CF_DIB 位图 Drop 最小切片

从 `36d323d89ab380af7b72d67fd13168ad35dac82d` 接续，唯一 root writer，独立 reviewer 只读设计和源码限定 ACK。对应 SPEC §6.1/§6.2/§7.1；未改变 main、生产安装或 Windows 安全设置。

## 产品接线

正式 Shell 的图片投放菜单增加“接收单张位图投放（仅预览）”，默认关闭、只在本次运行生效。Drop 模式、可见、无暂停且来源身份仍有效时接收。Off 使用独立 LocalBitmapDropTicket 和冻结 BitmapPreviewOnly hello，文字及文件仍需原被动 On 门；不会打开主开关或自动 OCR/发送。选项变化先撤原 peer，原退出/IO/注册结算后才建立下一 peer。

仅 CF_DIB/TYMED_HGLOBAL：40 字节 BITMAPINFOHEADER、planes=1、24bit BI_RGB、无调色板。允许正负高度；0/int.MinValue、复杂压缩、未知头、palette/masks、截断、错误 imageSize 均拒绝。每边≤16384、像素≤2400万、HGLOBAL≤1MiB且转换后 BMP≤1MiB；更大图片使用既有文件入口。本轮不增加分片协议、其他位图格式、多图、虚拟文件或链接。CF_DIB 在 CF_BITMAP 拒绝之前选择，避免同一来源同时广告两种格式时误拒可用 DIB。

隔离 OLE receiver 内先读取40字节头、校验实际 GlobalSize 与 checked stride/完整像素范围，才分配输出；每64KiB检查原 current/deadline，复核头未变化。只复制语义像素，行 padding 清零、不导出 allocator 尾部，生成受控14字节 BMP 文件头。source HGLOBAL 不作 GlobalFree；原 STA/async marshal、EndOperation、GlobalUnlock、ReleaseStgMedium 和 origin scope 均保留。任意复制/清理错误都清除独立 BMP，未确认 native cleanup 不发送 payload。

typed ImageBitmap callback 使用独立≤1MiB单 BinaryChunk、CodeUnits=0；旧文字300006字节上限和文件路径协议不变。instance/epoch/transaction/source/point/format、1500ms和 CleanupComplete/DropEnd 门保持；Off frozen hello 拒文字与路径。Coordinator 转移独立 OwnedOleBitmap，取消注册先从 queued 集合移除，再 Unregister，迟到取消只清仍归队列所有的 owner。

callback 只安装原图片任务，不等待自身 receiver Run。图片任务等待同 peer 原 Run、Control、读写/注册与 Client.TryFinalize(true) 的真实退出结算，之后才启动既有 BMP Worker。取消可以结束准备 waiter，但原 task/slot/encoded owner 继续等 peer 清理；只有 Worker finalize 成功才沿原 AcceptFileImage 发布。peer finalizer 不等待图片任务，避免循环。传输失败但清理完成时拒图并可清 encoded owner；未确认清理则保留所有权。

ImageSourceKind.Bitmap 只接受 BMP，没有文件路径、Browser、截图桌面坐标或 normalization proof。SHA记录实际交给 Worker 的受控 BMP 字节（不是带 padding/allocator 尾部的原 HGLOBAL）；Worker 前后摘要与既有 job 合同保持。主浮窗、本地原图、版本、恢复及逐次发送确认显示“投放位图”。本片明确拒 Bitmap OCR，避免 fallthrough 为 Screenshot；图像理解仍走原逐次确认发送门。

## 实际验证与反例

- r01：Core 位图定向36P；r02：Provider6P；r03：Codec4P。r04扩边界后 Core39P、Provider7P、OleReceiver交叉构建0W/E。不同轮不累计。
- 独审发现新片 SettleTextDropPeer 未确认分支覆盖原 transport 异常。修复保留独立 transport、原 Run/Control/cleanup leaf 按引用去重，首个等待超时仅作观察；不会因诊断ledger上限停止原 finalize。
- r05旧helper反例因夹具新字段未初始化触发CS0649，未执行用例，不计红。仅夹具显式null后，r06同一旧helper反例0P/1F：最终异常集合只有 original cleanup，缺失 original transport，完整TRX原文保留。
- r07最终定向：Core41P、Provider8P、Codec5P，各0F/0skip/exit0，850输入前后相同。包括1499/1500严格边界、Off拒文字/文件、实际私有stdio子进程与取消交付保留、真实Linux Worker正负高度BMP、等待peer/Worker实际退出、取消/迟到图片清理及主Off未发送。

r07的850输入运行前后相同；与后续固定产品提交 a4305e8700d7679410bd68cb2c0de5dd6201c0fd 比较，849同字节，唯一差异为随后README说明，不把r07称作850项全等该提交。

固定a4305e的r08完整Core实际2222P/1F/0skip/exit1，850输入前后及Git原字节一致；原失败方法 `OleReceiverTests.CancelSerializesAgainstEnablePublicationAndPreventsLaterReenable`，157.3571ms，TRX只显示Cleanup:73的TryFinalize(true)断言false。脚本立即停止后续全套；没有重跑凑绿，也不能断言该方法正文全部通过。

源码发现共享测试Cleanup未join原LateReadObserver，而生产finalize明确要求原readSettlements全完成。r09受控不可取消Read通过同Cleanup产生0P1F；r10改善反例保全body与cleanup双异常，仍0P1F，完整双栈均在TRX。反例使用fresh client、其余未启动/已结算门，唯一hold原I/O；不是actual child或r08唯一历史原因的证明。

修复仅测试Cleanup在原finalize断言前补等同一个原LateReadObserver，仍用既有4秒边界，不改生产文件、取消/实际退出门或断言。r11原失败方法+新屏障+OleRead/Bitmap相关75P/0F/0skip；新测试finally精确release/join原Read/observer/Cleanup，单错EDI、多错Aggregate保全。产品源码仍与a4305e完全一致。修复后一次完整Core及尚未执行的Provider/Codec/最终交叉构建随后固定新测试树归档，不预借验证。

## 未验边界

这里的 native medium故障源、Shell调度是明确合成夹具；真实子进程/private pipe/Worker确实运行，但不是 Windows COM 或真实 WPF 端到端。实际 Windows DragEnter/GetData/HGLOBAL/同步与async/OLE cleanup、源密码/完整性身份、WPF菜单/预览、Chrome/Edge/Explorer/medium正向warm、画质、250/600ms及600MiB未验。旧BMP取消及其他历史失败不追认唯一根因；P6/P7仍 InProgress。

固定依赖/许可、WebP奇数payload明确拒绝、PackageHold、生产复用Off、原来源/密码/完整性/取消/清理/实际退出及逐次发送门保持。没有真实模型、用户图片、用户电脑、新SDK协议/权限/凭据/费用/CI或被拒runner API重试、main/强推/部署。
