# 2026-10-27 云端交付收尾

用户本轮明确授权独审、回归后安全合入main及制作Release；此前不改main/不发布约定在此范围被覆盖。仍无强推、部署、新费用/凭据/安全权限、真实模型或新特殊Windows窗口授权。

## 发布闭包的实际缺口与修复

原4c91固定树新发布r01的16条命令均成功，2665文件/992,692,348B、449产品输入前后一致、8份Core同309439c4464df87d969785c47178d1408473f6aa1edff4f53357f05e496f51eb，已独核目录与逐hash。但原发布检查漏掉ImageWorker的Skia依赖，不能把原报告Pass当完整闭包通过。build/bin实际有SkiaSharp.dll/libSkiaSharp.dll，而publish及deps缺失；原件保留不改写，新强检查对r01明确报缺件，保留skia-publish-red.json。

锁定SDK10.0.402的本地官方Microsoft.NET.Sdk.Shared.targets中 `_ComputePackageReferencePublish` 规定PrivateAssets=All且未指定Publish时默认Publish=false。只在ImageWorker两项Windows所需Skia包显式Publish=true，保留PrivateAssets、版本与全部锁文件；macOS包仍不发布到Windows。新增检查同时要求原deps版本/managed→native依赖、两DLL及deps声明的native PDB、既有Git SHA256包pin、原锁文件SHA512 content hash和逐成员字节，不手拼DLL补缺deps。此修复需新实际发布验证，旧r01不回填成功。

## 真实候选包而非安装资格

package-preview.py从固定且已完整结算的新发布目录核before/after产品输入、原publisher字节/16命令/8角色清单和实际归档；七角色合成开发app闭包，Launcher独立为bootstrap-input，KuaiYi管理PDB在symbols，native PDB留原运行目录。仅允许原两runtime固定归档定义的WindowsBase Desktop/Core stub精确冲突对，其余异字节、Windows大小写/前缀/DOS别名冲突均拒绝。manifest另记录被替换stub来源，不能把sourceRoles混称最终同字节。逐输入前后hash、ZIP集合/CRC/逐成员读回、固定来源全复核后才给包SHA；失败输出保留，不覆盖旧件。

verify-preview.ps1是只读文件集合/hash检查，不加载程序、配置、秘密或注册表。读取manifest和每行文件前检查根及各组件，显式目录栈检查后才下降，不使用先递归后拒链接。使用5.1兼容API但实际Windows尚未运行；不加ExecutionPolicy Bypass。清单/路径/hash不是签名、原安装来源或运行授权。

pins仍Unavailable/null。四个产品native在本环境仅有合成fixture，无真实回收原件；扩展真实origin、原G/B/embedded pins、正向startup issuer及正式签名/身份等工程闭环仍缺，不能全说成只等用户点测。候选不是安装器，不公开真实秘密/日志/原文/截图，不开放250/600ms、来源、密码、完整性、取消、清理门。集中Windows项及明确工程缺口见docs/release/WINDOWS-TESTS.md；不会让用户伪造CI环境或沿用旧已消费窗口。

## 首轮完整Core回归保全

固定4c91源码九角色/Installer构建、Core locked restore/build均成功；原一次完整Core实际2932项=2881P/51F/0skip，所有51个失败正文均含No space left on device，不能重分类通过。其TMPDIR为本次独占/tmp/kyrelease27，/tmp实际为独立8.8GiB挂载，最终满；workspace仍约16GiB可用。未删除旧数据或失败root。

本轮临时监督脚本统计目录时遇已删除文件的stat竞态，原Popen wait未返回；之后只核原精确命令及kernel pidfd寿命，09:48:23UTC确认原进程结束，原group仅僵尸、无live成员。该观察不补造原wait或全机清理，监督结果仍Fail/缺证。原TRX、日志、脚本和观察记录保留在artifacts/2026-10-27-release-regression，完整/tmp原件保留。下一必要回归先修本地监督异常路径，使用已授权workspace自有临时目录并分别监测该文件系统与自有输出，不扩大磁盘或清理未知目录。

局部publisher在Skia检查初版14项，content hash校正后16项、候选包12项合成检查实际通过，旧11/9/12/14阶段不累加；不是Windows或真实Skia执行。新publish/完整离线/实际ZIP结果将在产生后单独记录，不能提前宣称main/Release完成。报告的signatureVerification明确仅runtime归档旧资格部分，新增Skia重验单独登记skiaAudits，不提前声称成功。

## 签名包content hash校正

r02已按新元数据生成Skia依赖及实际DLL，但新增检查把原NuGet锁contentHash误当原始签名ZIP SHA512而拒绝；原件保留，不将其写成产品运行失败或改锁规避。NuGet官方PackageArchiveReader.GetContentHash对签名包读取签名内容hash，另有GetArchiveHashAsync读取原始归档hash，二者语义不同（https://github.com/NuGet/NuGet.Client/blob/dev/src/NuGet.Core/NuGet.Packaging/PackageArchiveReader.cs）。修正后保留既有归档SHA256 pin，新增锁定SDK `dotnet nuget verify <固定包> --all` 正常在线验签两包，原命令/退出/组结算分别登记；只有实际Content hash与原锁一致、归档前后pin不变才放行。缓存metadata只作原锁的一致性检查，不作为签名或来源授权。包候选要求这两条原验签收据完整结算，不借成功exit省略content hash。该校正不修改原包、锁文件、系统信任或安全设置。
