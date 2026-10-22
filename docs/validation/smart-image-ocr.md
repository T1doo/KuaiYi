# 智能图片流程联动本地 OCR

从 `5f0a0c309fe1887b4d1fa28812cc167bef5e7791` 在原指定环境单writer继续SPEC7.4主路径。正常Shell的智能按钮/默认智能截图动作现先本地OCR能力查询、明确选择本机语言、原Worker实际结算、文字预览批准，再单独确认端点/模型和两阶段发送；不是把OCR-only改成自动发送。默认Off、不自动批准或fallback，不扩大格式、权限、SDK或依赖。

## 产品接线和安全合同

`OcrOnlyFlow`共享原语言/文字预览窗口，智能分支明确“本地批准，尚未发送”，不显示错误的“模型不支持图像理解”提示。独立`ApproveForImage`只接受原current preview私有引用，签发不可外部构造的一次`OcrImageApproval`；旧`Approve`文字来源独立生命周期不改。能力/识别worker的原Run/finalize/dispose确认后才有preview/批准。无语言、不支持、尺寸超限、取消或清理未确认均不转HTTP，不借自动降级跨越门。

Automatic在生产协调器Prepare/IsOfferCurrent/Submit要求same批准，不能仅靠Shell先OCR。准入从原OCR epoch到同ticket+1的唯一image身份，一次绑定正确vision profile；ActiveProfile与VisionProfile可不同。每次阶段、排队交付及HTTP前重验原source/version/settings、epoch/On/suspension/来源owner、精确批准引用，失效永久撤销。未launch offer在Prepare替换/风格/Stop/Clear时精确撤销；已launch由同Job最外finally释放，含TryLink早返。批准正文引用随Dispose清除，版本snapshot只存previewID/语言/是否文字路由，不存OCR正文。

分类仍发送原图片；仅合法`text_only + clear`正式生成使用精确批准文字、原文字风格、同次确认的vision模型/端点且Image=null。mixed/visual/uncertain及partly_unclear保持原图分支，不向其夹OCR正文。unreadable和非法/不完整分类保留原停止合同。正式文字system使用原PromptBuilder，正文不进入Plan.User/system或长期snapshot。显式TranslateText/ExplainImage原视觉入口不变；图片复用/再生成仍Off。

父smart Task在入口同步冻结exact retained/image/source/settingsVersion/input/result/ticket，首Yield后核同stamp，旧点击不能转新图。原Offer Task仅一次CAS安装且先于Begin；新smart发送helper在所有成功/失败路径join它，不用可变flow.Running替代。Clear先取消/关modal，再分别join原OCR/image/smart；StopTranslation在provider dispose之前join。body/Reject/原Task/Dispose错误分别保全，单错EDI、多错Aggregate，不形成image与smart相互等待。

输出检查原方法体逐字抽取到`OcrOutputPolicy`，原ImageWorkerClient公开签名转调；检查不变。Provider测试直接链接同OCR/批准/输出policy源码，Worker正例端口明确合成；HTTP使用既有真实loopback双协议。真实Windows OCR不由这些合成正例代签。

## 已实际执行与失败保全

- r01 App交叉构建exit0/0W/0E。
- r02 Smart26执行24P2Timeout；两新队列测试一次排空可能早于post且未持续处理原Task队列。原件保留，改为跟踪same原Task，不认产品红或历史根因。
- 独审确定3项候选P2：启动即Stop早返漏释放、父入口Yield前未冻结来源、取消未launch offer未撤销批准。r03同8例0P8F，r04同方法/测试源码原字节8P0F；保留红绿原件，不降断言。
- r05 Smart37P0F0skip，含原worker held-return/清零、exact父Task Clear、Begin同步替换为另一图片时仅join本offer Task，不等待替换job。
- r06相关回归stdout可证7失败，随后无进展；保全日志/源码后SIGINT确切自有vstest，exit1，无TRX，总执行数Unknown。通用runner原result错误写`tests_executed=0`，单独correction明确不能当零执行。自有testhost随后exit为PPid1 zombie，额外SIGTERM脚本因空cmdline先断言退出、未发送信号；不删锁/未知进程。5个大截图视觉预处理例因共享夹具默认Auto而在OCR2560门先拒，2个文件例仍要求纯文字最终带图。新同步夹具在持Gate通知中GetResult等待OCR continuation的死锁机制有源码闭环，但没有当次等待栈，r06唯一挂起原因Unknown。
- 原视觉回归助手改默认显式TranslateText，保留预处理/设置暂停/两阶段同图原断言；显式Automatic使用实际OCR协调器批准，新增Smart来源例覆盖file/browser/bitmap。测试桥提供async批准，遗留同步UI端口检测持Gate并拒绝阻塞；生产流程从始至终async await。8个风格例按本片正式纯文字行为改为精确原PromptBuilder system/version，另核OCR元数据和Plan无正文。
- r07/r08仅新夹具将Rig.Image扩为基类后target-typed new漏改导致CS0144编译失败；均0测试/无TRX，不算产品红。r07通用无TRX字段Unknown不用于计数，编译日志证明0执行。补显式OwnedSourceImage构造后r09相关18P0F0skip（含已见7失败对应、原两阶段视觉与三类非桌面Smart来源）。

r01输入861、r02输入863、r03—r09各864；各轮自身输入前后不变；dirty候选轮不冒称固定提交。随后冻结产品/测试，只执行与改动相关Provider选择及App/Worker构建，完整Core/Provider/Codec本片NotRun，不相加轮次，不借记旧结果。r10固定产品/测试 `bef61b5f2935a4d44851b461c48e844c13f75250`，相关Provider **458P/0F/0skip/exit0**，原选定418全名多重集均保留、新40；App/Worker各exit0/0W/0E。明确filter仅相关12类，不是完整Provider。864输入执行前/每阶段后/最终完全一致且匹配固定Git，4个目标AssemblyInformationalVersion均含此完整SHA；339 DLL当前摘要一致，是库存而非用例数。完整命令、日志、TRX和失败XML均保全，最终原件/源码/报告获独立限定ACK，未见新增P1/P2；ZIP经独立CRC及逐成员核验，337唯一成员/2,225,210B，SHA256 `5013147b489f670ec8e23fbae64c1df2f6abce985ae44c508cbaf7b499f31000`；Library确认 `libfile_0ed7d88d3e08819182e4962783ea2391/v0`，上传后本地xattr与ZIP摘要不变复核通过。包内报告为上传前快照，最终仅追加此保存回执，不循环改写原件。

## 验收边界

Linux合成正例和实际协议/任务清理不能代签Windows本机语言/身份/真实OCR效果、WPF模态焦点/屏幕阅读器、Explorer/Chrome/Edge/medium warm、多屏、真人画质、250/600ms或600MiB。P6/P7整体仍InProgress，PackageHold、来源/密码/完整性/原SHA/取消/清理/发送门及WebP odd拒绝保持。未做安装器/自启动/图像再生成/visibleCrop，不重探runner403，无新依赖许可/权限/凭据/安全网络/费用/真实模型/用户电脑/main/强推/发布部署。
