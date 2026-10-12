# 文字 Alpha 退出合同父审补正

基线04407573fee5c3288f178298c29533539619e393的自包含发布准备获限定接收，Windows实际功能未验。父审发现run-text-alpha.ps1只记录childExitCode/文本比較，却不传播失败；desktop wrapper和Windows suite也吞退出码，真实失败可变成shell成功。

已修正三层实际退出：非零child保留5/6/7等正退出码，比较Fail/fixture不匹配为3，探针有界超时为4，缺少有效选择/探针证据NotRun为2。预检Outcome=PreflightOnly/Pass/0且Process0；有效公开子比较后限定会话结束或有界结束为LimitedSessionEnded/BoundedSessionEnded，退出0、Status仍NotRun、WholeAcceptance始终NotRun，绝不把会话结束当整体验收通过。异常、包校验或清理失败仍由终止异常非零退出。wrapper及Windows branch明确exit LASTEXITCODE，原finally清理与环境恢复继续执行。

测试用真实持有的pwsh子进程退出5/6/7/0，直接执行由生产AST提取的Stop-AlphaChild、完整finally/报告及后续退出语句；desktop wrapper使用原完整文件，Windows suite测试其确切委托后的语句。只省略Windows采集/桌面前置阶段，不模拟授权或声明运行Windows；不是另造结果模型。原24红例全失败，其中18实际退出码错误，另6缺结果类别；断连前夹具Execute参数错误保留，首修复夹具用子scope导致22失败也保留，改dot-source匹配生产finally真实scope后24通过。随后的真实哈希表receipt形态暴露6失败，改键/属性统一识别后最终24/24过；没有改期限/期望状态/断言。

实际生产TextAlpha receipt是Hashtable、探针是PSCustomObject，测试匹配这两种真实形态。预检在原1199文件包上直接执行并返回PreflightOnly/0；Linux桌面拒绝仍独立保留。没有重跑未变化的1836/63生产逻辑，也没把其旧结果称本次新功能测试；本次功能测试是24个实际脚本收尾/委托退出合同及实际预检。Windows实际Probe/WPF/应用/模型仍未运行。

10:06:35.362917Z实际恢复核对：HEAD与远端044一致，只有保全的tests/scripts改动、零活动写入进程。此前05:56:54起点证据保留，无扩权/重复Git或进程。全部29历史包和3份既有Alpha大包保持原字节；本轮只交付小型脚本补正ZIP，不重打大包、不覆盖已恢复ccf或任何旧包。旧覆盖/恢复事件仍保留。

旧Alpha大包内启动脚本仍是旧版本，**不要从旧包直接运行旧launcher来判成功**。使用本仓库本补正的run-text-alpha.ps1/run-desktop-tests.ps1，或独立小补正目录中的脚本，BundleRoot指向原经摘要验证的Alpha目录。不把补正文件写入旧bundle，否则其原manifest应拒绝；这样修正执行入口且保全历史产物。安装器/原生DLL/注册不包含，P0/E06 Blocked、P1—P4 InProgress、106整体验收NotRun，不进入P5/P6。
