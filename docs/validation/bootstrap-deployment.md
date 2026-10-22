# 不可变 bootstrap 部署、替换与撤销基元

从1c1aedcfd311e783267c37a20141d136ae2fa500在原环境唯一写入者继续SPEC13.3/13.4、P7-04/05；仅内部文件事务与实际Launcher入口资格，不是真实安装器运行或P7完成。

## 精确归属与发布

根目录EXE/DLL逐个覆盖会使CLR在Main检查前加载混代，故不原地替换。每次复制到bootstrap-bundles/staging-<operation>，全校验后rename为新随机GUID目录，再原子发布stable-bootstrap.json。目录只含五运行文件及原pinned package.json：KuaiYi.Launcher.exe/.dll/.deps.json/.runtimeconfig.json与KuaiYi.Core.dll。源catalog只能精确五文件，逐文件32MiB、总64MiB；实际普通构建PE/托管identity/full revision、Core与当前v3 generation的Core字节相同、固定依赖图与runtimeconfig都要匹配。

本片Mode明确FrameworkDependentDevelopment；runtimeconfig为net10.0、Microsoft.NETCore.App framework10.0.0，**没有携带运行时**，不是10.0.13自包含发布。bootstrap自身五文件用真实普通build bytes；测试generation中三项native、runtime和第三方缺件仍是已有结构夹具，不可签可运行Windows整包。

bootstrap-store.json持久绑定随机owner与canonical root/data；未知已有目录无owner不收养。active绑定owner、bundle ID、catalog SHA、source revision和完整InstalledVersion。旧目录永不覆盖或自动删除，历史journal保存精确归属；撤销仅archive exact active记录并保留目录，不能以此声称卸载文件清理已完成。

## 门、回滚与清理

复用既有Host exclusive/upgrade mutation admission，新增bootstrap-deployment-pending.json拒绝正常启动和变更；RequireRecords与RequireImageRecords也持续拒绝，既存Host被撤销后不重新变成current。同步copy/flush/rename均由原Run Task保留到实际完成，取消不放弃原IO。不发现、收养或杀任何PID。

journal在任何store/bundle/active写入前持久化；owner4KiB、receipt8KiB与组合START原16KiB均在journal前校验，不扩大上限。部分复制保留与本intent关联的staging。active发布在pending下仍属provisional；失败仅在exact owner/current/journal/active符合时补偿旧active。未知替换或补偿失败保留原错与补偿错、pending及全部文件，不覆盖外来内容。原admission实际关闭后才归档journal；关闭未确认保留同一owner、原句柄和持久拒绝门，重试不重开替代句柄、不把失败改成功。

实际Program只接受bootstrap-bundles/<GUID>布局，从本机KnownFolder取得data；持有原六个read handles。取得admission起即由entry字段拥有，入口失败独立结算每个原资源，关闭错不丢lease；程序失败/原controller未结算时保留原entry。私有START携exact bootstrap owner/active/current绑定，controller在其原admission内、ClaimExecution/UI Start前重核，防入口早检查后被替换/撤销却迟到启动。指纹与CLI不是授权；原Off、图片/发送与250/600ms门未改变。

## 实际候选验证

原件artifacts/2026-10-22-bootstrap-deployment保留命令、日志、TRX、输入/DLL前后摘要与候选源码，各轮不相加。

- r01 Core普通编译0W/E，零测试；r02七正常managed命令0W/E、898输入稳定。
- r03测试编译因xUnit2031拒绝Assert.Single(Where)，无TRX/测试启动；修为Single(predicate)，未关分析器或降低断言。
- r04 56P0F0skip：源闭包、初装/替换六阶段失败、部分原复制、撤销/取消/冲突、Entry双原错与同owner重试、原20和新真实两跳3。尚未包含随后metadata上限修复，不代签最终树。
- r05实际隔离Host入口新增pending持续门2P；r06正常Launcher/Core构建0W/E。
- 独审先后指出Entry关闭原owner会丢失、owner消费4K但生产未限、入口检查后迟到启动窗口，均已在生产路径修复并加相应验证；组合wire仍维持原16K。r07候选60P0F0skip/899输入稳定，含40本片新例与原20；纯metadata边界测试不创建长路径或声称Windows布局可用。

产品冻结为`aab208ab716f5c1b36c0392d464ff35d8fc1dd05`。r08从clean树执行七次正常managed构建，全0W/E；r09同固定树一次60P/0F/0skip、4实际类、60唯一显示名称，保留旧20完整多重集并新增40。不是完整Core/Provider/Codec，不重跑旧586/310或无关套件，候选与固定轮次不相加。

r08/r09四快照各899输入，before=after并逐项匹配产品Git。17目标记录DLL/AssemblyInfo摘要且含完整产品revision，14产品目标跨构建/签收产物相同；CoreTests与两个实际fixture由本轮测试正常编译单列。Launcher、CoreTests和两fixture的实际Core副本均与主Core字节一致。既有357个src中351未变、六处接线修改，另增三基元源码；旧测试只把一处class改partial，其余原字节及全部旧断言保持。锁、workflow、packaging、扩展、scripts及sln均未改；旧310/586/105归档摘要保全。

## 尚未交付与未验

尚未接固定路径locator/快捷方式或统一安装入口，没有把generation物化、bootstrap和Host注册伪装成已完成的跨资源事务；后续应由统一事务持有原入场门并协调三方commit/补偿。所有原角色会话及Host注册接口保留，此内部基元无外部注册CLI。

三native DLL与完整自包含runtime仍缺；真实Windows/medium/Explorer/browser/OLE/WPF/安装注册卸载、自启动、OCR质量、画质与250/600ms未验。无新许可、依赖、凭据、安全权限、网络配置、真实模型费用或用户电脑操作；不重探已拒runner，不改main/强推/发布部署。

## 独审与原件

源码、固定60条TRX、899输入、17目标和四Core副本均获独立只读限定ACK，未用独审代替重跑。归档CRC及223唯一成员/222项manifest全部核过，14产品/测试blob加AGENTS/SPEC与固定Git字节一致；准确保留r03分析器编译失败，不包含私有helper、上传回执、私有URL或二进制本体。

原件bootstrap-deployment-originals.zip已保存Library：确认ID `libfile_20fae98a783481918afd40bc2229d393`，v0，1,475,759字节；SHA256 `664be5b73467cfd7994fbe0956cfbabd1ffcb219befbfa9b0212fb61c230ad4a`。ZIP三报告为保存前快照，随后仅追加独审/Library收据，不改产品和固定输入。
