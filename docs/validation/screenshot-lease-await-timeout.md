# 截图秘密租约 await 内预算到期负例

父c775db已有限准入。本片从其正常建dev/screenshot-lease-await-timeout，仅新增测试观察端口和4例，不修改任何生产源码，不预设缺陷、不制造红绿。

两协议各测分类/生成阶段：openSecret已进入真实async等待；释放后在该await内部推进单调时钟20秒(dispatch:false)，再构造并返回实际SecretLease。观察lease私有实际owned byte[]在返回前为非零公开合成secret，预算token尚未由timer取消；Run完成后同数组全零、HeaderValue抛Disposed，目标阶段没有新增HTTP，唯一失败事件和结果为RequestTimeout、未完成/已停止，原图保留。生成阶段允许先前已完成的分类HTTP1，正式生成HTTP0；分类阶段总HTTP0。此fixture确实构造并返回合成秘密lease；不声称调用了真实SecretStore/DPAPI，也不承诺所有managed header字符串被擦除，不冒充DPAPI/ACL/store验收。

r01新增专项4P，现有生产首次即通过，无需改源码。r02无筛选Provider实际2275P1F：旧PhaseDeadlineTests.IdleDeadlineKeepsPartialTextAndIgnoresNoContentActivity(AnthropicMessages, empty)在500ms Job.IsCompleted断言失败，新4均P。旧测试和协议/时钟源码未改。保全失败log/TRX/DLL；r03 --no-build同DLL旧idle族6P，r04 --no-build同DLL完整2276P。两个完整结果名多重集均精确父2272+新增4，四运行的26源码记录全部匹配当前(104/104)，相关目标五DLL均同字节。未取得首轮为何超过500ms的根因证据，继续Unknown，不能以复测宣称修复。

独立只读源码审查无P1/P2；封存终审回执另附。[原件索引](../test-evidence/2026-10-17-screenshot-lease-await-timeout.json)保留全部四次执行、源码/命令/TRX/DLL及失败说明。所有生产、SPEC/原书/追踪、旧证据含c775的499原件及其他旧测试/项目/锁原字节保持，不抬阶段状态。Core/App/Worker闭包未变，沿用c775已核Core1703P/AppWorker0W/E作为父证据，不声称本片重新运行。

预算片推送后，下一片为已授权失败/停止恢复菜单“重新截图”：共享真实截图链、显式菜单Intent，不伪造快捷键身份；取消框选保留旧会话，新不同像素裁剪仅本地预览，再明确确认两阶段发送；不复制旧图换ID。生产同图复用Off，当前图结果后的模式切换/重生成/重判断仍未启用。

真实Windows可见确认/焦点/锁屏、DPAPI/ACL、物理250/600ms、真实模型质量及完整P6未验；历史图片间歇/Job根因Unknown，旧Core历史当次条件Unknown。仅已有SDK/--no-restore且激活前后禁证书生成，无新包许可/Skia安装、403/Actions拒绝绕过、main/强推/部署/费用/真实凭据/权限；未知锁数据日志进程保全。
