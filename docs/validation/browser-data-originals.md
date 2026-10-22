# 浏览器受限data内嵌原图

基线 `1de5fea42d9d16a165d9373417b24bdd54f3c060`，正常开发分支 `dev/p7-fixed-signoff`，原指定保存Linux环境、唯一writer。SPEC13.2原始字节路径的实际缺口：imageContext原接受data:，original只允许同源HTTPS，因此用户右键data图片必拒。本片补齐已授权data原图，不开放blob/跨域/页面裁切。

仅接受精确五MIME `image/png`、`image/jpeg`、`image/bmp`、`image/webp`、`image/gif` 各自的 `data:<MIME>;base64,`，大小写、额外参数、百分号编码、空白、fragment、URL-safe字母、缺失/多余/中间padding及非零padbits都拒绝。完整三字节组不需要`=`，末组一/二字节须规范`==`/`=`。先按原字符串类型与总长度限界，再识别有限header；literal data不交给URL解析器，避免自动规范化。编码上限4×ceil(20MiB/3)，总长度再加最长23字符prefix；预计decoded大小扣padding后须1—20MiB，同编码长度但无padding预计20MiB+1的情况也在分配前拒绝。

Ready之后才扫描和解码。以64KiB字符为块，两遍处理：完整严格字符/padding验证通过后才分配唯一输出Uint8Array，再逐quartet写入。不调用fetch、atob、DOM、canvas、浏览器解码或截图，没有整串decoded中间副本；64KiB是4的倍数，块边界不切quartet。每次setTimeout0让出前后核取消，分配后、MIME核对后及返回前复核。原Task持有输出，abort即时清零，任何失败finally清零；错误文本固定，不含data URL或解码细节。浏览器原srcUrl/JS字符串不承诺物理擦除，只清可控字节，不写日志、协议或磁盘。

原ServiceWorker在Ready前不调用original，前后sourceCurrent仍核精确tab/window/page与operation，后续原SHA/分片、双端Commit MIME/原画布、隔离Worker与Browser owner、主Off/逐次确认/发送门不变。manifest、协议、全部C#及原png/JPEG/WebPodd/GIF子集验证均未改。内嵌GIF仍由原Worker规则首帧+可见notice，不直接把浏览器data类型视为合法栅格。

## 实际验证

最终完整Node回归 **154P/0F/0取消/0跳过**，无filter/exit0。842个冻结输入前后不漂移，760个保护输入及实际接收器5个DLL摘要独审核对一致。完整命令/日志、准确红绿源码副本、原始失败和最小对照见[证据索引](../test-evidence/2026-10-22-data-original.json)和Library原件包；不同轮次不相加。

新增直接字节测试覆盖五MIME六fixture（含EXIF6 JPEG）、19畸形、4种伪MIME、两种分配/调度前超限、literal原串不经URL/no fetch、预先取消，以及validation/decode两阶段确定性原task held、即时清零、释放屏障后结算。字节stress用中性合成buffer，仅用于取消验证，不冒称它的PNG尾随字节是Worker合法容器。

新增真实合成链使用原ServiceWorker→实际stdio BrowserSession→原DesktopPipeChannel/Receiver/Inbox与真实Linux本地pipe；Chrome Port/peer/desktop decision明确合成。九正例含六基础样本和跨64KiB的三种GIF89a comment样本（100/101/102个255字节中性subblock，原长模3为1/2/0）。Ready前零输出分配，精确原字节/MIME/SHA、一次确认、收据及返回数组清零。七负例覆盖拒绝、读前/读后页面替换、畸形、伪MIME、预计超限和取消；没有Begin/Chunk/Commit图片发送，控制Offer/Cancel不冒称零控制消息。

保留失败：最初五有效格式0P/5F复现原HTTPS门；target r1为47P；完整r1为151P/1F，唯一新跨块夹具向原GIF87a加入comment。最小真实接收对照显示71269字节/95028 base64的解码逐字节相等：87a在Commit被原验证拒绝/exit3/0接收，改89a仅声明合法扩展后Committed/exit0/71269字节且清理true；生产GifDecodePolicy确有old87拒扩展分支。只修新fixture header，未改生产验证、超时或断言。之后三边界target3P，最终完整r2才是154P签收。

没有重跑或借记Core/Provider/Codec；其C#字节与已签收b554产品相同。本次链证明字节获取/发送/双端容器检查与收据清理，不是实际Worker解码、真实WPF、浏览器权限或安装验收。独立设计/源码/原件限定ACK，无新增确定P1/P2。

真实Chrome/Edge/Windows、medium正向warm/Explorer、真实来源/密码/完整性、250/600ms与画质仍未验；跨域/blob/可信可见裁切不扩展，PackageHold、生产复用Off及旧安全红件边界保持。无新依赖/许可/费用/凭据/权限/CI/模型调用，未重试runner403、不操作用户电脑、不改main/强推/部署。
