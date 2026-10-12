# P4 密钥命令编辑失效补正

基线 `05dcf4705edfb123ea44001de550e8b1acf628c4`。监督确认一次测试授权隔离和冻结共享总预算两项成立；编辑取消仍漏清除已保存密钥按钮，本轮仅补该命令及实际同类的预设清引用，不推进其他功能或完整阶段。

清除按钮现在调用 `SettingsWindow.ClearApiKey`，与预设的秘密清除步骤共用 `ClearDraftApiKey`。这个入口先调用原统一 `ApiFormEdited`（推进测试草稿代次、取消当前身份），再修改 `SettingsDraft` 的密钥引用；不依赖 PasswordBox.Clear 是否发事件。旧结果在目的队列仍核身份和编辑代次。ApiFormEdited 原体只搬至命令 partial 文件，语义不变。新输入密钥只在 CaptureProfile 的非空密码分支 SetKey；输入已有 PasswordChanged、保存也会 Clear 非空框，不存在本次的空控件清旧引用遗漏。显示/遮罩按钮只操作新输入，不读取或删除已保存引用。未扩张其他设置流程。

回归实际执行按钮所调用的同一生产 partial 命令体（测试工程 Link 编译原文件），不是另写一套取消逻辑。Linux 测试专用 Password/Mask/Text 端口明确模拟空 Clear 零 change 事件；真实 WPF Click/PasswordChanged 与桌面未执行，不把这些端口当平台验收。真实 SettingsCoordinator/SecretFileStore 临时 AES 合成密文、原 gate/workflow/lease、loopback HTTP 和最终目的队列执行。所有凭据仅为公开 fixture，未读用户密钥/真实模型。

6项包括：空框/保存引用/运行中清除；清除与预设无值变化时拒绝原待确认 intent（2）；已排队 Completed 在清除后不应用；清除保存后原事务撤销磁盘引用；无选中profile不清除或取消其他测试。检查原请求 token、无成功事件、结算后的 transport lease 已释放、只有1次原HTTP或0次待确认HTTP。草稿清除不提前永久撤销存储：取消草稿原引用仍有效，保存时才撤销。没有声称 managed header string 能全部清零。

原生产 Window/Api 两份05dc精确字节、等价抽出的原命令体和原红测保留。初试队列夹具把 Post state 当信封，实际 shared receiver 传 null，因此等待超时；该失败也保留。夹具改为依据已经应用的真实 Usage 阶段扣住其后 Completed，不改生产 receiver或断言。修复前重跑6中5失败/1通过；修复后同一6项/同一断言全部通过。完整回归与独立源/锁/测试保留/历史包/PE证据见本轮summary，实际 Windows 列表见独立清单。

独立包 `artifacts/KuaiYi-P4-secret-command-managed-win-x64.zip`，框架依赖、无安装器/原生DLL；不替换前23份包。P0/E06 Blocked、P1—P4 InProgress、106整体NotRun。其他监督结论待返回；Windows UI/跨应用/焦点/动画/DPI/DPAPI/安装/远端CI未验。

最终完整Core1262+Provider243=1505/1505、Node63/63、27locked Release零警告/错误；原1499测试名称全部保留，27锁与规格字节不变。独审核23旧包+本包、19历史精确commit摘要与当前发布DLL/十个PE，原历史恢复事故不抹去。
