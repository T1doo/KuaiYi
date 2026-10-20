# 本次WindowsCI真实失败与只读静态核验

父级正常连接已确认本次[run39347646426](https://github.com/T1doo/KuaiYi/actions/runs/39347646426)与[job118384016034](https://github.com/T1doo/KuaiYi/actions/runs/39347646426/job/118384016034)，精确源码00ecaae6def945cf39f3bdacf5fca7f8dc2d69e9，2026-10-20 00:26:24—00:30:24UTC，实际终态 **Fail**。checkout成功；第3步“Verify runtime signatures, install approved job SDK and run managed validation”失败。该步骤包含整条流程，尚不能认定签名、SDK安装、审计、测试或构建中的任何一项是首因，也不凭4分钟时长归因。

父级已登录助手浏览器详细读取遭`credential_observation_restricted`，依工具说明一次新文档验证仍受限并已停止。本机Actions读取此前Get Forbidden；本片没有重试任一读取、换路或盲重跑CI。父级观察按可信元数据保存，绝不冒充完整运行日志。

只读Git／AST核验得到：00ec唯一父提交正是声明的98c3f27d07ee392bf98e647861596812c2d63a39，四文件差异精确匹配runner的FILES；repo／branch上下文声明正确。此前推送前真实本地Git源码门Pass有原件，但不能据此断言hosted检查实际通过。workflow相对父提交只改名称，调用、checkout固定action／ref／depth、权限、runner与预算不变。

相对上一真实执行源码32116577，runner所有函数AST除source_gate错误文案外相同；将该唯一文案规范化后AST精确相等。精确父SHA／四文件scope常量是本次有意更新。SDK安装器、其测试、准备器、SDK／runtime配置、runtime库存／锁、NuGet配置／构建属性及其他8workflow，共18固定输入逐字节保持。**未发现可证的父SHA匹配或流程早期静态缺陷**；没有据此修改生产源码、流程门或再次运行测试／SDK／CI。

要继续定位，本次仍缺：

1. 完整第3步raw日志；若脚本尚未输出JSON，需要最先错误行及step failure annotation。
2. `KUAIYI_SESSION_RUNTIME_WINDOWS`最终JSON：SourceCommit、Reason、Commands各Exit／Outcome／OutputPrefix／LogSha256、SignatureStatus、SdkStatus／SdkInstallation、RuntimeAuditCoverage、ManagedTests、BuildStatus、TestCounters及EvidenceStatus／Errors／cleanup。
3. 首个失败命令完整raw／gzip、原始异常／stack／退出状态，以及已输出分片的序号、大小、hash。
4. 若有生成，Core／Provider原始TRX与最终计数；未生成则保留缺失状态。

现已有确切run/job，不应再次触发来代替读取现有失败。实际首因、阶段成功／跳过范围、计数与cleanup继续Unknown。旧45calls／25methods、生产修复Linux4F→10P及34P均是各自独立历史证据，不补签本次CI；图片间歇根因Unknown、包Hold，生产复用Off及250/600ms／来源／密码／完整性／取消／清理门不变，真实App／WPF／键盘／模型／完整P6P7未验收。

[父级可信观察与静态审计原件](../test-evidence/2026-10-20-owned-exit-ci-static-followup.json)。
