# P6-06：单个本地图片文件投放

先完成并独审推送取消清理闭环 `d1b9d459f967847df47f83406d7f5324fc4dfb5e`，再从clean/远端一致的该完整SHA建立 `dev/p6-single-image-drop`。唯一root writer，独立reviewer只读，先最小设计ACK再实现。没有恢复旧实例或删除未知锁/进程。

## 实际接入与支持子集

正式Shell主菜单及更多菜单新增可勾选“接收单张图片文件投放”，**默认关闭、仅本次运行**。只在已On、Drop模式、可见且无暂停时接纳。改选项先撤销原receiver，再由原reconcile等待实际进程/注册/交付清理后重建，hello里的AllowImageFiles冻结；未开选项、Off、未知callback均不能接收文件，也不会自动启用。

本切片只接受CF_HDROP中**一个wide本地图片文件路径**。进入/悬停仅有限QueryGetData和反馈，无GetData、文件读取、解码、模型请求。虚拟文件descriptor及FileContents在index0/-1的HGLOBAL/ISTREAM/ISTORAGE受检形状明确拒绝；无CF_HDROP时CF_BITMAP、DIB/DIBV5、PNG/image/png的受检介质明确拒绝，不借Unicode/HTML替代内容回落文字。URL、多文件、ANSI、位图及虚拟文件本轮未实现，保留明确拒绝提示，不称全图片投放或完整P6-06/Beta。

Drop后沿原独立receiver的同步生命周期复制或协商async→跨STA正确封送→实际读取/EndOperation/ReleaseStgMedium/原scope清理。新增ReadImageFile只复制至多16KiB的HGLOBAL，实际自有copy在finally清零，不改共享来源内存。pure parser要求20字节DROPFILES头、pFiles精确20、fWide=1、fNC=0/1、偶数字节、完整双NUL、一个非空path、尾部仅受界零allocation padding；路径最多4096 UTF16 units，严格Unicode、普通盘符根路径、无UNC/device/ADS/URI/control/相对段。只接受既有六种后缀写法作为早拒；实际格式仍由原LocalImageFile/Worker签名和头部校验决定，不能凭后缀当正确图片。DROPFILES.pt完全不作桌面锚点。

