# P7-01 正常安装冷启动单图预览

从 `d6b776b84a8b804e1cb5619c40c2befb9b40ab96`、clean、远端0/0、单worktree接续；唯一root writer，原reviewer仅只读。SPEC3.4、7.1/7.3/7.5、13.1及P7-01为本次边界。P7-01仍InProgress，Windows11真实菜单/安装/WPF链路没有因此通过。

## 生产接线

- `ExplorerCommand.cpp`原固定同目录App和Windows argv转义不改；`Program`将单个 `--prepare-image <path>`从 `--platform-review`拆出。只接受原Core受限单图路径语法；不在CLI阶段读取图片。不支持的非安装布局拒绝；正常安装布局走原InstallationSessionEntry、选中generation、execution-owner、真实Start返回子进程及原stdio，子进程参数仍只有原installation-handshake。显式platform-review继续P0。
- 原READY后，原Role.Track/Call发送一次内存IMAGE offer：launch、原proof fingerprint、独立requestId、单个路径；严格规范Base64+JSON/长度上限，复用OleImageFileDrop.ValidatePath。子进程必须已Claim原启动证明，再精确匹配offer并检查原安装记录/持有租约。请求消费后即拒绝重复及另一requestId；IMAGE/OFFER双向互斥。路径不写安装记录、配置或诊断。指纹是关联校验，权限来自原受控会话，不把SHA当秘密凭据，也不声称CLI证明真实Explorer来源身份。
- `IMAGE_QUEUED`仅代表请求已消费、交给App登记排队；不代表文件任务成功安装、读取/预览成功或用户同意发送。忙、过时和关闭期间请求也不重试。App在排队前冻结线程安全gate、原子input/result ordinals及不可变configVersion；UI执行时比较原stamp，再核启动健康/可见性/设置/忙门。原dispatch Task的发布与close标记在同一lock内，关闭后不可新登记，退出先join原dispatch，再结算文件任务。
- 正常Shell复用原FileImageIntent、StartImagePreparation、FileImageOperation、受限文件句柄/最终drive/reparse/大小/签名、ImageWorker.PrepareFile及独立file owner。完整安装payload哈希验证留在control后台和原fileTask的后台验证Task中；UI仅核有界owner/current/accepted/pending记录和held leases。post-load验证未返时，图像由load委托明确持有；验证失败/取消显式Dispose原图，外层仍持有原worker owner并结算。
- STOP/EOF/control fault立即撤销独立image token，缓存并在原SettleForExit中join原CancelAsync Task；原控制循环不等待Dispatcher或Worker，因此仍可读取EOF。外部原token、current/live records、原ImageAdmission和epoch/activation/input/result身份共同拒绝迟到、隐藏、锁屏、取消、设置/来源替换。
- Off只准备本地预览，不Toggle、不OCR、不创建HTTP请求。GIF继续可见“使用首帧”，无桌面坐标的file owner不转截图owner。原“启用并翻译”与目标/模型/本次发送确认、OCR独立确认不改。已有实例仍Show并拒绝图片转交；未增加未经认证转发。

## 实际验证（不累计重复项）

| 原件 | 实际结果 | 精确范围 |
|---|---|---|
| r01 | Installation相关154P/0F/0skip | 真实合成安装目录/子进程/原控制stdio；含初版单图10例。当时Base64白空别名尚未收紧，旧handoff case只用非法payload，不能独立证明反向guard。准确源码保留。 |
| r02 | 完整Provider2449P/0F/0skip | 原2436案例全名多重集全部保留+13新例。直接链接生产排队登记、文件命令、预览/清理，WPF与文件字节端口为合成；覆盖Off首帧、排队后取消/清除/锁/隐藏/设置、关闭join、held验证清零与未确认owner拒替换。原发送/HTTP测试仅本地fixture，无真实模型。 |
| r03 | App Windows目标交叉0W/0E | 初版构建，后续Base64补正后另有r06。 |
| r04 | Session+Offer51P/0F/0skip | 收紧Base64后的原35 Session+单图10+Offer6。4个空白alias先证解码字节相同再拒绝；handoff case先等原revoked+cleanup屏障，精确核InvalidDataException，区别缺guard时FormatException。 |
| r05 | 新取消屏障1P/0F/0skip | fixture仅新增held-image-cancel模式。原callback hold时在cleanupEntered屏障观察同child/run pending且owner仍在；释放后实际exit0、原IO结算、owner移除。该屏障早于control join，未做去join突变，不能独立证明缺join必红；原Task精确join有静态独审支持。 |
| r06 | 最终App Windows目标交叉0W/0E | 最终产品源码，未执行Windows WPF。 |

r02后产品仅增加Base64规范编码拒绝，Provider链接的命令/预览/清理源码此后无变化；最终Provider未再次全套运行。r04之后仅测试fixture/单例补充，产品源码不变。新树未重跑完整Core或Codec；上一固定5e95cd0b完整Core2074P仍仅属于那次HEAD，不追认本树完整Core。所有命令、完整日志/TRX、初版/修后准确源、812最终输入摘要、DLL清单和旧用例多重集对照见[索引](../test-evidence/2026-10-21-shell-image-preview.json)。

设计独审两项P2（双向handoff互斥、UI不得完整包hash）实施前闭合；源码独审Base64 P2已补正；最终产品源码限定ACK。r05按独审限定陈述，不虚增证据强度。reviewer未构建、测试或写入。

## 保护及未验

原807输入中797项逐字相同，仅10个既有文件变化，新增5个源/测试文件。SPEC、AGENTS、全部依赖锁/版本/许可、格式解码/Worker/发送/OCR/来源与密码校验、原SHA/清零/真实退出、250/600ms合同不改。WebP奇数payload拒绝、PackageHold及生产图片复用Off保持。历史失败原件不变，不回填未知历史阶段/唯一根因。

真实Windows11现代菜单、实际安装身份/来源、完整CLI到WPF到文件句柄/Worker预览、OLE/COM/OCR、真人画质、250/600ms与600MiB、完整P6/P7/106例/Beta均未在本轮验收；warm-instance认证交接仍缺。未改隔离session-test编译变体入口白名单（其仍只支持既有normal/handshake），未冒充它已验证图片CLI。无新SDK/协议许可、依赖、凭据、安全网络权限、费用、用户电脑/内容、真实模型、CI运行、main修改、强推、部署或发布。
