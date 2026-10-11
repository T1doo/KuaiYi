# ADR 0013：正式文本 Drop 复用隔离接收器与最终来源门控

状态：源码已实现，Windows平台验证Blocked。基线a870389599a6fe046d3cef62ea14a4647b58bbb1，限定监督切片，不推进P4。

正式Shell复用 App-owned HWND、独立registrar STA、私有OleReceiver进程与共享授权戳。出生时冻结 receiver GUID / wire epoch / CaptureTicket / 设置版本，设置、On/Off、模式、暂停、隐藏或锚点变化撤销；旧Run、注册、revoke、read、delivery与实际进程退出未结算，不能创建第二接收器。私有子进程仅真实OLE STA调用COM，没有生产mock。Linux测试只替换明确的OS/注册/共享映射端。

外部 DragEnter/Over 仅格式查询/几何与来源复核，无GetData、UIA或剪贴板重读。选择CF_UNICODETEXT→HTML Format→Rich Text Format，文件/虚拟文件先拒绝；无效高优先级不向低优先级回退。实际Drop使用HGLOBAL有限深拷贝、严格Unicode、惰性纯文本净化，URL不下载、HTML不渲染、RTF对象不实例化。HTML以UTF8字节偏移取fragment或更窄selection；1MiB输入、有限标签长度，嵌套隐藏目的地等歧义明确拒绝。RTF为独立有限子集：512KiB、64层、32字控制、有限codepage、Unicode fallback与隐藏目的地；未知group destination、alternate content、Mac/PC/codepage/font-charset切换等明确拒绝。不是通用Word文档解析器，真实应用格式兼容仍待验证。

Win32 source identity检查前台 HWND / PID / 启动时间 / 执行路径、会话/完整性、原生焦点与Edit密码样式，并保留终端、自身与用户排除策略。OLE没有提供标准的“真实源进程证明”，前台匹配是本实现的保守关联策略，不能称对任意伪造IDataObject的来源认证；自定义控件/浏览器密码与跨应用支持必须实际测试。不会为Drop伪造UIA RuntimeId或选择矩形。

已取得文本通过统一SelectionSourceSnapshot保存SourceKind=OleDrop和receiver/transaction身份；RawText为实际语义纯文本，Text统一换行/控制字符净化，Unicode scalar硬上限50k、软上限12k确认。ReceiverHost以原生group建立时的Stopwatch测量本地1500ms；父OleReceiverClient以收到合法DropBegin时的TimeProvider测量本地1500ms；Core以Begin被接受时的自身TimeProvider测量本地1500ms。各自不续期，但没有传递原生起点，不跨进程比较绝对timestamp，不宣称共享原始DropBegin到最终交付的1500ms硬截止。ReceiverHost来源检查之后和明文enqueue前再次检查本地gate/共享activation/elapsed；目的队列使用父取消锁和RuntimeGate提交锁复核身份、可见性、设置、来源及父/Core本地截止。参数所称receiverCurrent是父接收客户端资格，不是远端原生时间证明。IPC延迟仍没有共享端到端预算证据。最多256已见事务后撤销整lease，不能清表后复用旧ID。同投递恰一次；相同文字的新投递仅给内存重开提示。失败不覆写旧来源，取消清掉排队引用，所有未交付IPC数组唯一观察者清零，读取任务未实际settle不释放activation。CLR字符串仅释放引用，不能承诺物理擦除。

提示使用正式浮窗当前与最终圆角物理交集，只允许按钮或最多96×48DIP的小型目标；进入/离开不触发请求，收回恢复旧面板。协议绑定各阶段同source、format与Drop位置；真实COM生命周期继承同步返回前副本、可选async协商/封送/End与清理监督规则。P0/E06响应性假设仍待实测。包缺nativeBridge时显式停用，不能运行出假的正式Drop成功。

官方核验依据（只读，不接受新法律协议或采纳示例代码）：

- [IDropTarget::Drop](https://learn.microsoft.com/en-us/windows/win32/api/oleidl/nf-oleidl-idroptarget-drop)：POINTL屏幕坐标与effect返回语义。
- [RegisterDragDrop](https://learn.microsoft.com/en-us/windows/win32/api/ole2/nf-ole2-registerdragdrop)：OLE初始化与消息泵约束。
- [IDataObjectAsyncCapability](https://learn.microsoft.com/en-us/windows/win32/api/shldisp/nn-shldisp-idataobjectasynccapability)：optional协商并不允许随意延后同步GetData。
- [HTML clipboard format](https://learn.microsoft.com/en-us/windows/win32/dataxchg/html-clipboard-format)：UTF8 byte offset/fragment/selection。
- [Microsoft RTF informative reference](https://learn.microsoft.com/en-us/openspecs/exchange_server_protocols/ms-oxrtfcp/85c0b884-a960-4d1a-874e-53eeee527ca6)：找到官方存档定位；存档带使用许可文字，本轮未复制/采用其示例或点击接受。有限解析器独立实现，完整RTF兼容不作已验声明。
