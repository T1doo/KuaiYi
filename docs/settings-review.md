# P1 配置与设置独立切片

基线为 `7c89618e99a683cdf8e40e3a6eb49ff865d1b360`。监督方只限定签收其可移植 OLE 生命周期；E06/P0 的原生、桌面门槛未退出。本轮按遇阻继续独立工作的授权实现 P1-03/P1-04 的配置部分，不将并行实现签成 P0 通过或 P1 整体完成。

SPEC 全文已在前轮阅读；本轮核对 SPEC 和原产品书的完整字节摘要均为 `ea3aa6529405bcc703986988fd089f476ee8f991d867e2220b3b20c439126166`，并复核第 8、12.2—12.3、16.3、A10、L01、L06。仓库此前无 Settings/SecretStore 实现，已有 RuntimeGate 的 Settings 暂停行为保留。现有 Provider 协议代码未重写或接入真实服务。

当前合法工具只有 Linux g++；无 CMake/MSVC/Windows SDK，原生桥要求 WIN32/MSVC，不能由当前工具编译。没有重复空编译探针、安装组件、接受协议、切换环境或操作连接桌面。

## 已交付的真实路径

- `AppSettings` 为不可变配置对象，包含 API 配置及秘密引用、默认文字/视觉配置、翻译偏好、互斥触发模式、浮窗、图片、隐私、热键建议和默认值。结构/数值/枚举 Schema 与默认 JSON 由相同定义和范围属性导出；引用关系、URI、隐私固定项和安全头等语义由 SettingsJson 验证。拒绝重复/未知字段、数字枚举、非有限数、非法范围及可序列化明文 API Key。没有 Activation/原文/图片/译文字段。
- 配置保存到用户运行软件时的 `%LOCALAPPDATA%\KuaiYi\config\settings.json`。草稿取消不改变生效配置或持久文件；成功保存后一次发布不可变快照，旧请求快照不被修改。文件和秘密操作在后台调度，UI 不同步等待 IO。
- 同目录 CreateNew 临时文件、Flush(true)、File.Replace/首次 File.Move，保留一份上版备份。共享文件锁和主文件/备份的字节摘要共同防止并发草稿覆盖；取消只可在原子提交前报告取消。提交后异常会核对持久 WriteId，不能把已提交说成回滚。遗留临时文件不自动应用。[File.Replace](https://learn.microsoft.com/en-us/dotnet/api/system.io.file.replace?view=net-10.0)、[FileStream.Flush](https://learn.microsoft.com/en-us/dotnet/api/system.io.filestream.flush?view=net-10.0)
- 损坏时明确选择备份或安全默认值；原坏文件及被替换的坏备份保留到独立 quarantine 文件。任一主/备份未来 schema 不支持则只读，未知旧格式也不倒退覆盖。schema0 仅指本轮明确建立的初始布局草稿格式，**不是宣称存在已发行旧版**；迁移仅在内存，保存后保留其原字节。schema1 另有无秘密的 `writeId`，用于恢复事务归属。
- 新秘密先以不可变随机引用加密落盘，再刷新引用事务，再提交配置；旧版未丢失前不删除旧引用。删除提交后先持久撤销标记再删除密文，备份不能恢复已撤销引用。持久 WriteId/基线摘要证明提交结果后才完成撤销或回收未发布新引用；不能证明时保留全部数据并阻止继续，避免猜测回滚。替换密钥仍保留备份所需旧密文；无法证明归属的孤立密文不会自动清理。
- 生产加密为 Win32 CryptProtectData/CryptUnprotectData，UI_FORBIDDEN、当前用户作用域；秘密目录以当前 SID 的受保护、可继承 DACL 创建/设置，密文与撤销文件继承。真实 DPAPI/ACL **未运行**。只在用户保存新密钥时加密，不自动读取旧密钥到设置表单。[CryptProtectData](https://learn.microsoft.com/en-us/windows/win32/api/dpapi/nf-dpapi-cryptprotectdata)、[CreateDirectoryW](https://learn.microsoft.com/en-us/windows/win32/api/fileapi/nf-fileapi-createdirectoryw)
- P0 App 的“设置”入口接到真实正常 WPF 窗口：七类导航、内部滚动、固定保存/取消、多 API 档、协议/地址/模型/认证、掩码新密钥及五秒显示、删除草稿、默认分工、风格、触发/浮窗/图片/排除应用。打开时暂停捕获，关闭不改变此前 Off/On；保存不授予上传权限。设置原操作未结束不能关闭其 owner。保存删除发出配置/秘密失效通知；当前 P0 本地实验保守停用，完整 API 任务按使用配置定向取消留在 P4。

普通保存不能修改未交付的热键注册事务或导入端点/复制授权。开机启动、完整热键录制、连接测试、能力测试、自定义提示词/头编辑、完整诊断导出和产品托盘尚未交付，界面明确显示范围。位置恢复已有使用当前拓扑/DPI、负原点、缺失显示器安全回退的纯函数；真实浮窗持久化和恢复接线仍未交付，P1-03 不整体签收。

## 验证与证据

新增 **45 个执行用例**，最终都在完整回归中通过：草稿取消及可控缓冲清零、原始快照保留、实际替换和备份、独立协调器冲突、实际共享锁取消、写入故障、已提交异常、未知迁移/未来主备文件保全、损坏和 null/数组恢复、非法秘密路径、真实密文/租约清零、提交后删除、备份不能复活、未证实事务保留、授权不能绕过、Off 下无旧秘密读取或发送、位置纯函数。

两例使用**实际独立 .NET 子进程**在临时文件刷新后或原子发布后阻塞，再仅终止所持 child，恢复整份配置与删除事务。另有明确的异常注入表示写满/中断边界；未在真实写满磁盘或硬件断电环境测试。加密文件测试使用明确的 AES-GCM 合成 protector 和临时目录 policy，**不能算 DPAPI、SID/DACL 或 WPF 实机证据**。所有测试内容为公开 SYNTHETIC 数据；没有真实凭据、模型调用或用户选区。

首批35、扩充40、随后42项通过；随后修复缺失API档ID随机生成的身份不稳定问题：ID必须显式存在，仅用户新增时生成，补1例后最终完整 **Core537 + Provider77 =614/614，Node63/63，无跳过**。25 个托管工程 locked Release 零警告/错误；身份源静态6/6。全部旧用例保留，非法配置断言进一步限定为预期异常类型。Schema 属性访问初次 CS1929 及 WPF 缺 System.IO 的构建错误日志保留，修复后构建通过。

机器可核对的源码摘要、日志、TRX、Node XML、Schema 和包证据见 [summary](test-evidence/2026-10-11-settings-summary.json)。在仓库根运行 `python3 docs/test-evidence/2026-10-11-settings-verify-evidence.py` 验证；旧摘要只用于其精确历史 commit，不能拿旧快照摘要验证当前已修改源码。

独立包 `artifacts/KuaiYi-P1-settings-managed-win-x64.zip`：770609 字节，SHA256 `4f0ea5213960b01a901d985e543a5c1a6d109f2ffbaacf033be294c380e24c46`。42 文件：39 运行/资产、2 定义示例和 manifest；五个实际 PE32+/x64 头及文件摘要核对。此前五个审阅包全部摘要不变。无 fixture、凭据、真实配置、receipt 或原生 DLL；框架依赖审阅件，**不是安装包**。

复跑 `build.ps1 -Scope Managed -Configuration Release`、`test.ps1 -Suite Offline -Configuration Release`、`test-identity-source.ps1`、`stage-settings-review.ps1`。定义导出可在构建后执行 `dotnet tools/KuaiYi.SettingsStoreFixture/bin/Release/net10.0/KuaiYi.SettingsStoreFixture.dll --schema` 或 `--defaults`，输出仅静态格式，测试驱动不进入 App 或分发包。

Windows 最小待测：实际 WPF 小工作区/跨 DPI、键盘导航/正常激活、掩码和保存中关闭；当前用户 DPAPI 及另一用户拒绝、目录/文件实际 ACL；真实磁盘满/写保护/Windows File.Replace 故障与进程终止；位置真实 HWND 接线。E06 的跨 HWND-owner STA 注册、来源捕获/Off、COM Release、低层钩子时限、关闭及多屏仍 NotRun。P0 Blocked、P1 InProgress，106 整体验收全 NotRun；正常开发分支提交供监督独审，未部署、发布或推进整体阶段签收。
