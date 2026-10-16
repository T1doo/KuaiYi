# 四字段显示偏好本地导入导出

从 `0bdf754df6b44b2764bea014c63e6e6d51424d60` 正常接续到 `dev/display-preferences-transfer`。本次仅交付用户指定的 SPEC8.1/8.2/8.3 显示偏好切片：字号、首选宽度、主题、减少动画。导入必须先预览、再明确应用到当前设置草稿，原有保存/取消与原子提交、冲突和恢复事务继续生效。导出冻结当前四个表单值的预览，确认后仅创建新文件；已有文件不覆盖。取消尚未发布的操作会撤销；发布完成后不声称文件被撤回。

## 文档合同与边界

采用独立 DTO 与手写 JSON 白名单，**不调用全配置 SettingsJson 读取、校验或序列化**，不遍历配置档或秘密存储。根对象严格要求下列六键，四个偏好以外仅有格式和版本：

```json
{
  "format": "KuaiYi.DisplayPreferences",
  "schemaVersion": 1,
  "fontSizeDip": 15,
  "preferredWidthDip": 420,
  "theme": "FollowSystem",
  "reduceMotion": "FollowSystem"
}
```

| 字段 | 接受值 |
|---|---|
| format / schemaVersion | 精确格式标识 / JSON 整数 1 |
| fontSizeDip | 有限 JSON 数值，12—22 DIP（含边界） |
| preferredWidthDip | 有限 JSON 数值，240—960 DIP（含边界） |
| theme | 精确字符串 FollowSystem、Light、Dark |
| reduceMotion | 精确字符串 FollowSystem、Reduce、Full |

最大4096字节；流读取最多4097字节用于判超界，finally清零缓冲。拒未知、缺失、重复（含转义等价键）、大小写变体、错类型、未知版本、NaN/Infinity/越界/非规范枚举、畸形UTF-8和JSON；固定问题码/中文提示不回显原始字段、值、路径或异常正文。凭据、端点、授权、热键、触发模式、图片发送、开机启动、位置及显示器标识均不在文档或投影内。导入只替换四个Overlay属性，其余对象保持原引用；投影在其它配置不可用的合成反例中仍可运作。链接生产设置命令测试验证不调用CaptureProfile、不读取或清空未捕获的合成密码；真实保存仍由原设置事务执行，不声称原事务不读取其它合法配置。

草稿快照绑定具体draft对象与单调revision。编辑、密钥变更、BeginSave（即使失败）都会使旧快照失效；apply与revision检查在同锁内，两个并发apply最多一个成功。关闭、只读、保存期间不可apply。独立传输ticket与调用取消令牌控制最新操作，拒迟到读/prepare，预览替换不会被旧完成回调覆盖。重复导出在设置命令层返回Busy而不抢首次命令归属；显式新预览继续撤销旧操作。恢复选择、保存与窗口关闭先撤销未完成预览。按钮布局可换行，但实际WPF键盘/可见性尚未运行。

文件仅接受用户选定的完整本地普通路径；拒UNC、Windows设备/ADS/网络驱动器、已有文件或父目录重解析点。创建随机CreateNew暂存，发布时新建目标不覆盖。读前后及prepare/发布/清理核仍打开的自身句柄最终路径；Windows复用仓库既有disk/file-info/[GetFinalPathNameByHandleW](https://learn.microsoft.com/en-us/windows/win32/api/fileapi/nf-fileapi-getfinalpathnamebyhandlew)方式，Linux只查询自身fd。观察到目录改名、符号链接或普通目录替换时，无法确认原路径归属就保留原暂存、报告Cleanup未确认，并关闭句柄。只清理本次能确认归属的暂存，不扫描/清理其它锁或数据。**路径核对与File.Move/Delete是两个步骤，不是防御恶意并发换名的原子保证**；真实Windows句柄/文件对话框仍NotRun。

## 实际验证与失败原件

| 运行 | 实际结果与解释 |
|---|---|
| r00 | 初始候选App交叉构建0W/E，仅历史构建，不代表最终源码 |
| r01 | 新测试xUnit2031编译诊断，0执行/无TRX；保留源码/日志，不能计为产品红测 |
| r02→r03 | 同三个完整名称、同三份测试源码字节，**3F→3P**。候选缺陷分别是畸形UTF-8外逸InvalidOperation、目录替换清理误删同名合成未知文件、实际成功导出状态被Cancel提示覆盖。r02只恢复初始候选Cancel方法复现，**不是父生产0bdf754红测** |
| r04 / r06 | 阶段专项105P / 完整Provider2152P；随后又加命令并发覆盖，非最终总数 |
| r08 | 完整Core1553P/1F。原有OleReceiverTests.RealParentReadDistinguishesStaleQueryFromAcceptedProductionFeedback缺QueryWritten观测；旧测试/OLE源码未改，原log/TRX/DLL绑定保留 |
| r09→r10 | 同一新命令屏障测试源码、同完整名称，**1F→1P**：重复Busy抢走首次导出UI owner，文件实际发布却留“正在处理”；修命令准入归属，不靠禁用按钮假定不可重入 |
| r11 | 最终四字段专项110P，0失败/跳过 |
| r12 | 最终完整Provider **2157P** = 父2047完整名称保留 + 新110；0失败/跳过 |
| r13 | 按最终候选完整复测 **1554P** = 父1551完整名称保留 + 新3；0失败/跳过；不能据此声称r08竞态已解决 |
| r05 / r07 / r14 | Provider/Core/App locked-mode恢复，无新依赖/锁变更 |
| r15 / r16 | App、ImageWorker Release交叉构建均 **0警告/0错误** |

110项Provider包含18组枚举/边界往返、6组缺失/重复转义键、11组敏感/未知字段、31组类型/版本/范围、畸形JSON/UTF-8、有界流、不可导出值；四字段投影/归属/ABA/密钥编辑/并发/迟到/取消、真实文件新建/碰撞/链接与目录替换/清理失败、原设置事务save/cancel/CAS冲突、链接实际设置命令的预览/明确应用/密码隔离/重复命令/四种迟到撤销。Core另3项验证保存准入/失败保存的旧revision不复活、未知schema只读。无删除旧测试或弱化断言。

r08首次OLE观测失败的根因未实证，本次不修改旧Core测试或OLE生产链路；r08/r13的Core.Tests及OLE DLL相同，但Core DLL因随后增加暂存cleanup安全错误码处理而不同，r13是按最终候选完整复测，并非所有生产DLL原样。复测通过也不等于消除竞态。历史图片间歇失败根因也仍未知，图片生产源码未改、离线复用保持Off。所有250/600ms、来源/密码/完整性/取消/清理门保持。

[固定证据与逐原件SHA256/大小清单](../test-evidence/2026-10-16-display-preferences-transfer.json)包含所有候选、红绿、初轮失败、最终回归、源码与执行DLL绑定；r11-final-source为最终源码，早期final-source只是r05—r08当时快照。独立只读审查状态见同一清单。P1-03/A10/L01/L06仅追加子项证据，整体状态不提升；所有真实Windows/WPF/DPAPI/ACL/原生组件、模型质量及完整SPEC验收仍NotRun。

所有dotnet激活前后显式DOTNET_GENERATE_ASPNET_CERTIFICATE=false；证书库/信任Unknown且不读改。Skia4.153.1四包许可待批、源码403不绕，不安装/运行；Actions先前拒绝，不查询、调度或换路，run数Unknown。无main修改/强推/部署/新费用/真实模型/凭据配置/安全权限/法律协议接受；仅正常开发分支，未知锁、数据、日志、进程保全。
