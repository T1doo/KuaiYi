# 深色选择框与原帧观察修复（离线）

状态：本轮完成源码修复、交叉构建与定向回归；原第二窗口仍未通过，不重试、不启动第三窗口。

## 可直接打开的原运行与证据

- [第二窗口原运行](https://github.com/T1doo/KuaiYi/actions/runs/40217224688)，job121240880724，attempt1，delivery3164cf35476463d3a85361bdfb3b909b1ada26f2。
- [GitHub原artifact下载页](https://github.com/T1doo/KuaiYi/actions/runs/40217224688/artifacts/12608252366)，原ZIP1619996B，SHA256 `9e2f546770937ac71377369b0599b73778e55fe03b83f56bbcce536cd08ea0b0`；原GitHub保留至北京时间2026-10-27 01:38:29，另有本工作区保全。用户可从GitHub下载ZIP，不把云端本地路径冒充可打开的图片链接。

8图均在ZIP内 `before-settings/raster/`，绑定before=e4d57e77426ab235c7219b375debd76bc072e00b；运行报告App hash=dd401809f6ee20744da130c10f662b93091555dfa1358b28e23e6c11e9d0b1d4，与prepared bundle inventory相同。artifact无DLL本体，不冒称本地直接读取了DLL。8图仅before基线，after未运行，不是前后对照。完整每图SHA/状态在同日机器收据。

| ZIP内文件 | 实际状态 | 内容像素／DPI |
| --- | --- | --- |
| Light-wide.png | Default | 924×661／96 |
| Light-narrow-input-focus.png | ProgrammaticInputFocus | 404×641／96 |
| Light-narrow-button-focus.png | ProgrammaticButtonFocus | 404×641／96 |
| Light-narrow-input-disabled.png | OriginalInputDisabled | 404×641／96 |
| Dark-wide.png | Default | 924×661／96 |
| Dark-narrow-input-focus.png | ProgrammaticInputFocus | 404×641／96 |
| Dark-narrow-button-focus.png | ProgrammaticButtonFocus | 404×641／96 |
| Dark-narrow-input-disabled.png | OriginalInputDisabled | 404×641／96 |

## 当前缺陷与最小生产修复

修复起点2d28的DesktopTheme字节与419082相同；只为ComboBox设置背景/前景并未替换原生选择区模板。before Dark-wide实际浅背景/浅字缺陷对应机制在当前源码仍存在；after没有实图，不能声称直接观察了after。现在仅给非editable ComboBox显式选择区/弹层模板，使原palette Background/Foreground真正绘制。保留原实例、原选择与键盘代码、SelectionBoxItem/Template/Selector/StringFormat、PART_Popup及TwoWay展开；editable保留原模板。没有改模型、强度、配置保存、密码、启动/安装资格门或全局主题。

实现依照[Microsoft ComboBox模板parts契约](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/controls/combobox)保留原Popup及ItemsPresenter/ScrollViewer结构。Linux交叉构建不验证真实Popup、键盘、焦点或高对比像素，这些仍未验。

## 探针标准保持

原Opening几何220ms与opacity80ms不是同一门；opacity=1仍可属于合法几何中间态。但新Full成功不能只凭这种帧：同一request/新opening epoch必须实际观察到非anchor且非原target的几何，以及独立0<opacity<1淡入样本；缺任一项仍Fail。内部Rendering属性样本不是人眼淡入/扩散或DWM质量通过。

在原Delta发送前订阅Rendering/发布TCS；原Send接受后，再核当前版本、Results精确正文和实际原document marker才锁epoch累计，排除hover-toolbar/旧正文。Capture重入保护，异常送回原TCS；前后复核原epoch/Card/Target/gate，finally解绑。原clock、220/80ms与产品动画代码均不改，不补造帧、不把无帧改通过。

## 实际本轮检查

Harness/App与Provider交叉构建0W0E，最终r03五命令exit0、原进程组空；15P0F0skip=新观察类11+原palette4，887输入与130DLL前后hash同。定向例包括合法opaque几何、量化target不可作中间、无帧、reduce终态、旧/跨epoch、未接受正文、缺fade不能Full通过。r01初版11P与独审补门过程、r02 CS1525一编译错原件保留；r03通过不回填旧失败。无全Core、真实WPF或第三CI。

## 清理结论分开记录

| 对象 | 已核事实 | 结论范围 |
| --- | --- | --- |
| before设置原进程／窗口 | 子报告Close/Resources true；exit0；原wait与双drain EOF/close | 有限原资源结算通过；深色可读性失败，非质量通过 |
| before浮窗／托盘 | 子报告Closed、原tray disposed且added=false；exit3、原wait/IO结算、无timeout/rescue | Body中间帧条件失败，有限清理有据，整体Failed |
| 原请求任务 | 原1 Flow+1 connector完成记录 | 本请求结算；无真实模型发送 |
| 整作业进程树 | runner另记录终止3 dotnet+1 conhost orphan，缺命令行/parent映射 | 归属未知，未证明全树自主清理，不认定均buildserver |
| after窗口／像素 | 首错后未启动 | NotRun，不能写清理通过 |

## 仅建议的下一必要原生窗口

不执行、不自动重跑。若另获明确批准，建议只用标准免费windows-2022一次job≤15min、visual≤6min：重新绑定before=e4d57与after=本轮修复完整提交SHA（本轮最终回报给出，不能仍用419082），先完成同job before/after设置对照，加入只读Combo关闭/程序打开弹层/选择显示/焦点/禁用的有界状态，再采原Full/Reduce浮窗。仅合成内容和自身窗口/托盘/程序输入，原HKLM EnableLUA只读披露、无registry写/系统DPI主题变更/真实采集/模型/真实安装/四native DLL窗口。任何新候选须先绑定源码与入口并独审，所有失败/UNKNOWN保全。

所有本轮修改均不触及workflow；正常开发推送不会触发新的视觉窗口。
