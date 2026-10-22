# 宿主注册与精确撤销事务内核

从 `07a3b4498f0e3d7cb2130fdec4c408c906ba7365` 在原环境唯一写入者继续SPEC13.3/13.4、P7-04/05。复用既有InstallationStore版本完整性/role admission、IntegrationInstalledFiles清单与角色校验、SettingsStore受界文档/刷盘创建；不重造退出、升级、会话或图片流程。本片实现内部注册/撤销事务与Windows适配器源码，**没有连接安装器、设置、CLI或浏览器入口**。

现有NativeHost/Program由浏览器直接启动BrowserSession，尚未纳入InstallationStore admission及原controller role。因此既有协调器只能证明自己实际Start的角色，不能证明所有浏览器宿主已停。本片不得被调用方当作全静止证明或安装许可；真实注册接线必须先闭合这个缺口。

## 范围与状态

事务固定两个物理HKCU浏览器宿主默认值，分别Chrome/Edge的 `com.kuaiyi.native_host`，不把WOW64两视图当四个独立位置。Windows适配器只用Registry32访问这两个固定键，读/写默认REG_SZ；key缺失、空默认值及其他值/子键分开观测。未知默认值、其他内容、同路径但无归属receipt都拒绝，不领养、不递归删key，不改HKLM、策略、ACL或浏览器权限。撤销只移除已确认自有默认值，保留空key。

正常首次注册要求当前完整generation、manifest/trust及全部角色校验成功，两个槽均无默认值；成功后以CreateNew持久化严格canonical ownership receipt。仅凭路径相同不能撤销；撤销必须原receipt与当前generation/catalog/manifest完全一致。已注册/已撤销可无写入返回。暂不支持把旧generation注册换为新版、损坏payload的卸载、来源不明record恢复或任意路径输入；这仍是后续统一安装事务的内部基元，不是完整安装/升级/卸载。

写前落受界 `host-registration-pending.json`，记录operation、原归属与两个原槽快照；原InstallationStore.EnterAdmission新增对此pending文件或目录的拒绝门。每次前向或补偿写入前复核原receipt、selected、pending及持有的admission；补偿不用已取消的用户token。逐步读/check/write/复核是合作式协议，**不是注册表原子CAS**；只承诺发现冲突时不再改写该外来值，不承诺抵御同账户恶意竞态。

两个槽复核成功后，注册以新receipt持久化为提交点；撤销以精确receipt归档为提交点。此前失败可在原归属仍成立时补偿；此后失败不恢复成与receipt矛盾的状态。pending最终归档晚于原admission成功关闭，期间原pending门继续拒绝新受控角色。未确认状态保留原件并报Unconfirmed，不伪成功、不自动重试、冷回退或清未知记录。

所有同步registry调用由原Task在后台执行；取消不放弃该调用。Windows适配器保留RegOpenKeyEx/RegCreateKeyEx返回的精确raw owner，用非owning CLR wrapper复用受界读取；最终检查RegCloseKey返回码，不能拿CLR IsClosed替代实际关闭结果。关闭失败保留原句柄、事务对象与原admission；初始Read关闭失败也落恢复marker。显式Finalize先检查精确原Run Task已结算，只重试同owner，复用正在执行的清理Task并保全历史失败；它不擦pending、不重新注册、不将原失败转成功。原操作/补偿/关闭多错均保全。

仅新增Core对同产品NativeHost的内部友元可见性以复用已有存储；没有OS权限、安全配置或新依赖改变。LaunchAtSignIn仍false，PackageHold、生产复用Off、来源/密码/完整性/取消/清理/逐次发送门及250/600ms不变。

## 实际定向验证

原件 `artifacts/2026-10-22-host-registration/` 保留命令、完整日志/TRX、失败XML、预执行源码、输入及DLL摘要。轮次不相加。

- r01 NativeHost编译失败5个CA1416；将Windows lambda移到受平台属性保护的私有方法，不关闭分析器。零测试，不算产品协议红绿。
- r02初版新事务29P/0F，仅中间候选。
- r03独审反例4F/0P：receipt或current换成另一有效generation后，补偿Writes期望1实际2；原Read/Write句柄关闭未确认两例，期望Unconfirmed实际普通IOException。没有时间超时假红。
- r04再次编译时CA1416涉及类型级Windows属性；改为构造器/Close方法属性。未产生TRX、零测试，runner原metadata的Unknown_NoTRX保留，不称运行通过。
- r05清理API并发/重试两反例0P2F：原Run未结束时Finalize未拒绝；首次关闭暂时失败后第二次返回同fault。原件保留。
- r06修复新事务35P/0F/0skip，875输入无漂移；覆盖两个物理槽/视图别名、持久归属/幂等、foreign/未知内容、原生命周期门、部分写入/写后异常/补偿冲突、原错+清理错、取消持有原写入、提交后失败、metadata变更、初始Read及Write原close未确认、精确Run完成门和显式同owner重试。测试fixture构建完整合成installed tree及current/catalog/manifest/trust，**不冒称InstallationStore.Apply已经能生成路径派生metadata**；未执行产品EXE或真实注册表。

四项设计/实现P2经独立只读审查闭合，r06原件限定ACK。冻结后仅安装/角色准入/既有集成读取相关回归与正常App交叉构建，不重复完整Core/Provider/Codec或真实Windows。

## 原始依据与未验

Windows HKCU软件键视图共享，子键继承父键行为，故不使用四独立槽模型：[WOW64共享键](https://learn.microsoft.com/en-us/windows/win32/winprog64/shared-registry-keys)。写API无expected-old参数：[RegSetValueEx](https://learn.microsoft.com/en-us/windows/win32/api/winreg/nf-winreg-regsetvalueexw)。关闭结果以返回码判断，关闭本身不等于磁盘耐久：[RegCloseKey](https://learn.microsoft.com/en-us/windows/win32/api/winreg/nf-winreg-regclosekey)。宿主发现与扩展是独立安装状态：[Chrome](https://developer.chrome.com/docs/extensions/develop/concepts/native-messaging)、[Edge](https://learn.microsoft.com/en-us/microsoft-edge/extensions/developer-guide/native-messaging)。

真实Windows注册、RegCloseKey/CLR wrapper互操作、浏览器发现、全宿主静止、断电耐久及同账户对抗仍未验。完整payload/派生metadata、身份注册和撤销、稳定启动器、可选登录启动、Inno安装器、设置/密钥删除选择、真实安装升级卸载与K组仍有未完成项。本片没有安装工具、接受新SDK/工具许可、操作用户电脑、请求新权限/凭据、调用真实模型或重探403 runner。真实WPF像素/DPI/focus/highcontrast、质量和250/600ms仍NotRun；完整P7未签收。
