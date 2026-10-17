# SPEC14.1：首次启用前展示冻结的模型

从父有限准入的 `a87673a2562d40f1ae54ca3870ee83642b4e168a` 正常建立 `dev/enable-model-disclosure`；核实际本地/默认远端、clean、单 worktree 与唯一 root 写入者，既有 high_water_review 独立只读审查。

按父建议先核 P7-04/P7-05：`package-identity.ps1` 仅生成开发身份包且 Release 明确拒绝；`register-dev-identity.ps1` 只允许显式注册并拒替换已有身份；`stage-text-alpha.ps1` 产生 GUID bundle/ZIP，清单明确 `Installer=false`。仓库无 `.iss`/`.wxs` 或安装升级事务消费入口。未执行这些脚本、安装/注册/签名，也未添加孤立回滚工具。本片推进此前已定位、父明确允许继续的真实启用说明缺项；P7 状态不改。

## 产品路径与修改

SPEC14.1 要求首次启用展示实际 origin、当前模型、触发模式和所选内容将发送的说明。原主按钮/托盘/快捷键共用 `ShellWindow.Toggle`，确认框只插入 origin 与模式；`EnableIntent` 也未冻结模型。普通未超软上限的文字随后可直接发送，不保证另有模型说明框。

生产只改两个文件：EnableIntent 从同一保存配置/profile 增加不可变 Model，提供包含 origin、完整模型、模式及对应 Selection/Drop 发送说明的 ConfirmationText；实际 MessageBox 正文直接使用该值。保留旧五参数转发构造及参数名，旧内部调用可保持编译；生产 TryPrepareEnable 始终显式使用第六参数 profile.Model。没有改变 origin 授权粒度，已同意 origin 的本会话再次启用仍可略过说明；同源仅修改模型不会新增授权要求。

`ConfirmEnable` 与 `RuntimeGate.TryEnableShell` 原字节不改，settingsVersion/epoch/binding/暂停/生命周期门保持。保存模型后旧确认失效；文字快照仍显示原模型，新准备才显示新模型。草稿未保存不影响已准备说明。隐藏/锁屏/退出/拒绝不会借说明授予授权，换 origin/HTTPS→HTTP 仍需各自同意。Model 已由既有设置校验限制 ≤256 字符且禁控制字符，本片不改变校验。确认框为纯文本，模型中的 `<&>` 不做 HTML 解释；没有输出密钥引用、请求头、路径或配置名称，也不解密秘密。

## 实际红绿与回归

- r01 四项失败来自新增夹具在 Seed 前构造 Shell，保存事件触发已有初始化保护；是夹具错误，不算产品红测。保留精确源码、log/TRX、执行 DLL 摘要。
- 修正初始化顺序后，r02 真实 SettingsStore/SettingsCoordinator/ShellLifecycle 在父生产源码原字节下执行四个 mode/protocol 组合，4F，均缺 EnableIntent.Model。反射仅为让相同测试源能针对父 Core 编译并执行；这是实际冻结数据契约红测，未执行 WPF 确认框。
- r03 因新增必填字段破坏旧 HotkeyLifecycleTests 五参数调用而 CS7036，0 执行、无 TRX，不算绿色。保留日志与修订源码，未修改旧测试。
- 恢复兼容构造后 r04 同测试文件 SHA、同四个名称 4P；r02/r04 测试 DLL 摘要不同，不能称同二进制。最终补模型编辑/陈旧确认/同源及换源/秘密不读/重启/长度场景，r05 专项16P。

最终完整 Core1688P=父1672全部结果多重集+新16；Provider2157P原结果多重集保持；App/ImageWorker Release --no-restore 交叉构建各0警告/0错误。精确命令、源码/DLL绑定、失败与通过原件见[固定证据](../test-evidence/2026-10-17-enable-model-disclosure.json)。四种组合不调用 Provider/真实模型，所有样本均公开合成数据；测试目录由当前夹具独占。

独立源码审查已核兼容修复、同源授权范围和16例，未发现P1/P2；封存证据终审回执另附。既有测试、所有旧证据、SPEC/原产品书/AGENTS、项目依赖/锁/工作流/提示词原字节保持；追踪仅给 P1-05/L02 追加本子项证据与说明，全部实现/验证状态不变。

真实 WPF 模型文字可见性、256字符排版、键盘与对话框交互仍 NotRun；便携字符串断言和交叉构建不代表这些通过。Windows/安装/模型/物理250/600ms与完整SPEC仍未验。前片 a876 的独审核94份原件后新增审计回执1，最终95份；本片不改其任何原件或范围。本片未新增日志/诊断/通用状态组件，默认Off、生产图片复用Off、来源/密码/身份/完整性/取消/清理门不变，旧图片间歇根因仍Unknown。

仅使用已有SDK，所有dotnet激活脚本前后显式禁ASP.NET证书生成，全部--no-restore，无新依赖/许可接受/环境安装。Skia4.153.1许可、403、Actions拒绝不查不绕；无main/强推/部署/费用/凭据/安全权限或真实模型，未知锁/数据/日志/进程保全。
