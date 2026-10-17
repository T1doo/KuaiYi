export DOTNET_GENERATE_ASPNET_CERTIFICATE=false
source /workspace/.kuaiyi-setup/activate.sh
export DOTNET_GENERATE_ASPNET_CERTIFICATE=false
dotnet test tests/KuaiYi.Provider.Tests/KuaiYi.Provider.Tests.csproj --no-restore -c Release --filter 'FullyQualifiedName~ShellCloseGateTests' --logger 'trx;LogFileName=r07-immediate-gate-red.trx' --results-directory artifacts/app-close-pending-retry
