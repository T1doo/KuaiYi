export DOTNET_GENERATE_ASPNET_CERTIFICATE=false
source /workspace/.kuaiyi-setup/activate.sh
export DOTNET_GENERATE_ASPNET_CERTIFICATE=false
dotnet test tests/KuaiYi.Core.Tests/KuaiYi.Core.Tests.csproj --no-restore -c Release --filter 'FullyQualifiedName~InstallationHandoffTests' --logger 'trx;LogFileName=r07-handoff.trx' --results-directory /workspace/KuaiYi/artifacts/runtime-ownership-handoff
