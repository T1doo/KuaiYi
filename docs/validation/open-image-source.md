# P6-06：生产打开图片与可信文件来源

2026-10-20，接续已推送且父侧静态ACK的完整父SHA `0b6d1c835f7f9baed79d7320ac485331fc2bbf88`，先复核本地/远端、单worktree和唯一writer，再创建正常开发分支 `dev/p6-open-image-source`。旧实例未恢复、未知锁文件未删除。最小设计经独立只读review后实现；本轮不继续增加格式变体。

## 交付的实际路径

正式 `ShellWindow` 主菜单及溢出菜单提供“打开图片”，选择单个PNG/JPEG/BMP/WebP/GIF后，真实 `FileImageOperation` 在后台调用原 `LocalImageFile` 安全打开/最终handle目标校验和原受界Worker。仅扩展该读取器的五种已有签名分派；普通本地文件、最终路径、本地drive、重解析/UNC/device/ADS、20MiB和原格式子集限制保持。不是把P0实验入口改名为生产功能。

新增封闭的 `ImageSourceOwner` 合同，保留现有 `OwnedSourceImage` 截图实现，新增独立 `OwnedFileImage`。文件owner只持有已准备的opaque BGRA和单次4MiB PNG，不保留路径、文件handle或桌面crop。原始长度/SHA、格式、EXIF原/oriented/normalized变换和纯数字GIF首帧信息由原Worker协议绑定；native不增加解码次数。`PrepareFileAsync` 仅在原完整EOF、实际exit0及原IO结算确认后投影，默认长边2560、原白底/area内核和单次lossless PNG。投影保留总15s协作预算、等待原Task，晚返/异常/取消清零实际自有buffer。该预算不是250/600ms性能证明，也不是强制线程终止。

文件的请求始终无桌面锚点，`NormalizationProof` 为null，不能取得截图物理坐标；未借截图owner或 `ScreenshotIntent` 提交文件。文件预览的局部像素容器以0,0作本地bitmap原点，不进入请求Transform。截图继续持有其原真实crop、proof和坐标映射。公共生产协调器校验SourceId、Kind、Notice、Transform及“截图有anchor/proof、文件无anchor/proof”，以Transform是否存在判断准备完成，避免把文件null proof误当未准备而重复通知。

打开只本地准备并选中，不启用、不分类、不发送；旧来源保留到新文件完全准备且资格仍当前。成功后实际共用图片来源区域自动展开，标题/结果label/版本说明/生成与失败状态区分“文件图片”；可信GIF固定可见“使用首帧”，任意文件名或comment不生成提示。`CreatePreview`最长1024、实际显示沿原1024有界bitmap/原清理重试路径。严格API cap只复制已保存PNG或拒绝，不缩图、重编码或质量重试。

Off下“启用并翻译图片”先明确用户动作，再调用原 `Toggle`/lifecycle/profile/来源确认，随后重查同一owner、version及On状态，再进入原生产图片确认。该确认显示目标origin、模型、模式、可能两次调用及首帧提示；生产协调器仍原profile/settings/binding/budget/secret/consent、逐HTTP发送门、分类后生成和消费来源复用Off。路径不进入provider payload。产品代码没有loopback测试专用放行；测试通过原受控HTTP设置使用合成服务。

## 所有权与取消

文件使用Core通用独占采集lease及自身CTS、原Task和具体 `IFileImageOperation` owner。admission同时核input/result/epoch、共用来源owner及具体file/screenshot CTS。所有文件打开读取/预处理从Dispatcher转到后台；原task在回调可重入前安装，所有finally继续等同一真实操作。`TryFinalizeAsync` 未确认或抛错时保留同一owner，阻止新文件及截图，清除内容可显式重试同一owner；既不提前释放，也不删除未知锁/PID。

清除、隐藏、锁屏、Off、停止及新文字来源主动取消该次读取；设置失效立即拒绝交付，并在状态刷新时取消CTS。停止即使尚未进入generation也先取消文件；清除/退出收集并等待同一任务/子进程结算。旧任务晚返只清零释放，不能恢复source、预览或发送。原markdown bitmap清理失败重试机制保持。文件OCR与图片投放本轮尚未接入，OCR入口明确只接受原截图owner，后续应复用此文件来源而非强制转换。

## 实测与审查

[证据索引](../test-evidence/2026-10-20-open-image-source.json)及其原件ZIP保留命令、完整TRX、失败原件、最终源码/产物摘要。SDK10.0.402/runtime10.0.13，沿原锁文件五次restore，NuGet audit all/low，均exit0；四个既有固定SkiaSharp4.153.1包/全部锁与许可证不变。

| 最终writer执行 | 结果 |
| --- | --- |
| 完整Codec | 470P/0F/0skip，原453+新增文件owner17 |
| 完整Provider | 2381P/0F/0skip，原2361+新增文件来源20 |
| Core相关限定filter | 165P/0F/0skip，非全Core |
| Worker/App Release交叉构建 | 各0W/0E，非Windows实际运行 |

Codec真实Linux子进程覆盖PNG/JPEG/GIF到文件owner、原SHA/长度、EXIF6的3×2到2×3、无桌面锚点、预览/请求独立寿命、严格cap不重编码、实际buffer清零、投影原Task取消仍持有、错metadata/extra-byte/exit7禁止投影。旧453项原测试源字节不改。

Provider新增20项直接链接生产managed命令体和来源/协调器：Off只本地展示、七种失效晚返、原owner结算失败/抛错后显式重试、Off确认四种结果、两协议实际loopback分类与生成、来源/首帧标签/不重复Prepared/同一PNG及消费复用拒绝、原确认/settings/Off/清除发送负例。其file loader提供已准备的合成owner，原生绑定由独立Codec17项验证；不冒称Windows文件dialog、Win32安全handle或真正WPF事件已运行。测试启用端口也不等于真实Toggle交互。

原件保留三类失败：r03旧截图admission用例1F揭露重构遗漏具体截图CTS门，已修复，原断言不动；r06新Anthropic测试错误假设image块位置，改按schema type寻找，保持原payload断言；r13完整Codec469P/1F，旧BMP取消用例在Dispose返回SettlementUnconfirmed。未降低退出/清理门或修改该旧测试；同构建定向2P、同源码完整复核470P。当前仅确认这一次结算未确认与随后复核通过，未证实其根因或宣称已修复旧退出竞争。各轮不累计。

独审要求的P2“停止在尚未generation时不取消文件”已闭合，并有managed命令回归；finally结算异常保留原owner、过期操作不改UI，具体CTS门及原case断言均复审。最终只读review的范围/回执见索引；reviewer不另跑测试，不充当第二writer。

## 仍未验边界

Windows真实文件/最终handle校验、dialog/WPF/实际Toggle确认、bitmap生命周期、native Worker/OCR、真实字体字号与画质、250/600ms与600MiB仍NotRun；完整P6-06/P6和图像Beta未验收。投放和文件OCR后续复用此路径，当前未交付。GIF/WebP等严格子集不变，合法WebP odd payload仍明确拒绝；PackageHold、生产图片复用Off和全部来源/密码/完整性/取消清理/实际退出/发送门保持。

本轮仅合成数据/中性内容、已批准依赖，无真实模型、私人图片、新费用/凭据/安全网络权限/依赖许可、CI调度、用户电脑、发布部署、main修改或强推；旧被拒日志未请求或绕路。
