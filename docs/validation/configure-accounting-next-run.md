# AccountingV1 专用来源门与唯一 configure-only 验证

本阶段已获父任务明确授权：本地来源合同、三运行文件与最终freeze独审通过后，正常推新开发分支一次，执行唯一标准免费Windows configure-only attempt1。准备快照中Windows尚NotRun，实际结果另行归档，不回填历史日志或旧拒绝。

## 唯一身份及三运行版本

唯一父9d75368aa246b40e2bfc9517eb390384c6faf470/tree2f0d8993206519bf42991055ee0abda92627db3b；新候选只能是其一个直接child。最终完整SHA/tree和全部六文件blob由交付回执冻结，推送前再次核对。入口ReviewedParent已从旧b4592d5同步为精确9d75368，其余入口字节保持；Accounting operation与投影helper相对9d基线原字节保持。

| 运行文件 | Git blob | canonical blob SHA256 |
| --- | --- | --- |
| tools/ci/LifecycleOperation.cs | b8b6fcf80cc763bf4289591765bb42e4bb31df37 | 585a20758ce699fdc0c927ddd451239008b11985134e2a529929ba28bea893a3 |
| tests/scripts/lifecycle-ci-evidence.ps1 | 208b2df950a05511078f5205f0b7442fabf4ca8e | fcebcab0813a3302a40564cdbb11e9a98ca445f2bc7f356bdb5a78b88abae7ea |
| tests/scripts/run-configure-only-windows.ps1 | 71e742f1642090c8690a3b44b5b704687bdb442d | a04683f16bb55d453821440071d41b2994594f0580e2cc0f23cb7e31c9848efc |

新独立AccountingV1合同与workflow硬锁三blob/三SHA，不改变旧合同、不作路径或版本宽泛豁免。工作文件通过git hash-object按仓库既有text规则核对canonical blob；SHA256直接读取git show stdout BaseStream的规范blob字节，不经PowerShell编码/换行重写。Windows既有CRLF checkout仅按原Git文本规则归一，测试明确覆盖；未改.gitattributes，内容字节漂移会拒绝。Get-Command git只取首个Application，stderr并发drain，输出不含原始异常。

精确六文件：新.github/workflows/windows-configure-accounting.yml、docs/progress.md、新docs/test-evidence/2026-10-15-configure-accounting-source.json、新本计划、新tests/scripts/configure-accounting-source.test.py、入口run-configure-only-windows.ps1。Compare-Object使用CaseSensitive，大小写改名也拒绝。

## 实际深度与拒绝反例

locked3ae69419a1152ea9694a3a9cf11130fbae645fa3/tree870a72ed3ef2ee31252c10e2d63cbcb859e7fd7b到父基线有20个单父节点。对真实owned本地bare及直接child候选，完整来源门已证明depth21拒绝、depth22通过；workflow因此固定fetch-depth22，没有猜旧12或绕祖先拒绝。

独立5项测试通过，覆盖完整新门、三个运行版本、唯一父及scope、其他六workflow原字节/分支隔离、CRLF规范化通过、working及committed运行字节漂移、额外文件、大小写scope漂移、错误eventSHA/父/hashpin拒绝。未改旧tests/scripts/configure-only-source.test.py与旧workflow；该旧六项实际4通过2拒绝，原因是旧Operation字节与旧ReviewedParent=b459门，完整日志保留。9d上一阶段的5通过1拒绝也原样保留，没有回填。

Accounting162断言/11场景、原configure171及旧证据45在本阶段实际复跑通过，保存8组父/当前等待轨迹、32清理变体及原summary/Exit3对照。新门首轮暴露Get-Command返回多个Git位置，修正首个Application后通过；新增case反例首遇bare --force-remove缺worktree，改临时index的原prefix后通过，失败日志均保留。独审复制到/tmp复测5/5与原三运行文件差异，最终freeze另核。

## 一次事件、资源与停止门

新分支dev/configure-accounting-windows，仅新workflow自身路径.github/workflows/windows-configure-accounting.yml的push事件。repo/ref/event精确门；六份旧workflow均不匹配该分支且字节不改。无dispatch、PR、schedule、matrix、retry、artifact/cache、安装或服务。只标准public windows-2022，contents:read；推送前确认public/未归档及同SHA运行数0，不用larger/custom runner或新账单设置。

固定checkout action d23441a48e516b6c34aea4fa41551a30e30af803，github.sha、depth22、persist-credentials=false、set-safe-directory=false。来源步骤最多1分钟，验证完整父/tree/locked祖先/tree、精确六scope、三运行blob/工作内容/规范SHA；执行步骤7分钟，job10分钟。custom pwsh -File模板保留原ExitCode和停止错误行为。

原前置检查→一次NativeConfigure→原operation救援/清理结算→允许时有限四文件采集→终止，不build/restore/四真实case，不调用成员探针。configure原参数/工具链、每operation30秒、JobEmpty与原救援时限/关闭顺序不变。根lease configure前建立并持有到采集结束；Python DuplicateHandle仍以源进程/目标进程正确复制；四文件各64KiB/总256KiB，未清理零读取、缺失不补造，原采集失败不覆盖原结果。

Accounting只复制原成功查询的ActiveProcesses/TotalProcesses，首次等待与最后救援前查询固定保存；失败Unknown/null，清理查询不覆盖。Active是Job关联计数，Total包括已终止；不用TotalTerminated推算全部退出，不识别进程/类别/根因。序列化在原结算后，Known不意味着清理通过；新前缀输出失败为Unavailable且原summary/exit继续。零新增原生查询/时钟读取/等待/线程/进程句柄；不承诺零指令耗时或同步API新的硬上限，外层超时不是清理证明。

独审最终SHA/tree/parent/六文件一致后，仅正常非force推该精确SHA一次，监测该SHA只一条新workflow/run attempt1到终态，完整tool响应及解码日志原字节保全。成功、失败、超时或取消都不自动重跑；失败仅本地分析。实际可能只有Unknown或有限计数，不能承诺找到根因。原configure根因Unknown、ProductionReuse Off、ModelCalls0，250/600ms及原安全门、P0/E06验收边界不变。

固定本地计数、规范版本和完整输出hash见[准备证据](../test-evidence/2026-10-15-configure-accounting-source.json)。
