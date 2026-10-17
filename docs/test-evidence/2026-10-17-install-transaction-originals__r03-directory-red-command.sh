export DOTNET_GENERATE_ASPNET_CERTIFICATE=false
source /workspace/.kuaiyi-setup/activate.sh
export DOTNET_GENERATE_ASPNET_CERTIFICATE=false
dotnet test tests/KuaiYi.Core.Tests/KuaiYi.Core.Tests.csproj -c Release --no-restore --filter 'FullyQualifiedName~ASeparateCaseAliasedDirectoryIsNotSilentlyDroppedFromThePackage' --logger 'trx;LogFileName=r03-directory-red.trx' --results-directory artifacts/install-transaction-core
