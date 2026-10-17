export DOTNET_GENERATE_ASPNET_CERTIFICATE=false
source /workspace/.kuaiyi-setup/activate.sh
export DOTNET_GENERATE_ASPNET_CERTIFICATE=false
dotnet test tests/KuaiYi.Core.Tests/KuaiYi.Core.Tests.csproj --no-restore -c Release --filter 'FullyQualifiedName~StopReceivedWhileProductionShellCleanupWaitsIsRetriedAfterThatExactCleanupFails' --logger 'trx;LogFileName=r01-production-close-red.trx' --results-directory artifacts/app-close-pending-retry
