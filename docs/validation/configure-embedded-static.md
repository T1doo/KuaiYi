# EmbeddedV1：精确 CMake 3.31.6 的单变量修复候选

基线为原失败结果归档 a1291b99f12d46bedc96805f70a10215e53d6247/tree ea3d47bbb448d8b97eefc7ac8233a7051b121f05。原唯一run38716240995的 root Exit0、WaitJobEmpty Timeout、整体Exit3与清理确认保留；FirstWait/LastPreRescue均Active2/Total33不证明成员身份，也不证明区间内不变。原失败日志与归档不修改。

本批只静态审查、离线来源验证及本地冻结，不运行CMake/Windows、四案例或CI，不推送。独占写入锁与单worktree已核；本地分支dev/configure-embedded-local不匹配任何现有workflow，七份workflow原字节保持，不新增执行触发器。

## 精确来源与生效链

官网`/help/v3.31/`当前页面实际标为3.31.12；因此以Kitware官方v3.31.6标签的peeled commit `859ca5c4d7396a011a462878179bc173e1283731`为实现依据，标签对象a9b14138fe83e134bc3da139857b5616695bab54。30份源码均以完整commit URL重新读取，字节与初次标签读取一致，原文仅私有保全，文件hash见[固定来源合同](../test-evidence/2026-10-15-configure-embedded-source.json)。仅摘录行号、链接和结论，不导出原CI路径/命令日志。

