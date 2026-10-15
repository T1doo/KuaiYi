# 本地安全诊断冻结与后续唯一验证方案（未授权执行）

本轮仅本地开发/测试/只读独审，不上传、不修改远端、不启动 Windows CI。14534bd439f7dd2df3015725e03cbdda101f0a9c/tree3e54f27821e2849ff294f1bce3599e1fa5da41a5 的唯一parent为1f78ac3cf2073895dab8c6a3ab3e936ce064d143；原14534bd和旧失败原件保留。最终候选将全部累计变化聚合为同一parent1f78的一child，避免中间本地提交造成depth合同错误；精确SHA/tree/逐文件字节哈希与完整diff另存 ignored 交接manifest，独审针对冻结身份。

## 新增有限诊断

14534bd没有独立的救援前Job状态与BudgetExpired，旧receipt不能补推这些值。新Receipt.PreRescueJobState只取Unknown/Empty/NonEmpty；初始Unknown，原WaitJobEmpty查询前清为Unknown，查询成功记录观测；查询失败保留Unknown。不增加查询次数、PID/路径/命令/异常输出。它只描述操作完成等待阶段的最近一次原查询，不是精确救援瞬间的原子快照；其他stage尚未查询为Unknown。

BudgetExpired记录原ResumeThread、WaitRoot、WaitJobEmpty决策点的30s预算判断，默认false表示没有在这些决策点观察到耗尽，不是全操作未耗尽证明。纯CompleteJobWait保存Job状态与expired两操作数后沿原 !empty || expired 判断，不改变阈值/等待/失败处理。finally仍用原Empty更新最终JobEmpty、RootExitConfirmed和CleanupConfirmed，不覆盖新字段；救援后空不能覆盖救援前NonEmpty/Unknown。有限枚举projection拒绝未定义值，旧失败JSON不迁移、不回填。

固定MSBUILDDISABLENODEREUSE=1仍通过实际受控ChildEnvironment，覆盖宿主0；不是只设workflow环境。原5环境/真实SDKsetter合同保留；新14断言覆盖四种empty/expired组合、清理后前态不变、默认Unknown与非法枚举拒绝。实际105/105、72/72、45/45通过，见[本地原输出](../test-evidence/2026-10-14-safe-lifecycle-local-contracts.txt)。无BuildManager/build/native Run调用。官方开关语义不证明Windows残留进程或本次根因，禁节点复用仍是候选修复。

## 拟议一次上传及一次验证

仅在父级核验冻结manifest/diff/独审并取得新授权后，一次交付累计文件至dev/p0-com-selection-diagnostic；先核远端仍1f78，逐blob字节/完整tree核对，创建唯一parent1f78的正常非force提交。API交付SHA可能不同，必须tree等于冻结tree。发生ref漂移或identity失败即停，不强推、不在新parent上自动重整。

workflow沿原push路径触发一次Windows2022 attempt1，checkout固定event SHA、depth6、persist-credentials false、contents read；不使用手动dispatch或retry。先核精确HEAD、唯一parent1f78、锁定3ae694祖先/tree/runtime inputs clean与diff；取第一个git/cmake/dotnet，vswhere检查现有工具，无安装。构建仅现有两生命周期工具，然后一次Normal、BoundParentDeath、BeforeBindParentDeath、Watchdog。每外部操作仍30s成功预算、已有held root 2s与Job 2s救援清理观察；step8min/job10min，生产250/600/120/2000ms不变。

任一操作非Completed、exit非0、rescue、cleanup未确认、身份/协议/case断言失败即停；不自动重跑、改预算、弱断言、安装、上传artifact或raw日志。收集固定新字段和旧清理字段分开报告。仅四个非目标fixture合同，真实UIA/OLE原文、匹配、性能、Windows11普通用户/实体输入/多屏DPI/安装卸载仍NotRun，ProductionReuse Off/ModelCalls0。成功也不能宣称整体P0或产品通过。
