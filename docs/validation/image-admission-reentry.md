# P6 离线截图请求接纳重入与分类提示契约修复

基线 `04f4cdb85634d46726b4de78c3b18e018fa15932`，正常分支 `dev/image-admission-reentry`。本批优先修复上批两项 P2 缺陷，暂停新增 usage 功能。实际核文件、干净工作树、单 worktree/唯一写入者、HEAD 与默认网络远端祖先；main 保持 `51d077481cf6303602fcf5d2718213da9cf05178`，独审只读，不恢复旧实例、不动未知锁或进程。

## 可复现问题及修复

1. `RuntimeGate` 安装新请求 B 后，同步取消旧请求 A。A 的非抛错取消回调若执行 Clear/Stop/Adopt/Dispose，旧实现仍给 B 写快照并调用 Results.Begin，可能恢复已清状态；回调若启动 C，外层 B 还会覆盖 C 的 active job 和结果。修复在取消回调返回后、写缓存或结果之前，在 canonical Gate 内再次证明当前命令修订、原 source/owner、settings/profile/binding、epoch、来源、启停/隐藏/挂起资格。仅通过后提交 Results.Begin、snapshot、active 和分类缓存变化。拒绝或抛错只结算实际 B identity；已有 C 不被全局停止或清空。Stop 即使没有 active job 也撤销待提交命令，模式修改也撤销旧接纳；普通无变更回调仍允许新的显式生成。独审再发现：回调仅修改模式/设置导致 B 拒绝时，被取消的 A 可仍显示生成中。追加红测后，拒绝路径仅在 active 与 Results 都仍属原 A、A 已不能通过 Gate 时清 active 并标 Stopped，保留 partial、原图和分类；C 的未停止状态独立断言。
2. 实际 `ImageTranslationPlanner.ClassificationPrompt` 使用的 `image-classify@1` 缺少 parser 所需 readability 枚举等合同。修正尚未发布的原生模板，明确五个精确必需字段、所有枚举和类型、重复/未知拒绝、regions/id/position/bbox/note 的边界及完整有效 JSON 示例。实际双协议 HTTP body 断言 system 合同，取实际发送的示例经同一 parser 校验并完成第二阶段；完整但非法 readability 仍只有一次 HTTP、无 final。没有放松 parser，也没有增加 JSON 修复循环或新模板版本/依赖。

## 实际运行与原件

| 运行 | 实际结果 | 说明 |
|---|---|---|
| r01-reentry-red | 10 项：2 通过、8 失败，exit 1 | 4 个 Clear/Stop/Adopt/Dispose 重入、2 个 C 后继隔离、2 个实际 wire 合同失败；2 个非法枚举 fail-closed 已通过 |
| r02-reentry-fixed | 135/135，0 失败/跳过 | 分类/实际编排/原图 owner 专项 |
| r03-provider | 1138/1138，0 失败/跳过 | 原 1118 测试名多重集合保留，新增 20 项 |
| r04-app | 0 警告、0 错误 | Release、no-restore、EnableWindowsTargeting 交叉构建 |
| r05-partial-red | 6 项：2 通过、4 失败，exit 1 | 双协议 partial A 在 mode/settings 拒绝 B 后未标停止；2 项 C 未停止已通过 |
| r06-partial-fixed | 139/139，0 失败/跳过 | 独审修后同一专项，含新增 4 项 partial |
| r07-provider-final | 1142/1142，0 失败/跳过 | 原 1118 测试名多重集合保留，最终新增 24 项 |
| r08-app-final | 0 警告、0 错误 | 最终生产源码的相同交叉构建 |

新增测试直接 Link 实际 App 组件，复用真实 Core Gate、Provider、PNG 与既有 Kestrel loopback fixture，检验实际 HTTP 数量和 body。还覆盖 settings、挂起往返导致 epoch 改变、模式、profile、来源、Lock、Hide、owner disposed，以及双协议无变更回调的成功路径；没有新验收工具或真实模型。所有输入均为合成公开数据。

保存 14 份 SDK log/TRX 与 5 份红测源码原字节，唯一命名并设置精确 `-text`。r01 红版 production/prompt 与父提交完全相同；红版 tests 含最初 10 项用例。最终 tests 增加另外 14 项，并移除示例定位 marker 的换行要求以兼容 LF/CRLF；r05 红版生产/测试另行保全，修后补强原分类引用保持断言。没有删除用例或弱化行为断言。父 105 份原始回执及 1685 份既有证据保持原字节；历史覆盖前字节仍 Unknown，没有重造。具体 hashes、计数与独审结论见[固定证据](../test-evidence/2026-10-16-image-admission-reentry.json)。

## 范围和剩余门槛

仅修改离线功能组件的接纳、一个实际提示模板及其回归。Core、Provider parser/serializer、正常 Shell/截图流程、其他七个模板、8 workflow、43 锁不变。250/600ms 与全部来源/密码/完整性/取消/清理门、生产离线复用 Off 保持。普通截图卡仍本地未翻译、未发送；无自动生产外发、usage 新功能、图片入口或依赖。

当前 Linux 无 WindowsDesktop/交互桌面；WPF 实际测量、排版、选择、阅读位置、焦点、Windows/COM 所有权与取消时限、真实截图/OCR/模型与视觉质量、完整 P2/P4/P6 仍 NotRun，P6-04/05 继续 InProgress/validation NotRun。本次交叉构建不能代替这些验收。没有 Windows/CMake 探针、兼容层、新 CI 或权限绕过。

每次 dotnet 前 export 且命令前显式 `DOTNET_GENERATE_ASPNET_CERTIFICATE=false`；旧证书存储/信任 Unknown，不读库/配置/trust/删除。Actions 不查询、不调度、不绕既有 Forbidden，远端运行数 Unknown。无 main 修改、强推、部署、新费用/凭据/权限/协议或真实模型调用。
