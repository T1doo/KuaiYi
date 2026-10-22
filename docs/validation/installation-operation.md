# 单一安装操作编排（限定内部入口）

按[合同](../design/installation-operation-contract.md)复用原Scope准备→三资源提交→新受控会话→仅原失败授权补偿→旧受控会话。一个原Task/owner贯穿；无新磁盘journal/locator/重试恢复机制，无真实安装入口。明确NotCommitted/CommittedNotReady/NewVersionReady/RestoredNoPrevious/RestoredPreviousNotReady/PreviousVersionReady/Unconfirmed；阶段结果与后续原生命周期/cleanup另签。

READY只读原canonical成功资格，结果发布后仍join原session.Completion，晚到取消/错误仅进Settlement，不二次补偿。仅原RollbackRequired、原任务结算与原execution仍held才用None完成既定反向清理，取消后恢复不自动重启旧版；首次为空零旧factory。提交Closing后取消/外来owner阻断没有原execution，明确CommittedNotReady，不制造回滚授权。工厂任务先登记、迟到返回复核取消；unused-close只在原serial内确认fresh并封闭内存，绝不generic Dispose其他工作；unknown保原operation/全部owner。

候选原件 `artifacts/2026-10-22-installation-operation`：r01测试项目编译失败CS0619/xUnit2014，重复Start同步拒绝断言误选Task重载；零用例，不计反例红。改Action块后r02七普通managed同步，r03/r04只Launcher同步均0W/E；r05新矩阵15P/0F/0skip（924输入稳定）。均为必要负例注入及真实合成Start/stdio/退出链；没有宣称不存在的产品3F→3P。随后仅补独立旧Peer的外层清理保留门，产品与新15测试源码不改。候选产品/fixture独审限定ACK，随后固定签收见下。

固定一次范围19＝新15＋原4代表链：既有CommitIsNotReady...1、CompensationRestoresOwnedUpgradeOrInitialAbsence...2、RestoredCompensationIsNotReady...1。旧266集合只重跑这4条代表，其余未跑；Prepared等204及完整Core未跑、不借记，不把候选15和固定19相加。只对Sources抽取和原完整合成链必要交互验证，原会话22、补偿43、恢复会话32用例对应的方法正文不动。

真实Windows/medium/Explorer/浏览器/WPF、真实安装/注册/卸载、三native DLL与完整self-contained runtime、实际桌面健康/画质/OCR250600/完整P7均未签；结构native/runtime fixture不签可运行Windows包。无用户电脑、新权限/凭据/费用/协议或真实模型调用。

固定产品 `6d04d197683f8841c906c5d33fed0e1800fe51ed`：r06七普通managed构建0W/E；r07一次19P/0F/0skip（19唯一显示名）。新15精确等r05集合，原4精确取自前片266的指定三方法（1+2+1）；新Operation及完整新testfile与r05字节相同。924输入建测前后逐Git相符，17项完整产品版本/14产品DLL建测库存摘要/4实际Core副本一致。372旧src仅Session增加纯内存pending观察+1new Operation；342旧test仅helper变动+1new testfile，原22方法suffix与Comp43/Restored32全文件均字节保留，其余旧src/tests及tools/依赖/锁/项目/工作流/扩展/打包不变。8条新case含实际合成child退出及原IO结算文本，其余为正确零Start等负例，不虚称15条全部启动子进程。

原件Library `libfile_a500dcbf7ac881919ccd26aa18571e5c` v0，1,193,473B，129成员/128manifest，SHA256 `0c95a452d71f64b92ce9d05fa700b10b43e2b5bcb36199e19c7701cabca2e213`，本地Library身份已确认。固定树及ZIP原件独审限定ACK：CRC/128项大小SHA/覆盖及安全路径、10项Git原件、raw19/新15/原4、924四快照/17版本/14产品DLL建测库存/4Core及七0W/E日志均独立核过；未重跑产品。无DLL本体，不冒称包内二进制独立重hash。ZIP保留冻结产品时的候选报告快照；本次证据仅三报告更新，产品/测试/合同不变。
