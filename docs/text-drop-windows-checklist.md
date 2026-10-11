# 正式文字 Drop 的 Windows 待执行清单

全部 **NotRun**。Linux 协议/策略测试不能替代下列操作。仅在本项目授权 Windows 环境、公开测试文本、无真实模型调用条件下执行；首先完成 P0/E06 原生宿主审查。

1. 使用既有 build.ps1 -Scope All 在 Windows/MSVC/SDK 构建真实 KuaiYi.OleNativeBridge.dll，核验 x64 COM ABI、真实 RegisterDragDrop/OleInitialize/注册线程消息泵、撤销和同 HWND 组合。当前 managed ZIP 缺该 DLL，正式 Drop 会明确停用，不是假成功。
2. 原规格 E01：模式 Selection/Drop 持久化与互斥；Drop 开启仅划选、不拖出，不触发 UIA/复制/HTTP；Off 拖入不 QueryGetData/GetData；暂停、隐藏、锁屏与恢复不继承旧身份。
3. E02：浏览器、Markdown 编辑器、PDF 阅读器真实选文拖到按钮/移出/取消，记录 QueryGetData 与 GetData 计数；只有实际 Drop 才取内容，离开旧原文不变，提示最大96×64DIP，无激活/焦点争抢。
4. E03：使用公开中文/英文/emoji/代码段，从 Chrome/Edge、VSCode/Obsidian及Adobe/Edge PDF支持的实际 OLE 入口投放；核验所选字节、来源 HWND/PID/start/path、同投递恰一次、限长及确认标记。P4尚未接入，当前只保存内存来源；不能验成“只请求一次翻译成功”。没有OLE源的应用记录Unsupported，不伪造兼容。
5. E04：实际可编辑来源允许 Copy+Move 或 Move-only；仅 COPY，MOVE-only拒绝，源未删。测试缺失async接口、GetAsyncMode(false)、StartOperation失败均在同步 Drop 返回前深拷贝；真实async=true进行跨STA封送、EndOperation恰一次与所有实际Release。
6. E05：实际 CF_UNICODETEXT / HTML Format / Rich Text Format 优先级；无效高优先级不回退；UTF8 CF_HTML字节偏移/StartSelection、隐藏标签、未闭合内容、有限RTF目的地/codepage/Unicode；图片/文件/虚拟文件/仅URL拒绝文字入口，URL不下载，HTML不执行，RTF不加载对象。字体charset切换等不支持形式明确拒绝；不宣称任意Word RTF兼容。
7. E06：故障来源卡在 Query/GetData/Release/Start/End/封送；原生来源检查后与明文enqueue前1500ms本地复核不续期，父/Core各自接收起点也不续期；没有共享原生起点，端到端总预算仍待证明；Off/取消立即撤销共享授权，主UI仍能拖动/收起/设置；旧任务/注册/读取/交付/退出未结算不启动第二接收器。尤其实际App-owned HWND+独立注册STA+远程nativeTarget组合响应性仍未证明。
8. 四角/屏幕边缘/负坐标/多显示器/100%—200%DPI、跨屏移动与DPI变更；实际圆角物理边界及每帧A⊆C(t)⊆W、锚点冻结、目标延迟控制、提示收回后旧结果恢复。GrowthHarness禁用所有真实Selection/Drop后端，仅能评审公开动画。
9. UIA、授权复制、600ms/1200ms既有截止、逐应用隐私权限、托盘/焦点、关闭与退出清理真实回归；安装/卸载、DPAPI/ACL与开发身份仍分别待验，ZIP不是安装包。

记录 Windows版本、应用版本、公开样本ID、原生构建日志、线程/清理计数、录像、实际失败状态与时钟证据。106整体验收保持NotRun，P0/E06阻塞不能被本清单或离线通过改变。
