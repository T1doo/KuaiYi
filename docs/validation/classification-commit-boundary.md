# 分类成功提交与截止的线性化边界

父级独立审查发现上一片30秒阶段门的确定P2：canonical目的线程已在29.999秒提交分类，`IdentityEventReceiver`使用异步continuation；若caller在30.001秒才恢复，commit之后的phase.Check仍把成功分类改成REQUEST_TIMEOUT。此前DoesNotLeak测试仅在第二次取凭据时推进时间，没有覆盖Post返回前过期。

本片独立于暂存的桌面样式工作。生产只改ProductionScreenshotCoordinator：在canonical callback内最后检查phase.Check并提交；提交后只检查原总timeout。原receiver仍在同Gate检查身份、来源和阶段budget，未提交而恰到30秒/超过30秒仍拒。正式生成继续核原总预算/Eligible/逐次发送门，不重置预算、不自动fallback、不增加发送授权。分类timer在原scope结算并dispose。

原件 `artifacts/2026-10-22-classification-commit/`，各轮命令/完整日志/TRX/失败XML/源码及DLL摘要保留，轮次不相加：

- r01编译失败，缺新测试文件的App using；零用例，不算产品反例。
- r02旧生产、新反例0P4F：双协议各timer dispatch true/false，测试destination同步调用真实callback后、Post返回前推进29999→30001ms。第5次Post已写Classification；四例HTTP期望2实际1。
- r03修复24P0F0skip：同四例测试源码与r02完全相同；另含最终commit恰30000/30001ms拒绝四例，及既有阶段相关16例。868输入前后一致。
- r04新增保护4P0F0skip：双协议在成功commit后、Post返回前Off或总120秒到期，均1次HTTP/1次凭据读取，零Generating/Completed；总预算到期仍REQUEST_TIMEOUT。868输入前后一致。

生产最小差异及原件获只读独审限定ACK。冻结后相关整类签收和App交叉构建结果随后补充；不借记以前代码树结果。仅合成、离线loopback协议及受控时间，不调用真实模型。未改依赖/权限/main，不重复完整Core/Provider/Codec。Windows实机、界面像素、OCR质量、250/600ms和整体P6/P7未代签。

固定产品/测试 `94ffa5e4dbab681367912f7d7ab7b3e9dd12178a`：r05完整ProductionScreenshotTranslationTests 162 + ImagePhaseBudgetTests 3 = 165P0F0skip，原同范围153条全保留+新增12；r06正常App0W/E。868输入前后=固定Git，App/Providers/ProviderTests三目标版本含完整SHA；产物摘要是库存，不冒充全部执行。独审已核165条及输入。

原件Library确认 `libfile_b0c365efc690819195b2c183284e1ff5/v0`，719619B/92成员，ZIP SHA256 `72e582976c505b88d3401d3e607d3ae2bb72b7919d28dd5a5c23f1aecbaad15e`；保存后所有返回xattr及本地LibraryID/摘要已复核。后续仅三报告提交，不改产品/测试。
