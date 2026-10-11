# 设置的 profile 绑定失效与定向取消

基线 `99146204a0259ac9d106da75a2ba80d2d96fc49d` 的共享密文保护没有处理另一层失效：A/B 共用密文，清除 A 后物理退休为空、配置也未删除，旧事件 HasDeletion 为 false，A 原绑定任务继续。删除 A 时旧 App 又全局 Disable，影响仍可用的 B。本轮只修绑定失效、生产设置到运行时的接线及同类提交恢复通知，不推进其他功能。

`SettingsChange.RevokedProfileBindings` 明确列出被删除或秘密引用变化的 profile ID 与旧秘密引用，分别保留 DeletedProfiles 和物理 DeletedSecrets。清除/删除/更换 A 的引用发出绑定失效，即使共享 B 的密文不退休。普通模型、风格、默认分工改变不撤销原请求快照。物理秘密依然按所有当前引用判定最后退休，不以吊销共享密文替代任务取消。

RuntimeGate 提供当前 profile/ref 的内存不透明 BindingId 和 TryStartProfileRequest，在同一身份锁内核对当前绑定及 On/会话/触发资格；拒绝空、伪造、其他 gate、已撤销及纯本地许可下的 API 绑定。引用改变/删除/重新创建产生新身份；当前绑定失效时仅取消相应 active request，阻止其 CanApply/CanSend/TryApply/TryBeginSend/展开及旧绑定重启。原来的捕获 epoch 同步失效，B 的 active identity、绑定与 On 保留。GUID 仅存在内存，配置不保存 On 或请求内容，映射最多保存当前配置的16个 profile。

实际 App SettingsFlow 创建/初始化/关闭生产 SettingsRuntimeLink，订阅同一 Committed 事件，转到定向 Gate.ApplyCommittedSettings；不再用 HasDeletion 全局 Disable。本地 OCR 操作没有 API 绑定，继续保持纯本地和无网络资格。正式 API 任务必须从 TryStartProfileRequest 获取请求身份；**完整 P4 设置转协议配置、API 调度与正式模型 UI 尚未交付**。本轮证明生产事件/门控/原异步取消链及编译的 App 接线，不声称已有完整翻译界面或真实 Windows 图形证据。

新秘密 journal 为 schema2，持久化无秘密字节的删除/旧绑定通知。配置实际提交后，所有订阅者逐个收到通知；一个订阅者异常不跳过其余 runtime 订阅者，异常仍返回，journal 保留，已提交不报告回滚。按 WriteId 证明提交的恢复再次发布同样的绑定通知，再完成物理撤销。重复通知对已失效绑定保持安全。schema1仍可读，有先前 Effective 快照时推导变更；旧格式缺少元信息时不伪造历史绑定。未知未来 journal 保全、拒绝猜测，不通知或提前退休。

14例基线 **8失败/6通过**，原源码及 TRX 保留。初修14例11通过/3失败，是新测试误把不可变数组容器相等当作通知内容相等；修为逐项内容比较，双通知数量、profile/ref、A取消与B存活断言保留。另补纯本地许可 API 准入1例先失败再修复，原日志保留。初扩充 **20/20**；迟到初始化再补1例先失败后修复，最终 **21/21**，完整 **Core610+Provider77=687/686，Node63/63**，无跳过；25托管工程 locked Release 零警告/错误，旧用例与断言保留。

21例覆盖共享 A 清除/删除/替换时 A 实际 token/旧结果/发送失效与 B 继续可用、最后退休规则的旧52例保留、三种提交前失败不通知/不取消、订阅者故障及恢复通知一致、两例实际独立 child 在提交前/后终止、旧schema1共享日志、未来journal保全、Off不读取/不启用、无密钥配置删除、同ID/引用重新建立不能复用旧BindingId、纯本地OCR不受API删除影响。另有通过生产 TryLinkRequestCancellation 的真实原异步等待：A终结为取消，B保持未完成，直到自己的 Stop 才终结。测试两独立 RuntimeGate 各保持单个 active request，不冒称同一 gate 可并发两条主请求。

实际文件、锁、原子提交、journal、事件、runtime link/gate、owned child 和取消任务为真实可移植实现。AES-GCM protector/目录 policy 为明确合成；受控异常不是物理磁盘满。没有真实凭据或模型调用。Windows WPF/DPAPI/ACL、正式 API UI/D08组合、E06及原生仍 NotRun，P0 Blocked/P1 InProgress，106整体验收全部 NotRun。

证据见 [summary](test-evidence/2026-10-11-settings-bindings-summary.json)，根目录运行 `python3 docs/test-evidence/2026-10-11-settings-bindings-verify-evidence.py`；991/0db/7c快照按对应 Git commit 验证，不能对当前变更源使用旧摘要。

独立 `artifacts/KuaiYi-P1-settings-bindings-managed-win-x64.zip`：777721字节，SHA256 `c417b7980eb810448429edfadc5d795b916e8e8fbec18625dcb06feaec411bf6`；42文件、五个实际 PE32+/x64 宿主和 manifest 摘要核对。此前七包摘要不变。框架依赖、无原生 DLL/fixture/真实配置/凭据/receipt，**不是安装包**。实现者自查完成，监督独审待进行，不签收整个阶段或 D08。

最后复核发现旧加载初始化可能晚于新提交到达；1例先失败后修复。SettingsRuntimeLink在Gate最终身份锁内核对Coordinator的当前版本和自身disposed准入，旧初始化不恢复旧秘密引用，也不取消新绑定任务。原20例/686全回归和初包元数据另作pre-stale-init历史证据；最终21新例/687全回归及新包摘要重新冻结。