字段与列表定义据Microsoft [DROPFILES](https://learn.microsoft.com/en-us/windows/win32/api/shlobj_core/ns-shlobj_core-dropfiles)及[Shell Clipboard Formats](https://learn.microsoft.com/en-us/windows/win32/shell/clipboard)复核。上述精确header/旗标/上限及格式拒绝是产品严格子集，不承诺接受所有合法Shell对象。

## 原资格到独立文件owner的交接

现有wire字段TextFormat增加显式ImageFile枚举值，名称沿兼容字段但语义明确分流；原OleDroppedText继续拒绝该值。hello新增AllowImageFiles默认false，仅production可用。父端仍核原transaction/source/point/选择format/epoch、CleanupComplete和DropEnd，再以独立typed path callback交付；无callback直接Protocol拒绝，绝不调用文字callback。path UTF8受限12288byte，长度/解码再核；payload仍待**原实际callback Task**结算才清零，取消wait不能提前清数据或释放owner。

Core新增ReceiveImageFileAsync复用同一DropWork、seen transaction、原CaptureTicket、来源密码/受保护/屏蔽/完整性/当前窗口策略、geometry、settings/generation及原1500ms接纳门。最后source检查后再核资格/期限及receiverCurrent，队列晚返与失效清除受控path引用，文件不建立文字snapshot。托管string只清引用，不宣称物理擦除。

UI有限交接只把复制并验证的path交给已有StartOpenImage，**安装同一个自有fileImageTask后即返回，不在OLE的1500ms预算内等待图片解码**。随后完全沿既有FileImageOperation→安全普通本地文件handle/最终目标/drive/reparse/20MiB门→原受界Worker→OwnedFileImage→本地预览→原显式图片发送确认；没有第二条解码或provider请求支路。fileImageTask用原FileImageIntent及App lifetime/自有CTS持有。它暂停捕获而正常撤销原被动receiver，不能连带取消已转移文件；取消图片接收选项只取消ReferenceEquals的当前droppedTask，不取消独立“打开图片”任务。Off、隐藏、锁屏、清除、停止和退出沿既有资格/原Task结算门。

投放成功只本地准备、自动展开现有预览；GIF仍实际可见“使用首帧”。用户使用原图片翻译按钮/菜单，再看目标origin、模型、可能两次调用的确认，未经确认不发送。文件保持独立无桌面坐标owner，路径不进provider内容。图片准备或清理未确认时，新投放被拒，旧来源保留；多文件等Drop阶段拒绝有固定原因提示。

## 真实验证

[证据索引](../test-evidence/2026-10-20-single-image-drop.json)及原件ZIP保存完整命令/日志/TRX、失败因果源码、最终源码与产物摘要。SDK10.0.402/runtime10.0.13，Receiver/App两次locked restore、NuGet audit all/low成功；无新依赖/版本/锁/许可。旧解码/文件owner/发送链和清理诊断源字节保持。

| writer执行 | 结果与范围 |
| --- | --- |
| r14 Core相关限定 | **261P/0F/0skip**，原相关200+新61，非全Core |
| r15 完整Provider | **2393P/0F/0skip**，原2381+新12 |
| r16 完整Codec | **475P/0F/0skip**，本轮无新格式/codec用例 |
| r19 Receiver / r20 App最终Release | 各 **0W/0E**，Linux上的Windows交叉构建 |

新Core61包含parser/队列资格52及实际owned子进程/真实production私有wire+server9：单路径、禁开关、悬停零交付、multi/URL/missing-cleanup拒绝、null typed callback拒绝、typed callback取消后原buffer仍持有且实际结算再清零。子进程backend/注册器/来源窗口均为明确合成端口，不是Windows原生OLE验证。

新Provider12直接链接实际managed命令/反馈body：默认关闭与On/Drop/暂停拒绝、optout/Off/原receiver撤销三种寿命、optout不取消独立open、QueryEnd/DropEnd可见原因及旧owner保留，两协议Core路径admission→原file任务→预览/首帧→原confirm→实际loopback分类/生成及复用拒绝。其loader返回合成准备owner，真实Linux Worker/fileowner仍由原Codec475中的用例独立验证。不是Windows真实filehandle/WPF/checkbox/COM/模型实跑。

r01/r02因typed callback未传到private RunOwned编译失败，0执行；保留原件。r04为21P8F：旧clear专用port默认拒绝nonclear revoke，新测试加明确port模式，默认旧严格断言保留；r05为2P6F：测试在Dispose后查询同Process.HasExited而报错，改在finalize前核原owner退出，不放宽产品退出门。随后r06 29P、r07 8P；反馈P2修后r08 32P、r09 9P，最终以表中回归为准，不累计。独审发现DropEnd收起hover的else-if使后续拒绝提示不可达，已提取共享实际反馈body、分开两种动作，并以旧owner/Off/可见提示回归闭合。

最后只收窄Windows格式query的受检形状（FileContents/PNG类增加ISTREAM/ISTORAGE拒绝，FileContents再按官方零基条目增加index0并保留-1兼容），便携测试不执行这个Windows adapter；修正后两项最终交叉构建通过。静态审查和交叉构建不能证明原生COM、源窗口身份或真实桌面交互。

## 保留边界

真实Windows native/OLE同步与async、注册/原来源/密码及完整性、最终文件handle、dialog/checkbox/WPF/bitmap清理、画质/本地字号、250/600ms、600MiB与完整P6仍NotRun。bitmap/虚拟文件、多图/URL本体未交付，文件OCR后续复用既有file owner。旧BMP取消历史根因仍未确认，不把上一闭环诊断当根因已修。

WebP odd payload明确拒绝、所有原格式子集、PackageHold、生产图片复用Off、来源/密码/完整性/取消清理/实际退出/逐HTTP发送门保持。仅合成数据/中性内容；无真实用户图片或模型、新依赖许可/安全设置/费用/凭据、CI调度、用户电脑、发布部署、main修改或强推。
