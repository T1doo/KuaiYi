# 智能图片：明确选择使用视觉继续

本片从 `5f8b1b51b87567973c93bc0af7afd526854ac1dc` 在原指定环境单 writer 接续 SPEC 7.4/7.5。此前 Automatic 必须先取得非空 OCR 文字批准；无语言或空结果在分类前结束，纯照片也无法从智能入口继续。本片在本地提示中增加独立“使用视觉继续”按钮，其后仍有原 BYOK 端点、模型及通常两次调用的发送确认。没有自动 fallback，也不把取消语言/文字预览解释为改用视觉。

## 最小设计与实际接线

- `OcrOnlyCoordinator` 只从合规回复生成私有 exact-reference `VisionContinuationCandidate`。发现阶段在完整 diagnostic 校验之外，独立核 Windows、Present、identity.Failure=None、原包名前缀和 ImageWorker role；仅接受零安装语言、LanguageUnavailable 或 NativeUnavailable。身份缺失/不匹配、UnsupportedPlatform、NativeFailure、超时、取消、协议错误及所有异常 catch 均不授予候选。
- 识别阶段先校验 exact job、合法 Failure 和成功/Output 互斥。LanguageUnavailable/NativeUnavailable 只有原 `Start(exact capabilities, chosen language)` 私有 lineage 可授候选；直接 `Start(version,image,language)` 的失败不会授予。空结果完整通过 `OcrOutputPolicy` 的尺寸、语言、结构与范围检查，严格要求 Text 为零长度串且 Lines 为零个。空白、矛盾输出、非法/超限文字继续失败。
- 原 Worker TryFinalize 和 Dispose 均成功、ownedWorker 清除、canonical current identity 接收后才能保存候选；公开及批准另要求 active=null 与原 Running.IsCompletedSuccessfully。已应用但原任务未结算只是 pending，不误清；来源/version/settings/epoch/On/暂停/隐藏等真实失效永久废弃候选。清理返回 false 或抛错不授予候选，仍保留原 owner 供原清理路径结算。
- 原 `OcrImageApproval` 的一次性生命周期抽到封闭 `SmartImageApproval`，仅两个内部可构造的 sealed 子类。OCR 文字批准和无文字的 `VisionImageApproval` 共用同一 slot、Gate→sync 锁序、旧 OCR epoch 到唯一 ticket+1 图像身份绑定、逐次 HTTP 前 current 复核及永久撤销。不能靠 null 批准、Failure 字符串或元数据副本开启 Automatic。
- 正常 `OcrOnlyFlow` 在 smart 模式收到当前候选才显示新按钮/取消，说明未使用 OCR 文字；按钮点击再核 exact candidate 并消费一次。无语言时提示系统语言能力，组件不可用时提示修复本地组件。原 worker、父 smart Task、原 offer Task、取消/替换/Clear/Stop 的结算路径保持；未启动 offer 废弃及根 finally 共用原释放逻辑。
- `ProductionScreenshotCoordinator` Automatic 接受一种有效的本地批准，随后仍由原发送确认授权真实视觉端点。OCR 文字批准的 `text_only + clear` 仍只发送精确批准文字；视觉批准在该分支保留原图并按用户文字风格翻译。mixed/visual/uncertain 同样保留原图解释；unreadable、非法或不完整分类仍保留原停止合同。截图或文件/Browser/Bitmap 的来源及 GIF 首帧标注不改。
- 发送确认与结果标签区分“使用视觉继续”和 OCR 文字批准。snapshot.Ocr 仅记录真实文字批准；视觉只存 reason 枚举，不存文字/图片/路径，也不伪造语言或桌面坐标证明。

设计及首轮源码获独立只读限定 ACK；审查要求的 reply 互斥与 pending 屏障均落实。合成 Worker 端口不代表 Windows OCR 实测。

## 实际验证与范围

本地原件根目录 `artifacts/2026-10-22-smart-vision/`。各轮单独保留 command、日志、完整 TRX、输入及 DLL 摘要、dirty patch 和新增源码；轮次不累加。

- r01 dirty 候选 SmartVision：39P/0F/0skip/exit0，865 输入前后相同。
- r02 dirty 候选正常 App Release 交叉构建：0W/0E/exit0，865 输入前后相同。
- r03 dirty 候选相关实际七类：399P/0F/0skip/exit0，865 输入前后相同，包含新增 Shell 拒绝/Off/清除三例。命令的 BitmapOcrTests、ImageSourceStyleTests 无匹配，不能计作通过；Bitmap OCR 实际属于 FileImageOcrTests partial 类。
- r04 用 Gate 持锁的确定性屏障替换原弱 pending 检查：同一原任务中先 Drain 实际 candidate 接收，核 original 尚未完成/公开 candidate=null，释放 Gate 并 await 原任务后可见且可批准；1P/0F/0skip/exit0，865 输入前后相同。未加生产 hook、sleep 或放宽超时。
- r05 补清理 false、Finalize 抛错、Dispose 抛错的原 owner 保留和零候选定向验证：3P/0F/0skip/exit0，865 输入前后相同。

冻结产品/测试 `e00d8e15dbc1daf7a887bf403939728eb38dbc07` 后，r06 同原十二类筛选实际 501P/0F/0skip/exit0；上轮同组 458 个完整名称多重集全部保留、新增 43。r07 正常 App、r08 Worker Release 交叉构建各 exit0/0W/0E。865 输入在每阶段前后、最终与固定 Git 全部一致，四个目标 AssemblyInfo 均含此完整 SHA；339 个 DLL 当前摘要一致，仅为库存而非执行数。完整 Core、完整 Provider、Codec 不重跑，不借记旧结果。最终原件回执由证据记录。

## 验收边界

默认 Off、图片复用/再生成 Off、PackageHold、WebP odd 拒绝、来源/密码/完整性/原 SHA、资源上限及 250/600ms 合同保持。未做安装器/自启动、visibleCrop、格式或权限扩展，未重探 runner403；无新依赖/SDK/协议许可、凭据、安全权限、费用、真实模型调用、用户电脑操作、main 修改或发布部署。

Linux 合成正例、实际 offline loopback 双协议与交叉构建不能代签真实 Windows 身份/语言/OCR 效果、WPF 模态按钮/焦点、OLE、Chrome/Edge、medium warm、Explorer、多屏、画质、250/600ms 或 600MiB；P6/P7 整体仍 InProgress。

固定轮完整TRX、各阶段命令/日志、865输入与Git、四份完整SHA版本及339项DLL摘要均获独立只读限定ACK；未重跑产品。随后只核三报告与封包，不回填或扩大Windows/完整套件验收。

原件已保存到 Library `libfile_4e21f75a874c8191a1025bf286ddd1f9/v0`，工具确认 succeeded；ZIP 1,751,547B、177唯一成员/manifest176项，SHA256 `eeea23f06790a8add475c63f1d5f4fc899aee9cfeced3c5f527e992e53480110`。上传后按返回值设置 user.library-file-version=0 并复核 ZIP 摘要不变。包内三报告为上传前快照，此后只追加此保存回执，不循环重包。

封包经独立只读 CRC 和全部176项manifest核对通过，三报告准确限定实际范围，最终限定ACK；保存回执只引用实际工具结果，不改变包内原件。
