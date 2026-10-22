# 原失败会话的新三方补偿（限定内核）

本片根据[独审合同](../design/deployment-compensation-contract.md)实现。只接受原Scope确定Committed且原session=RollbackRequired的一次私有授权，持续持有原execution对象及lease，在同controller专用serial下取得setup→Host exclusive→upgrade→Attach原execution；同ledger精确读取记录，不按PID、旧archive或新execution收养。没有新增App/安装器/真实注册入口。

新Ticket≤16KiB、计划≤128KiB，绑定新operation、原operation/failedLaunch、Committed/Before/Prepared/plan/execution摘要及原历史。原registry owner重入后复核exact committed targets及旧G/B/Core/revision/Host归属闭包，H receipt→Edge→Chrome→B active→G current逐步恢复，每步核整个合法逆序前缀。foreign、关闭未知、取消、partial和write-after-effect保原异常与pending，不写更早依赖、不重放。首次安装恢复current/active/H缺失，保留已归属B owner/bundle与物理空键。

Closing只在完整恢复且取消未赢时进入；专用retirement owner在原lease held时精确Move记录，Retiring后旧live Require永久失败，不全局放松。MoveConfirmed要求Move及原核验正常返回。原finalizer拥有Closing fresh reads/terminal IO，包含关闭后复核也逐Task/句柄结算；原admission/execution/serial及终态IO全部确认关闭后最后archive Move才Compensated，之后无新增IO/Dispose/serial释放。原部署Committed历史不改。Compensated只证明本内核三方恢复，不证明旧会话READY或Windows安装成功。

Unconfirmed仅可对原resources同owner关闭重试，缓存原Task、保全部错误与历史；不能重放/清pending/借新读洗白/提升成功。成功归档后的Settle为内存空结果。测试fixture分别保正文+清理错且join原settlement，未知原Task保Rig/Stop/root。

候选原件在 `artifacts/2026-10-22-deployment-compensation`：r03三F均Mixed revision，未到取消断言，保原但不计目标红；七managed同步后r05三条真实目标F→r07同完整file三P（hold原registry复核取消、BeforeMutation取消、temporary准备后取消）。r08旧fixture矩阵39P；加强fixture保错后r11 cache原Task反例1F→r13同完整file1P，最终新43待固定签收，不把不同轮数相加。r01仅编译零用例（最初目录日期标签修正，原命令日志保留）。

前片191＝新22＋旧169，不是旧262；本轮计划新43＋前片191，旧Prepared等204范围明确未跑，不借旧通过数。原22方法从首Fact到EOF保持字节一致，修改仅其partial/测试辅助以复用原真实协调链；不删断言或提高时限。

真实Windows/medium/Explorer/浏览器/WPF/完整注册安装/P7未验，3 native DLL及完整自包含runtime缺件仍不能以结构fixture签可运行包；OCR/画质250/600ms未代签。不操作用户电脑、不新增权限/凭据/费用/许可或真实模型调用。尚无自动resume、fresh进程恢复、原失败提升或完整安装入口。
