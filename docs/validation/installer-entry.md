# 独立安装入口与首次根适配

接续产品6d04d197683f8841c906c5d33fed0e1800fe51ed/证据eaf6ed1fcfdd78a21958694927b2799992bdeee5。先核同环境单writer、完整HEAD/远端/祖先/clean；不改main。独审先收敛[合同](../design/installer-entry-contract.md)，再审候选源码；没有新增恢复内核。

## 实际产品差异

- 独立 `KuaiYi.Installer --install`，固定入口相对包路径、真实LocalApplicationData下专属文件/数据根，无CLI路径/pin/恢复或环境override。Installer单独在payload封存后构建，不属于其所pin的G/B，避免循环摘要。
- 可信pins仅编入资源；当前真实 `packaging/installer-source-pins.json` 明确Unavailable。生产在根创建、平台registry owner和controller之前拒绝。完整Windows/native/runtime和经审定的两个源摘要缺失，不将结构fixture签成可运行包。
- 可选首次根初始化在原Scope任务执行前持有owner/IO，native排他mkdir、固定owner/空锁/pending、原读取Flush/Close→原子Move。只有同原内存owner借setup，原Ticket完整后交接标记；部分写、关闭或Move未知保partial/原Ticket，普通入场拒绝，existing-only默认不变，不领养空或foreign根。
- 入口只Start同一个原Operation。七状态分列，阶段READY不表示进程已退出。原Session producer封闭后join原角色协议/startup/observer/stop/exitWait；actual child未退出只被动观察，无法确认就保原Task不返回。不重试Stop/Finalize/补偿，不把原Unconfirmed/清理false提升。
- Ctrl+C抑制默认终止，缓存同一原CancelAsync任务；封闭producer后仍抑制再次Ctrl+C，join取消原错后才解除handler，未知CTS不Dispose。状态输出错不改写原READY，仍join原会话再返回非零。
- 原peer门保持。Installer不在所选generation内，因此本健康会话不启warm capability；不改expected image或用稳定Launcher伪造部署授权。此范围非完整P7使用链。

## 候选原件与必要反例

原件根 `artifacts/2026-10-22-installer-entry`。所有轮次独立，不相加。

|轮次|真实范围与结果|
|---|---|
|r01|测试var多声明CS0819，0用例、无TRX，非产品红|
|r02|held IO用例在IoEntered超时，0P1F；保原件，不倒推历史唯一根因|
|r03|7普通managed构建0W/E，source revision对齐，无锁漂移|
|r04|first-root断言NotCommitted、held IO未到屏障，0P2F|
|r05|单first-root诊断确认Bootstrap严格拒绝新Microsoft.Win32.Primitives依赖；移除新Win32Exception，未放宽bootstrap依赖门|
|r06|仅Launcher/Core对齐构建0W/E|
|r07|first-root1P；held IO后清理断言错误地预期退出0，实际137而失败；不记作fence红|
|r08|有效fence反例1F：inline report调度屏障后原IO仍held，Entry.OriginalWork却已完成；原TRX和源码保全|
|r09|fence泛型Task返回类型编译失败，0用例、无TRX|
|r10|修后Launcher/Core对齐0W/E|
|r11|新增16P/0F/0skip，956输入无漂移；含r08同方法字节一致绿、首次根/现有根/empty拒绝、pins形状与不匹配、写关闭双错、目录Move及handoff各before/after、Ticket partial、held原读、输出错、held协议、lateStart实际child、取消callback held/throw|
|r12|独立Installer真实普通managed交叉构建0W/E；含保持handler直到取消原任务结算的最终Program调整|

r11不借记最后测试改读实际pins文件的变化；随后固定树一次相关签收见下节；不重复266/204/完整Core。新增Core进程退出fence没有更改既有Stop/Finalize实现；原22会话测试方法正文应逐字节保留。

## 阶段边界

仅隔离文件根/fake registry/实际原Start持有的中性合成stdio子进程，资源/来源/取消/清理/发送门不放宽，依赖锁版本不新增外部包。Installer仅新增项目引用，无新SDK许可/凭据/权限/模型费用。

未执行Windows安装器、真实注册/身份包/Shell/自启动/卸载；真实Windows/medium/Explorer/浏览器/WPF/OLE/OCR/画质250/600ms、完整native/runtime与P7未验。当前生产入口不可因内部ACK/结构fixture而开放；最小前置为完整可信源包、审定内嵌G/B摘要及合法隔离Windows验收环境，不操作用户电脑或改安全设置。

## 固定树签收与原件独审

固定产品 `d55be5563a6cde314975b58356b0fd7cb95c9524`，r13八项普通managed构建均0W/E；r14一次 **58P/0F/0skip**＝新增16＋原Operation15＋原Scope25＋Host门代表2（最后两条class为InstallationSessionTests）。新16与候选multiset、原15与前19的Operation子集完全相同，未相加历史轮次。r08原1F与r14对应绿的方法字节一致；旧22方法suffix和其余既有test文件保持原字节。

956输入的建前/建后/测前/测后四快照一致且与Git匹配；18项AssemblyInfo含完整产品SHA，15产品DLL建测库存相符，Launcher/Installer/Core.Tests/SettingsStoreFixture/BrowserHostFixture五份Core与实际Core一致，Core SHA256 `ea7c9eb54c65f6173a880bc8ef1f1a132da0181badeb1928e8dad9ace36caf08`。源码、项目及锁的差异限定23文件；新增Installer锁仅Project引用，无新外部依赖。原工具/扩展/协议权限、来源密码完整性/清理发送/250600合同未改。

原件 `installer-entry-originals.zip`，2,453,373B，389成员/388manifest，SHA256 `ec60502473b264c437b8fe51e81664c86f5ce3b5fd4f8c7024832a052b02fbc3`。Library **`libfile_9f283f3eb7b88191b050095bf97037bc` v0**，本地身份确认，未分享私有下载URL。ZIP保留冻结产品时的候选报告，未为证据更新重新打包；没有DLL本体，不宣称归档内二进制独立重hash。

独审限定ACK：CRC/全部388项覆盖大小SHA与安全路径、23变更文件＋SPEC/AGENTS共25份Git原件、raw58分组、同fence方法红绿、956四快照/18版本/15产品DLL摘要/5Core以及八0W/E日志均实核；reviewer未重跑产品。之后只更新本报告/证据JSON/progress三文件，产品、测试和合同不变。

原58的相关集合不是完整Core；旧266/204全量未重跑，其未执行项不借记，不回填旧红门唯一历史原因。不扩签Windows/medium/Explorer/浏览器/WPF/OLE/OCR/完整native或runtime/画质性能250600、完整安装注册/卸载/P7。生产pins仍Unavailable，真实安装命令未运行，真实registry零改动；缺前置不是权限拒绝，也没有待审批操作。
