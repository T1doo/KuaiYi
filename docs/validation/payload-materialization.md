# 源包到随机安装 generation 的实际物化

从5bb2c5f926d2ea0a06eac3894d91679b35a36998在原指定环境唯一写入者继续SPEC13.3/13.4、P7-04/05。先设计并独立只读审查，再实现InstallationStore.Apply实际物化及两个消费方的完整性校验；不接稳定启动器或统一安装/注册入口，不执行真实注册、身份注册、安装或系统配置更改。

## 实际生产闭环

新增明确的MaterializedDevelopmentPayload源catalog v2，固定七角色App、NativeHost、CaptureBroker、ClipboardHost、CaptureHost、OleReceiver、ImageWorker，以及共同托管库、原生组件、runtime/Skia、身份清单及资源、浏览器扩展分发文件和许可证路径。检查正常产品完整源码revision、x64 PE结构、声明依赖闭包和精确自包含runtimeconfig；拒绝acceptance-only App、framework-dependent、缺角色/依赖、混revision、越界声明、身份Id/EXE错配及扩大扩展权限。源定义显式给出1—2个精确extension origin，不从未固定key的扩展manifest猜ID。

原source catalog摘要在解析前由调用者expected SHA固定；复制逐文件按原摘要复核，源变动不重算领养。在原mutation admission/锁/独立staging下，生成最终随机versions/GuidN对应的manifest路径、trust和provenance，保留source-package.json原字节，再生成覆盖源文件和四份metadata的installed catalog v3。原文件数/metadata/单文件/总量界限也应用于派生结果。staging完整复核后才发布目录和原current指针；失败保留staging/已发布候选，沿原精确current回滚和取消路径。

source digest与installed digest明确分开：升级pending仍绑定原source digest，InstalledVersion、current和启动证明绑定包含最终路径的v3摘要。ReadActive先检查expected installed摘要，再验证原source、闭包、精确派生metadata和canonical catalog；重算攻击后的catalog不能掩盖更改。generic v1保留原纯文件事务用途，不能获得Materialized资格；v3不能作为新source再次Apply。

NativeHost真实LoadConfiguredWindows保留原注册表和OS身份/session门，共享ReadConfiguration承接物化与进程路径校验；Program实际调用链不改。内部注册事务在读注册表前要求当前generation为已物化v3，并复核selected摘要。原35注册测试的正常fixture现从source经真实Apply得到随机generation，不再手造installed目录。合成测试只调用共享纯配置校验、提供synthetic expected process path和registry store，**不是实际Program/Windows身份或注册成功**。

## 实际证据与红绿

原件位于artifacts/2026-10-22-payload-materialization，保留完整命令/日志/TRX、所有失败XML、candidate源码/patch、逐输入与DLL摘要。各轮不相加。

- r01 NativeHost正常构建0W/0E；零用例。
- r02准备真实普通managed角色产物，原CaptureBroker/ClipboardHost/CaptureHost缺assets时按原锁restore，其后六个正常项目顺序构建成功；锁未漂移，无新依赖。
- r03新Registry fixture漏实现两个接口，CS0535编译失败，零测试。
- r04 xUnit2030分析器拒绝新测试的NotEmpty过滤断言，编译失败零测试；raw runner保留Unknown_NoTRX，不能计通过。
- r05身份反例30条26P4F：四个错误worker-id/duplicate-id/name/publisher均未抛异常。修复固定开发身份Name/Publisher、唯一Identity/Applications及两项精确Id→EXE映射；红绿测试源码不变，不是超时或放宽期望。
- r06相关65P0F0skip（新30+旧注册35），r07边界16P0F0skip；883输入各轮稳定，仍是dirty候选。额外边界覆盖malformed deps/PE/DTD、派生metadata计入文件上限、同source两次物化路径摘要各异、有效其他origin仍不能替换caller固定source。

源码与原件获独立只读限定ACK，无新增确定P1/P2。冻结后仅一次安装/注册/原共享接收相关回归和正常App交叉构建作为最终签收，不重复完整Core/Provider/Codec。

## 合成输入与未验边界

正向测试输入来自实际普通构建的本产品DLL/apphosts/deps及仓库分发资源；本环境缺完整Windows自包含运行时和三项产品原生DLL，因此未提供的runtime/native/第三方依赖用中性structural PE fixture填充，runtimeconfig也是明确fixture。它们只能验证有界结构与物化逻辑，**不执行，不是完整可运行Windows源包**。测试公开样本仅保存真实Apply生成的五份catalog/metadata及scope标识，不保存用户数据或宣称fixture可运行。健康回滚是合成callback，非Windows child健康验收。

当前缺少KuaiYi.CaptureNative.dll、KuaiYi.OleNativeBridge.dll、KuaiYi.ShellExtension.dll真实Windows构建及完整Windows自包含runtime输入；本环境无现成Windows native工具链，没有安装新工具/接受新许可，也未重试403 runner。固定角色/声明依赖/PE结构并不证明运行时可加载、微软或Skia发布者真实性、签名/OS身份。源SHA是调用者固定原字节的完整性，不是发布签名。

身份模板/资源不等于MSIX生成、签名或注册。稳定受控启动器、统一安装/注册入口、Inno、可选登录启动、真实干净虚拟机安装/升级/卸载仍未完成。PackageHold/生产复用Off、来源/密码/完整性/取消/实际退出/清理/发送门及250/600ms保持；未触用户电脑、权限、凭据、费用或模型。真实Windows/browser/WPF/medium/Explorer、像素/DPI/focus/highcontrast、OCR/画质/250/600ms未验，完整P7未签收。


## 第一次固定相关回归与精确合同迁移

固定1c2225ca84f2d2ea78b709f1ab3aba856fe37be1的r08六个正常构建命令全部0W/0E，覆盖七产品角色；883输入稳定/锁无漂移。r09一次相关585结果为584P1F，唯一失败InstalledRoleFilesTests.EntireManagedRoleMustMatchReceipt(valid)第23行：旧固定计数Expected23/Actual47。完整原TRX/失败XML保留，非生产异常或超时，不签收此轮。

独审确认完整角色集合由旧23项扩至47项，需迁移该旧测试合同。仅测试更新23→47，同时新增一个独立合同Fact，显式写出七角色及共同/native/runtime文件预期，不引用生产列表建立期望；核精确47集合、逐项缺receipt/缺文件/改写的准确失败码及每次恢复。生产字节完全不变，原17例保留。该Fact循环检验全部47项，不拆成膨胀用例数，不扩大为运行时可加载验收。

r10-role-contract定向18P0F0skip（原17+新合同Fact1），883输入稳定；没有生产源码差异。保留r09原红，不累计轮次。
