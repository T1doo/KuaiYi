# Bitmap 本地 OCR 接线

从已独审 `6f6f3483e1ffd113e9f3b7d0cfc5610e61d4eac7` 接续，root 唯一 writer；设计、实现及定向原件已获只读独审限定 ACK。对应 SPEC §7.3/§7.5，未扩 CF_DIB 格式、HGLOBAL/BMP 1MiB 上限、OLE/IPC 或 Worker 协议。

## 实际接入

既有“仅提取图片文字并翻译”显式菜单现在接受投放 Bitmap。默认 Off 和位图接收选项不变；位图预览不会调用 OCR。OCR 仍要求 On、有效 profile/current source/version/settings，先发现本机能力，再由用户明确选择已安装语言；使用原尺寸限制，原 Worker 完整结算后才展示预览。确认该预览后才建立 OCR 文字来源，仍须经过原独立发送授权。没有自动 OCR、自动发送、SDK/依赖或权限变化。

Core 的 `OcrTextProvenance.ImageKind` 使用互斥枚举 Screenshot/LocalFile/Browser/Bitmap。FromFile/FromBrowser 仅为兼容派生读取，增加 FromBitmap 派生读取；不再用两个false表示截图。Approve显式四分支映射并冻结sourceVersion/sourceId/previewId，未知值拒绝；Valid约束非空身份、有效枚举，Bitmap/Screenshot不能冒“使用首帧”。Text SourceAllowed实际检查Valid。主结果来源和版本菜单按枚举显示“投放位图 · OCR-only · 图形关系未分析”，不会伪装文件、浏览器或截图。

Capabilities和OCR preview保留既有ImageSourceKind.Bitmap，归一化PNG经原ILocalOcrCapabilitiesWorker接口交给本地Worker。ValidatePrepared仍核sourceId/kind/notice/PNG大小和原OwnedFileImage transform，要求无desktop anchor及normalization proof。词区域使用原NormalizedToOriginal映射；不能调用NormalizedToDesktop作截图证据。没有新增裁剪、重新采集或文件路径。

Approve前原preview引用、source/version/settings、On及原Worker结算门保持。Approve后的文字仍是独立内存来源：可释放图片，然后由用户独立确认发送文字；不新增“图片释放即禁发”的错误依赖。Clear(true)、Off、新OCR替换仍撤销原批准，设置版本检查与原逐次HTTP发送门保持。预览确认前或取消、迟到、清理未确认时均不发送。

## 实际定向验证

- r01 Provider因新增partial测试文件缺ApiProtocol别名出现CS0104；原源码/编译日志保留，未执行用例，不计红绿测试。仅补明确别名，不改产品门。
- r01 Codec实际4P/0F/0skip：PNG/EXIF6/GIF旧例保留，新增Bitmap由真实Linux私有Worker解码后进入真实OCR协调器，OCR端口是明确合成实现，不能称真实OCR识别。
- r02 Provider OCR相关196P/0F/0skip、exit0，851输入前后同字节。包括Bitmap所选语言、精确预览确认与来源枚举、无桌面锚点/原图映射、双协议真实loopback text-only且0自动发送、图片释放后独立文字确认、清除/Off/设置/换源/迟到/取消/held cleanup、未取消的排队旧回调、Browser控制及未知枚举拒绝。实际Linux ImageWorker发现/识别均明确UnsupportedPlatform，未假成功。

产品固定 `02fa638874ca88afa1c5b7146237cc810d2c257c` 后r03一次完整Core为2223P/1F/0skip；单例soft metadata249.999预期Success却返回HardTimeout，原堆栈、TRX和851输入=current=Git保留，后续签收立即停止。夹具只给wire metadata填时间，却同时使用真实600ms进程启动/传输截止；不能从685ms总耗时确定历史超时阶段或唯一根因。

