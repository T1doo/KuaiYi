# 统一部署的原 owner / scope / journal 第一片

基于0c9c565492bbb7fbbc0297e122425902921e6dad设计的根侧限定ACK实施。本片只交内部existing-only入场、持久Ticket和已结算AbortBeforeSnapshot；没有generation/bootstrap/Host发布、Before/Prepared、Committed、跨进程恢复、历史查询入口、fixed locator或真实注册入口。完整统一事务仍InProgress。

## 资源与终态合同

InstallationAdmission先创建owner，再逐层获取setup、Host activity及upgrade原lease。setup/Host/upgrade句柄先登记后校验；Store.EnterAdmission、ReenterUpgrade、ReenterHostMutation和HostActivity获取失败均分别收集正文与原关闭错误，单错EDI、多错Aggregate。其他子资源关闭未知时保留原setup；scope持有setup直至Ticket完整Write/Flush/实际关闭。失败原owner强引用保留，不能返回可用admission。

scope先发布原acquisition和finalization Task，再允许启动。完整入场才CreateNew既有upgrade-pending.json；schema2/kind/operation/root/data/两源SHA/随机generation与bundle均有界16KiB，旧合作入口持续凭pending拒绝。partial Write/Flush/Close不删除、不覆盖、不签干净Abort，且原FileStream结算只重试同一对象。

所有final-read owner在启动前登记并封闭producer。Closing前最后观察取消；Closing后新文件读取、Terminal写入与只读registry adapter的原Task/句柄都属于此前发布的finalization。真实Windows registry adapter继续使用既有raw-key保留机制；本轮只合成槽+真实FileStream屏障，不调用Windows注册表。

原admission、读任务、原registry key及文件句柄全部结算才写AbortBeforeSnapshot Terminal（Before/Prepared摘要必须null）。exact ticket/temp验证、foreign backup拒绝后Replace，原Ticket留operation侧文件；最后Move到failed archive是本片唯一完成点，此后没有可失败的资源清理。显式settlement在资源已清时也不再次调用reader close。Move失败或已Move但收据抛错均Unconfirmed，不重建pending、不补偿、不自动重放；已存在归档仅留原件，不充当启动授权。

显式原资源重试Task先发布并缓存；并发请求共用同pending Task，CleanupConfirmed同时要求原finalization及重试Task结束。未知/partial journal即使资源后来关闭，仍保留拒绝，不改报成功。

## 实际候选与独审

原件目录artifacts/2026-10-22-deployment-scope保留每轮command/log/TRX/失败完整XML、输入及DLL摘要、对应源码。轮次不相加。

- r01 legacy admission获取原错被Dispose原错覆盖，确定1F；r02同测试文件1P，但新增Scope文件与命令可能重叠，故仅开发诊断，不作为固定输入签收。
- r03 scope候选19P/0F/0skip。独审随后发现显式settlement并发未缓存Task的P2。
- r04受控Close屏障同一测试1F（两个pending Task不同）；r05完整测试文件字节相同1P，保留原异常与屏障，无sleep/删断言/放宽预算。
- r06新25例+原InstallationTransactionTests45例，共70P/0F/0skip，902输入before=after，dirty候选；覆盖三层获取/关闭双故障、partial、取消writer、Closing两胜序、原文件/registry读取及关闭双错、单pending重试、foreign backup、最后Move失败/收据丢失、完成后零再次清理、缺store与未知pending。

独立只读reviewer对当前源码及r04/r05/r06原件限定ACK；未代替运行、未写产品。产品冻结后再做一次所需普通managed构建与包含原升级/BrowserHost入场的定向签收；当前尚不借记旧310/586/60或完整Core/Provider/Codec。

## 保留边界

没有三参与者接线或任何Committed路径，不能把Ticket/Abort称为完整安装/卸载。未验证真实Windows/medium/浏览器/WPF/Explorer/OLE/OCR效果/画质/250/600ms；未运行安装、注册、身份包或用户电脑。仍缺三native DLL及完整自包含runtime，框架依赖开发形态不当可运行Windows包。原Off/PackageHold/发送门、来源/密码/完整性/取消/隔离及固定250/600预算不变；未新增包、许可、费用、模型调用、凭据或安全权限。
