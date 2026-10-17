export DOTNET_GENERATE_ASPNET_CERTIFICATE=false
source /workspace/.kuaiyi-setup/activate.sh
export DOTNET_GENERATE_ASPNET_CERTIFICATE=false
dotnet test tests/KuaiYi.Core.Tests/KuaiYi.Core.Tests.csproj -c Release --no-restore --filter 'FullyQualifiedName~InstallationTransactionTests' --logger 'trx;LogFileName=r05-install-final.trx' --results-directory artifacts/install-transaction-core
