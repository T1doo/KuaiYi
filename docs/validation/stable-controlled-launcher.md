# 稳定入口接入原 generation 会话控制器

从065992060f1623cb651a507f2162be8c901f18d3在原指定环境唯一写入者继续SPEC13.3/13.4。新增KuaiYi.Launcher源码项目（仅引用既有Core、无新NuGet包）和最小父子stdio生命周期桥；不接统一安装/注册入口、不实际部署/注册稳定文件，不代签完整可运行Windows包或P7。

## 真实接线和所有权

Launcher仅接受无参数正常启动，Windows入口root=自身AppContext.BaseDirectory，data=当前用户KnownFolder/KuaiYi，不从环境变量或START消息取得任意安装/数据目录。现有七generation角色与其物化资格保持；Launcher不是新的UI或warm服务角色，其独立稳定文件的部署、摘要和更新属于后续统一安装入口，当前项目仍是普通managed交叉构建、不是自包含发布。

选择使用existing-only admission，不初始化缺失owner/store/原锁。短门内只捕获或复核current原字节，完整v3/源闭包验证在门外；释放选择门后只Start所选generation的App.exe，固定working directory/参数列表、不经shell、不传图片。App新--installation-controller模式只路由私有继承stdio，验证canonical nonce/root/数据/所选路径；实际coordinator仍在其原admission下复核exact selection、当前v3及原owner/pending门后才ClaimExecution/Start原UI。选择到Start之间发生换代必拒绝，不自动重选、重放、Show授权或cold fallback。

同代App继续作为原控制器和真实warm peer，复用原InstallationSessionEntry及Windows图片适配器。原UI ADMITTED/READY成功、Host session实际发布后，才由原Call跟踪的短BOUND_READY写通知外层；外层reader不进入内层Role.Tasks。新Launcher不接管Host、UI子进程或warm认证，不按PID发现、收养或终止任何进程。nonce/指纹是私有生命周期关联，不是图片授权。

取消、超时或原IO错误独立关闭实际Start-returned controller的原stdin BaseStream，不等writer锁、不直接kill控制器。Bridge EOF/STOP撤销原Run token，沿原coordinator取消与保留fence语义处理。成功必须原Run0→Closing→父关闭原stdin→原reader/cancel/write全join→EXIT_READY→实际controller退出0及外层Start/原IO/退出观察结算；Start、Show、单独exit0、错误或迟到READY都不等于健康。失败无成功收据，外层不会清理coordinator的execution owner。外层CleanupConfirmed仅说明其controller handle/原任务已结算，不能证明失败App的UI owner已释放。

## 实施中独审与红绿

原件artifacts/2026-10-22-stable-launcher保留全部命令/日志/TRX/失败XML、逐输入/DLL摘要及candidate源码。各轮不相加。

- 新Launcher初始restore和随后locked restore均成功，锁图只有Core project reference；无新包/SDK许可接受。sln纳入正常项目，不运行安装器。
- r01 Core编译失败CS0103，原全局替换误改ExecutionOwnership的OpenOrCreate；已精确恢复，零测试。
- r02前3普通项目构建成功，App缺System.IO using编译失败，零测试；补显式using，不关闭分析器。
- r03七个普通构建命令全部0W/0E，含正常App及新Launcher；892输入稳定、锁未漂移。
- r04反例测试误选Assert.Throws(Func<Task>)，CS0619编译失败，零测试；改Action block保持同步拒绝合同。
- r05独审四屏障反例0P4F→r06同测试4P0F：Bridge原写未settle仍返回/并发Closing，Dispose后可Run，以及同步write owner未发布时CleanupConfirmed已true。测试没有sleep，超时观察用internal受控observer，生产默认10秒不变；红绿测试字节相同。

修复为全部短写串行并逐个join；超时仍保留失败并继续await同一原Task，CTS不早dispose。还显式join原coordinator callback producer（非WaitAsync外壳），防原Run已Unconfirmed但callback尚未执行的新写发布。Launcher成功/catch两路封闭任务发布，原startupObserver结算后才采IO快照；Dispose后禁止Run，stop.CancelAsync保存同一原Task，对外清理状态需原Run terminal；READY后继续监控IO故障。上述生命周期修复已获独立只读限定ACK，最终树仍须固定构建与相关签收。

- r07新两类19结果16P3F，三项原始堆栈完全相同：新Peer构造在管道未连接时设置AutoFlush触发Flush，尚未进入产品路径。修为连接后懒建writer/reader、显式flush；Dispose三个原资源分别结算。新取消断言同时对齐原会话合同，不要求生产从未承诺的PUBLIC_SHELL_CLOSED，不改变生产取消或旧断言。
- r08同19例19P0F；r09增加原session错与原CancelAsync callback错双屏障保全，并断言无EXIT_READY，20P0F/0skip。两轮892输入稳定；不是Windows实测，各轮不累加。

## 合成验证的边界

新主路径测试用真实Apply得到随机generation和v3，实际启动两跳dotnet合成进程并复用生产SessionEntry、coordinator、stdio编码/受控UI接纳和退出代码；只在明确测试starter中把Windows App.exe传输替换为已知fixture driver，生产默认仍SystemOwnedProcessStarter。fixture只用公开中性数据与PUBLIC_OFF sentinel，未运行WPF或真实Windows身份。Source中缺少的native/runtime/第三方仍是structural fixture，不能作为可运行Windows包。

原3项native DLL及完整自包含Windows runtime、稳定bootstrap实际部署与升级、身份/注册/安装、真实Windows/browser/medium/warm/WPF像素/OCR质量/250/600ms未验。原586/105原件不改，不重跑无关全套。未新增安全权限、凭据、费用、模型调用、SDK协议或用户电脑操作，不重探403 runner，不改main或发布部署。
