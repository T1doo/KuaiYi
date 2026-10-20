# 文件图片的显式 OCR-only 接入

从父侧限定静态ACK的完整SHA `31ee5704fe1e210556a4c2d70e80b811d3a75634` 接续，正常分支 `dev/p6-file-image-ocr`。指定保存环境只有root写入、一个worktree；原reviewer先独立只读设计ACK，再复审五个产品文件。未恢复旧写入实例，不删除未知锁或进程。

## 同一来源、任务与发送链

正式“仅提取图片文字并翻译”菜单现接受原闭合 `ImageSourceOwner` 中的截图和文件图片。文件只复用既有安全打开/单文件投放得到的 `OwnedFileImage`，没有截图owner强转、桌面anchor、截图proof或新的解码/HTTP支路。默认Off、原来源身份/密码/完整性、profile/settings/session/version/request/epoch门和PackageHold不改；OCR要求On、可见且无暂停。

原 `OcrOnlyCoordinator` 继续先调用真实Worker查询本机包身份、已安装语言和尺寸上限，用户选择已有语言后才识别。OCR检查的是**准备PNG的实际归一化宽高**，不是原文件或EXIF旋转前尺寸。超过 `min(nativeMax,2560)` 在创建识别Worker前拒绝；不额外缩图或反复编码来绕过OCR限制。原截图超过2560仍拒绝，旧直接Start接口也在创建Worker前拒绝该形状。文件的已有归一化PNG只复制一次作为本任务输入，4MiB限额不变。

准备请求严格绑定原SourceId/Kind/Notice、实际PNG宽高、Transform.NormalizedSize；file必须精确等于原Metadata.Transform且无desktop/proof，screenshot必须native尺寸、identity方向、原有白底proof与anchor。映射只在原job、语言、尺寸、文本和所有词界均通过原ValidateOutput后创建，最多50000个immutable数值 `OcrWordRegion(LineIndex,WordIndex,Normalized,Original)`；不复制词文字，不生成置信度。原EXIF八方向和实际四舍五入后的两轴缩放仍由ImageTransform表达；文件向桌面坐标转换继续拒绝。

原本地文字预览显示文件来源、GIF“使用首帧”、图形关系未分析警告、目标origin/model和费用提示；必须明确“确认发送这些文字”。Approve只接受当前同一预览对象，后续PrepareOcr/Submit仍要求原reference-bound approved来源及显式confirmed。Provider没有修改请求路径；只发送经确认的OCR文字及原提示词，不发送PNG、路径、SHA、坐标或新metadata。仅增加可信bool provenance供结果与版本标签保留文件/首帧标注；bool本身不能授予发送资格。确认后的文字按旧规则独立保留，原图销毁后仍可沿原门控使用文字。

## 取消、晚返回与原任务

任务持有独立PreparedImageRequest直到**原worker.RunAsync Task实际返回**，再Dispose实清PNG；来源Dispose/替换/CancellationToken/停止等待都不提前清借用数据。原finalize false/throw时继续持有同Worker、阻止新识别，不展示可批准预览；同owner实际清理成功后才能重试。发现与识别结果仍走原IdentityEventReceiver、来源/版本复核和原Task结算，不让晚返恢复旧预览。

独审要求收紧原弹窗晚返回时的全局Clear：新增reference-specific RejectPreview，与原RejectCapabilities相同，只撤销所捕获预览并StopRequest其精确identity。取消、失效和finally均绑定原preview/capability，finally仅清同一个heldDialog；旧modal不能停止后来的识别或已提交文字。原ocrFlowBusy与隐私清理链保留。

## 实际验证与直接证据

[证据索引](../test-evidence/2026-10-20-file-image-ocr.json)及原件ZIP包含实际命令/退出值、日志/TRX、源码/产物摘要与受保护旧文件字节比对。SDK10.0.402/runtime10.0.13，沿原锁文件；无新增依赖/许可/权限。

- 新Provider **34P/0F/0skip**：明确合成准备file owner与OCR端口，原真实coordinator/两协议loopback检查语言选择、无确认零HTTP、预览clone拒绝、已确认纯文字请求、首帧结果标签；native尺寸恰好/越界及2560归一化file对大原图、八方向非等轴实际数值、50000词上限/50001拒绝、错误job/dims/language/NaN/精确残差越界/空白输出、原借用PNG跨清除/Dispose/换源/Off、finalize false/throw同owner重试、队列晚返和旧modal不能停新任务。实际Linux Worker另测文件识别UnsupportedPlatform，不伪称Windows成功。
- 新Codec **3P/0F/0skip**：实际Linux Skia/private worker把合成PNG、JPEG EXIF6、双帧偏移GIF解码并准备成真实file owner，再接实际OCR coordinator和**明确合成OCR端口**；核实际归一化尺寸、原图映射、无desktop、首帧、原任务结束后PNG清零。不把合成识别当真实OCR。
- 最终完整Provider **2427P**（原2393+新34），完整Codec **478P**（原475+新3），均0F/0skip；Core相关限定 **114P/0F/0skip**，非全Core。Worker/App最终Release各0W/0E，Linux Windows目标交叉构建；两次locked restore + NuGet audit all/low成功。
- 首次App编译0W/2E（同scope notice变量重名），改名后通过；首次新Provider编译ApiProtocol歧义、0执行，加明确类型alias后169相关用例（旧138+当时新31）通过。失败日志保留，无测试断言放宽/删除、无sleep凑绿。首次新Codec3P，最终结果不与定向运行累计。

## 保留边界

Windows OLE/COM、WPF菜单/弹窗/标签实机、Windows OCR/真实安装语言与包身份、真实来源/密码/完整性、最终文件handle、字体/图像质量、250/600ms、600MiB和完整P6仍NotRun。没有扩展位图/虚拟文件/多图或资源管理器/浏览器原生菜单入口；不宣称完整P6或Beta签收。旧BMP取消历史根因仍未知，上一诊断和本轮回归不能反推该根因已修复。

WebP奇数payload仍明确拒绝，既有格式严格子集、生产图片复用Off、所有原采集/取消/实际退出/清理/逐HTTP发送门保持。仅合成中性内容/本地loopback；无用户电脑或真实付费模型、费用/凭据/新依赖许可/安全网络改变、CI/发布部署/main修改/强推。
