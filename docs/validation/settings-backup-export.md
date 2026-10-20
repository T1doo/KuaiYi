# 已保存普通配置的预览与导出

基线 `31e0abba70a7a9db3ee5bc6def4b8ec1ae9a6870`；开发分支 `dev/settings-backup-export`，远端 `https://github.com/T1doo/KuaiYi.git`。开始时实际核 Git/远端 HEAD 一致、工作区干净、单 worktree，没有其他运行写入任务；上一窗口来源切片的独审记录与推送提交相符。main 仍为 `51d077481cf6303602fcf5d2718213da9cf05178`。

## 原条目与本次产品行为

原 SPEC §8.3 要求普通配置导出剔除密钥及敏感头。原设置页仅有独立四字段显示偏好转移，尚无普通配置导出。本次完成 P1-03/P4-01 配置模块的**导出子项**：设置“常规”提供“预览并导出已保存普通配置”，先展示完整 JSON，再由用户选择新的本地文件。此为配置备份，不是整体验收或已实现完整导入/恢复。

从 `coordinator.Effective` 读取当前有效已保存快照；拒绝没有已保存内容、未知/只读或待恢复状态。预览不调用 CaptureProfile，不读 PasswordBox、待保存草稿或 SecretStore。私有 UTF-8 字节与完整只读预览来自同一次投影，后续草稿、配置变化不改变此份已标明冻结的预览。保留 API 地址/协议/模型/参数、风格模板、普通偏好和快捷键组合；所有 SecretRef 值、自定义 Headers、Privacy 中应用规则/授权和端点同意、位置/显示器标识及 WriteId 均剔除。原对象不改，导出不会保存草稿、修改设置、启用翻译或发送请求。

全部自定义头都会省略，包含非敏感头和 `anthropic-version` 等覆盖；预览说明明确提示补回必要头和安全规则。HTTP 风险开关保留为传输偏好，界面不将其称作端点授权。完整配置导入尚未提供；四字段导入仍只接受原专用白名单文档。

## 所有权与文件提交

新导出 session 保留唯一原文件任务及 CancelAsync 任务直到结算。关闭/撤销立即取消发布资格，但不提前清 Busy 或释放原 owner；生产 UI 命令先记录 task 再进入 IO，父设置窗与子预览窗均等待同一原 task 后关闭，原保存/API 测试 Closing 守卫保持。已成功发布后取消不报告回滚。

复用原 `DisplayPreferencesFiles` 的本地路径/重解析检查、已打开句柄最终路径、随机 CreateNew 暂存及不覆盖的 File.Move；仅抽 internal 文档验证通路。原公开四字段 API 继续调用原 Document.Read，4096 字节白名单不变。普通配置使用原 SettingsJson 的 256KiB/16层和字段校验；文件通路在写入前再次拒绝不脱敏配置。无法确认归属的暂存不冒险删除；路径检查与 Move/Delete 仍非防御恶意并发换名的原子保证。Windows 句柄/对话框实测另列 NotRun。

## 实际验证与独审

新 `SettingsBackupTests` **23P/0F/0skip**，并以原3项 DisplayPreferencesDraftTests 运行最终 **26P**。包括真实文件精确字节、已保存来源不变、草稿新密钥不入备份、未知/坏配置、已有目标不覆盖、链接目录拒绝、未脱敏内容拒绝、外国预览/已取消零 IO、重复导出、忽略取消的原 IO、原 CancelAsync 回调持有/异常、发布后取消及清理失败。链接生产 SettingsBackupCommands，用明确控件端口验证真实文件状态、相同原 task 的关闭结算，以及显式合成 UI 队列中的发送前撤销。真实 WPF 事件没有执行。

最小设计及最终源码均独审。独审发现一个候选 P2：真实 File.Move 已成功，Dispose 及原取消回调同时失败时，第二次错误合并会把 PublishedCleanupUnconfirmed 降为 Failed。新增同方法/同字节组合反例实际 **1F→1P**；现独立保存 published 事实，检查实际文件仍与预览相同、状态明确已发布且清理未确认、两个原异常均保留。独立审查再次 `--no-build --no-restore` 跑新23项全部通过，无剩余 P1/P2。

共享文件通路的原四字段 Provider 回归 **110P**；原断言和测试源码不改。该运行在最后组合 P2 修复之前，随后只改新 SettingsBackupSession 的 published 状态处理、原四字段通路/旧测试字节保持，不把它称为最终 Core DLL 的全套重测。最终 App 的8项目 Release 交叉构建 **0警告/0错误**。没有重跑整个 .NET 套件或上一浏览器91项；此切片不改变浏览器、取词、OCR、模型协议或原超时门。

Core/Provider/App 使用已有获批隔离 SDK402/host13 与未改 launcher；locked restore 的 audit=true/all/low、warnings as errors 均成功，锁文件不变，无新依赖或协议接受。首次 App restore 因 Linux 未给 EnableWindowsTargeting 报 NETSDK1100；补原交叉编译属性后成功，不改变系统安全权限。

初轮19项4P15F来自新测试使用了非法秘密引用；次轮17P2F是新测试对既有验证器/已释放对象异常类型的假设错误。两轮日志、TRX和当时测试源码保留，不当成旧产品红测。中间25P、组合反例1F及最终26P另存；最终完整测试文件与组合红测逐字节相同。详见[证据索引](../test-evidence/2026-10-20-settings-backup-export.json)。

最终命令：

```sh
python3 -B scripts/session-sdk.py test tests/KuaiYi.Core.Tests/KuaiYi.Core.Tests.csproj --no-restore -c Release -nodeReuse:false -p:UseSharedCompilation=false --filter 'FullyQualifiedName~SettingsBackupTests|FullyQualifiedName~DisplayPreferencesDraftTests' --logger 'trx;LogFileName=backup-final.trx' --results-directory artifacts/settings-backup-export
python3 -B scripts/session-sdk.py build src/KuaiYi.App/KuaiYi.App.csproj --no-restore -c Release -p:EnableWindowsTargeting=true -nodeReuse:false -p:UseSharedCompilation=false
```

## 未完成条件

此为 §8.3 普通配置导出子项源码/便携验证，不提升 L01/L06 或106条整体验收；完整配置导入仍可独立推进。真实 Windows 普通用户桌面还需核设置/预览可访问性、模态文件选择、父子关闭/退出、实际本地句柄和文件系统竞争。生产安装、精确 extension ID/同用户同 session 接收端、真实 UIA/OLE/OCR/250/600ms、P6/P7、模型质量、签名升级卸载仍另验，Package Hold 保持。

旧 CI39347646426 第3步内部首因 Unknown，被拒日志未查询、重试或换路；没有新CI、浏览器启动、模型调用、费用、凭据、安全权限变更、发布或用户电脑操作。main未改，生产图片复用 Off，来源/密码/完整性/取消/清理/隔离门保持。