| 环节 | 3.31.6 静态证据与结论 |
| --- | --- |
| policy前提 | 未改工程前两行是minimum_required(VERSION 3.28)然后project(CXX)。[minimum_required规则](https://github.com/Kitware/CMake/blob/859ca5c4d7396a011a462878179bc173e1283731/Help/command/cmake_minimum_required.rst#L56-L63)将3.28及之前引入的policy设NEW；[CMP0141定义](https://github.com/Kitware/CMake/blob/859ca5c4d7396a011a462878179bc173e1283731/Source/cmPolicies.h#L427-L430)引入版本3.25。因此正常本工程路径已在第一次project前NEW，无需新增policy开关。命令行-D值在项目处理前可用；没有改工程CMakeLists。 |
| CompilerId | [CMakeDetermineCompilerId](https://github.com/Kitware/CMake/blob/859ca5c4d7396a011a462878179bc173e1283731/Modules/CMakeDetermineCompilerId.cmake#L647-L669)直接配置专用VS模板并以Debug调用MSBuild。[模板52–57](https://github.com/Kitware/CMake/blob/859ca5c4d7396a011a462878179bc173e1283731/Modules/CompilerId/VS-10.vcxproj.in#L52-L57)编译DebugInformationFormat为空、链接GenerateDebugInformation=false；此路径没有消费该变量。Embedded不把这个CompilerId转换为/Z7；它也不是普通target/ABI try_compile路径。导入的VS props/targets及环境仍可能影响最终工具调用，未在本批实测。 |
| ABI/working检测 | [CMakeTestCXXCompiler 26–31](https://github.com/Kitware/CMake/blob/859ca5c4d7396a011a462878179bc173e1283731/Modules/CMakeTestCXXCompiler.cmake#L26-L31)先做ABI；成功后自然跳过独立working测试，候选不强制跳过。ABI模块[74–84](https://github.com/Kitware/CMake/blob/859ca5c4d7396a011a462878179bc173e1283731/Modules/CMakeDetermineCompilerABI.cmake#L74-L84)调用source形式try_compile；ABI未定义时运行，forced compiler或已有信息可改变探测路径，候选不设置这些值。 |
| 子项目传播 | [cmCoreTryCompile 678–684](https://github.com/Kitware/CMake/blob/859ca5c4d7396a011a462878179bc173e1283731/Source/cmCoreTryCompile.cxx#L678-L684)在子project之前写对应CMP0141；[1103–1108/1152/1234–1238](https://github.com/Kitware/CMake/blob/859ca5c4d7396a011a462878179bc173e1283731/Source/cmCoreTryCompile.cxx#L1103-L1154)传播该平台变量，前提未启用CMAKE_TRY_COMPILE_NO_PLATFORM_VARIABLES。候选保留原source签名、exe目标和链接，未设置禁传播、STATIC_LIBRARY、COMPILER_FORCED/WORKS等。 |
| 编译器debug format | [Windows-MSVC 348–354](https://github.com/Kitware/CMake/blob/859ca5c4d7396a011a462878179bc173e1283731/Modules/Platform/Windows-MSVC.cmake#L348-L354)将NEW转换为启用抽象；[464–497](https://github.com/Kitware/CMake/blob/859ca5c4d7396a011a462878179bc173e1283731/Modules/Platform/Windows-MSVC.cmake#L464-L497)避免旧默认/Zi并将Embedded映射-Z7。[cmLocalGenerator 3021–3040](https://github.com/Kitware/CMake/blob/859ca5c4d7396a011a462878179bc173e1283731/Source/cmLocalGenerator.cxx#L3021-L3040)读取target属性；[v143 CL表](https://github.com/Kitware/CMake/blob/859ca5c4d7396a011a462878179bc173e1283731/Templates/MSBuild/FlagTables/v143_CL.json#L9-L21)将Z7映射为VS OldStyle。这是ABI编译端从默认ProgramDatabase到Embedded的具体干预路径；实际最终flags仍受外部环境/props影响，本批未测。 |

[官方变量说明](https://cmake.org/cmake/help/v3.31/variable/CMAKE_MSVC_DEBUG_INFORMATION_FORMAT.html)说明变量初始化新target属性并传播到try_compile，且要求CMP0141在启用MSVC ABI语言前NEW。无条件Embedded会对后续新target的所有配置选择该format；本configure-only入口的终点仍是configure，不另跑任何目标构建，也没有切Release或改变try_compile配置。

## 三个不能混同的机制

1. 编译器PDB：按[Microsoft /Z7说明](https://learn.microsoft.com/en-us/cpp/build/reference/z7-zi-zi-debug-information-format?view=msvc-170)，/Z7把调试信息放入.obj，编译器不生成PDB。[Microsoft /FS说明](https://learn.microsoft.com/en-us/cpp/build/reference/fs-force-synchronous-pdb-writes?view=msvc-170)进一步区分/Zi或/ZI编译器PDB经MSPDBSRV串行写入的情形。这为移除ABI编译端PDB依赖提供机制依据；没有证明原调用用了/FS或原残留Job成员来自该依赖。
2. 链接器PDB：同一官方说明明确/DEBUG仍能从这些.obj生成链接器PDB。[Windows-MSVC 319–326](https://github.com/Kitware/CMake/blob/859ca5c4d7396a011a462878179bc173e1283731/Modules/Platform/Windows-MSVC.cmake#L319-L326)独立保留Debug/RelWithDebInfo链接/debug。候选不禁链接、不改链接选项，因此不承诺零PDB、无PDB相关进程或JobEmpty。
3. MSBuild节点复用：按[官方nodeReuse开关](https://learn.microsoft.com/en-us/visualstudio/msbuild/msbuild-command-line-reference?view=vs-2022#switches)，复用节点可在构建完成后保留；这是独立机制。精确CMake的[CompilerId MSBuild调用](https://github.com/Kitware/CMake/blob/859ca5c4d7396a011a462878179bc173e1283731/Modules/CMakeDetermineCompilerId.cmake#L650-L666)和[普通VS构建命令](https://github.com/Kitware/CMake/blob/859ca5c4d7396a011a462878179bc173e1283731/Source/cmGlobalVisualStudio10Generator.cxx#L1209-L1230)均不由Embedded切换nodeReuse。不能据此证明原运行实际留下MSBuild节点；本原LifecycleOperation.cs的ChildEnvironment第172行已设置MSBUILDDISABLENODEREUSE=1，候选原字节保留；这不能独立证明所有工具实际复用行为。候选不修改/nr、环境变量或复用设置。

## 最小候选与审查边界

唯一配置差异是原NativeConfigure参数末尾增加`-DCMAKE_MSVC_DEBUG_INFORMATION_FORMAT=Embedded`。另将ReviewedParent同步为精确a1291b99，仅是来源身份。生成器VS17 2022、x64、v143精确toolset、VS实例、SDK、编译器识别/ABI/working检查及链接保持；原Run/C#/投影helper、JobEmpty、30秒预算、救援/关闭顺序、根租约、Python跨进程DuplicateHandle与四文件64KiB/256KiB上限全原字节保持。无成员探针、Release/跳探测/延长超时。

候选精确五文件：入口、新独立来源测试、本说明、新JSON来源合同、progress。旧来源测试/七workflow/失败结果及私有manifest均不改。新来源门锁唯一父/tree、locked祖先/tree、五文件大小写scope、三运行blob/canonical SHA与工作内容归一；真实owned浅clone已23拒绝/24通过，新合同5项通过。CLI的--verify-source仅运行只读Git检查，不调用CMake；本地测试用合成提交/临时index隔离验证错误父/SHA、额外/大小写路径及运行字节漂移。

此候选有ABI编译端因果干预依据，尚不是原超时根因修复证明。原四案例、P0/E06和生产验收仍未通过，250/600ms及来源/密码/完整性/取消/清理安全门保持，ProductionReuse Off、ModelCalls0。本批冻结后交父复核，不推送或启动CI。

本批实际复跑Accounting162断言/11场景与旧证据45通过。旧configure171合同在163项通过后因原始命令严格相等要求拒绝，原测试不改且完整日志保留；owned临时clone中仅将这条命令期望精确追加Embedded参数，原全部171断言通过，没有删除场景/断言。后者是新配置的注入合同，不冒称旧冻结门通过或Windows实测。来源5项覆盖完整Git门、CRLF接受、wrongSHA/父、working/committed字节、extra与大小写scope拒绝。