仅测试修正：soft六边界用同一受控时基负责absolute checks及原deadline CTS；新屏障在原Startup未交付时推进到恰600，明确metadata尚未读取，验证HardTimeout、原owner保留和最终实际结算。原250ms/文字/copy/退出断言不放宽；默认真实600ms watchdog/迟到/取消测试不改。r04相关61P/2F，两例既有protocol语义extra/soft又被真实计时器变为HardTimeout（628.1842/601.1822ms，具体阶段未知），没有全套重刷。仅该11-case语义表复用已有受控clock/deadline，arm1/due600/fire0，保留expected/内容/identity断言和默认真实计时测试；r05相关63P/0F/0skip。随后补正文与原任务清理双错保全，取消精确coordinator/原CTS、join同original，单错EDI/多错Aggregate；r06该清理调整相关63P/0F/0skip。独审另发现新startup夹具在Process.Start后原release.Wait(5s)抛错可使child尚未交给owner，改为仅等同release，外guard仍5s失败、finally先release再join原Start；r07该真实子进程屏障1P/0F/0skip。修正后冻结新树完整轮另记，轮次不相加。

产品src不因上述夹具修正而变更；没有扩大600ms预算、加sleep、接受双结果或忽略失败。r02后README说明会改变，运行前后851无漂移不等于851全等后续固定提交；最终完整轮将自行核Git输入。

## 完整签收与第二个夹具屏障

r08固定测试树 `7260f663e0269d4e5900872797b6874f7401e0a0`：完整Core2225P/0F/0skip，旧2224案例均保留，仅增恰600Startup屏障；完整Provider2492P/1F/0skip。当轮851输入前后=固定Git、HEAD固定/dirty0；Provider失败后立即停止Codec/三构建。失败为既有 `BitmapPreparationTests.LateDecodedImageAfterClearIsDisposedAndCannotReplaceTheOldSource` 行39的Assert.True，该行有多个True检查且无阶段快照，不能凭原TRX确定具体检查或唯一路径。

r09只作受控夹具反例：原peer信号完成而观察被hold，取消原WaitAsync后不会消费Reply；旧body的late.IsDisposed在唯一断言行45准确失败，0P/1F/exit1。TRX记录peer signal=True、observation held=True、decoded entered=False、late disposed=False、slotbusy=False/selected=False，未借出的图片仍归夹具，finally自行释放gate/图片并join原file任务，未单独缓存/join诊断ObservePeer wrapper，不把该wrapper结算计作证明。不能把它称生产图片泄漏或回填r08唯一根因。

最小修正仅Provider夹具：DecodedEntered屏障紧接Peer.WaitAsync成功之后、Reply之前；Clear前等待此屏障以锁定“晚解码返回”路径，保留原取消/释放/零发布/slot断言和cancel-before-peer原例。外5秒guard不增加产品预算；正文、原任务和Clear清理错误EDI/Aggregate保全，自建图片finally释放。r10相关63P/0F/0skip（四个既有图片准备测试类），未增加案例数。独审补原Clear任务保全：提到外层保存，finally join同original file task后await同clear，不能另起Clear代替；r11该最小修正Bitmap准备4P/0F/0skip。生产src及Core测试相对r08不变；新树只补一次完整Provider和首次完整Codec/三构建，不无变化重复Core。各套件固定HEAD分别记录，不声称同一个HEAD跑过全部完整套件。

## 未验边界

合成ILocalOcrCapabilitiesWorker正例不是Windows.Media.Ocr，也不是真实WPF交互。真实Windows/OLE/HGLOBAL/来源身份/medium、OCR效果、WPF语言选择和确认、画质及250/600ms/600MiB仍未验；P6/P7整体保持InProgress。保留旧失败原件与未知历史唯一根因，不回填。

无真实模型/API费用、用户内容/电脑、新SDK协议/依赖许可/权限/凭据、runner403重试、新CI、main/强推/发布部署。PackageHold、生产复用Off、来源/密码/完整性/取消/清理/实际退出和发送门保持。
