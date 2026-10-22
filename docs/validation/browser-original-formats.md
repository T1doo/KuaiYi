# 浏览器受支持原图格式接入

SPEC7.1/13.2、P7-02/P7-03 的原产品缺口：扩展取得原图、NativeHost Commit、桌面 Commit、Browser owner 与 Worker 准备入口仍限 PNG，已有文件解码器不能用于普通网页 JPEG 等输入。本轮在 `dev/p7-fixed-signoff`、基线 `b82c377fba33f72b307cb4cc18598425cf85bc89` 的指定保存云环境由唯一 writer 实施；独立只读设计与源码审查限定 ACK。

扩展在原逐次桌面确认后取得同源 HTTPS 原始字节，新增 JPEG、BMP、受支持的静态 WebP、GIF，与 PNG 共用原路径。HTTP MIME 白名单必须与字节签名一致；JS 不调用图片解码器、canvas、DOM 测量或页面截图。NativeHost 和桌面分别在原 SHA/完整传输 Commit 后，以统一有界策略检查实际格式、MIME 与原始画布；EXIF6 仍以 raw3×2 交接，Worker 才产生 normalized2×3。不是把网页尺寸或响应头当作解码证明。

BrowserImageOperation 在原 Load Task 内 await 后台 Inspect+Hash，借用收据仍归原 caller 持有；active 覆盖实际任务，取消在 factory 前复核。复用原隔离 Worker、job/SHA、原 IO/EOF/实际退出/投影结算。owner 的 Browser 格式是显式五种枚举；保留 Browser 来源、无文件路径、无桌面 anchor/截图 proof。结果仍为规范化 PNG，不把原 GIF/JPEG 直接当预览像素。GIF 沿可信 Worker metadata 显示“使用首帧”；偏移、透明画布与白底路径不改。接收确认和状态文本不再承诺“仅 PNG”。

20MiB/24MP/16384 输入、2560长边/4MiB输出、既有 PNG/eXIf、JPEG baseline/八方向、24bit40字节头 BMP、偶数 payload VP8/VP8L 最小 VP8X、GIF 容器/子块/资源上限均不放宽。WebP 合法奇数 payload 仍因固定 native padding 风险明确拒绝。Contracts assembler 原就支持五 MIME，本轮字段、protocol1、256KiB/190KiB 分块、绝对时限、安装/peer/来源策略不变。旧扩展 PNG→新 host 保持；新增 MIME→旧 host/desktop 会明确拒绝，不重编码、重放或自动 cold fallback。该混版结论为源码合同判断，未额外运行旧二进制。

Off 主浮窗仅准备本地预览，不自动启用、OCR 或发送。内部 transfer gate 的局部 On 仅表示本次接收授权；主 gate 的 Off 与发送同意独立。PackageHold、生产复用 Off、250/600ms、来源/密码/完整性、取消、清理与隔离门保持。无新依赖/许可/权限/身份/凭据/费用，未运行真实模型、未使用用户内容或用户电脑，未触发新 Windows CI。

实际验证与完整命令/TRX/日志、最终源码及 DLL 摘要见[证据索引](../test-evidence/2026-10-22-browser-original-formats.json)和 Library 原件包。不同轮次不相加：

- 最终 Node 浏览器回归104P/0F/0cancel/0skip。包含实际扩展发送器→独立 stdio host loop→本地 pipe 的六种样本（PNG分块、JPEG、EXIF6、BMP、WebP、GIF），及原确认/来源变化/取消/清零。Chrome Port/事件是合成端口，非真实 Chrome/Edge。
- 最终完整 Codec488P/0F/0skip；其中 Browser 定向10项有真实 Linux native Worker/完整退出证据，原 SHA、Browser kind、EXIF方向、GIF完整canvas/偏移透明/首帧notice、奇数WebP factory前拒绝、借用缓冲与取消结算。
- 最终 Core 接收定向141P/0F/0skip，实际 portable pipe、stdio、原接收器/Inbox；peer与确认明确合成，新增跨MIME/伪造raw宽度和两个接收端奇数WebP拒绝。未重跑完整Core。
- Provider浏览器准备定向10P/0F/0skip，源码链接的主浮窗命令与标签端口，新增GIF可见首帧/未翻译/Off断言；原两协议loopback确认与发送门回归。不是实际WPF或完整Provider。命令中另一个FileImagePreparationTests过滤名不存在，没有借记任何文件类用例。
- 正常App Release交叉构建0W/0E。

保留全部中间失败：最初同源四格式JS0P/4F与Browser Codec0P/4F确证旧PNG门；初次Core139项为134P/5F，全部是新增theory误把内部传输gate期望为Off，改正测试以断言局部On且被动采集/发送拒绝，生产gate未改。初次Core构建中途新增两个负例，故不作最终树签收。Provider r1/r2均CS0103、0测试（源码链接缺helper、再缺其DecodePolicy依赖）；最终不用扩大decoder链接，owner只显式列五格式，测试项目与基线同字节。旧“拒JPEG”测试合同明确替换为不支持SVG factory0/单用，另增真实JPEG正例；Begin unsupported MIME负例改为SVG，同时增加四个已支持MIME伪标PNG的Commit拒绝，未以放宽断言掩盖拒绝。

Windows普通权限路径调查：仓库11个既有工作流均windows-2022；既有真实preflight是High/Elevated且无linked token。标准托管Windows runner使用管理员账户且关闭UAC，见[GitHub官方说明](https://docs.github.com/en/actions/reference/runners/github-hosted-runners)。本次只读 `repos/T1doo/KuaiYi/actions/runners` 返回403 Resource not accessible by integration，状态为Denied；无法据此确认是否存在其他注册runner，未重试或申请权限。没有已建立且符合边界的medium桌面入口，故推进本产品缺口；未造/借token、改UAC/ACL、建账户或操作个人电脑。

真实Chrome/Edge扩展权限/原图→已安装Windows主浮窗、medium正向warm/Explorer、真实身份/来源/OLE/COM/OCR、混合DPI、250/600ms实机及画质仍未验；跨域、认证、blob、可信可见部分裁切仍拒绝/需用户主动区域截图。旧bit4客户端实授红件与两次额外server创建Win32 5的限定结论保持，不因本片转绿。P6/P7、K04/K05/K06和完整v1.0不标Pass。
