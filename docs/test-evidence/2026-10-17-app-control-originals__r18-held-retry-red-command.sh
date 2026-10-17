export DOTNET_GENERATE_ASPNET_CERTIFICATE=false
source /workspace/.kuaiyi-setup/activate.sh
export DOTNET_GENERATE_ASPNET_CERTIFICATE=false
dotnet test tests/KuaiYi.Core.Tests/KuaiYi.Core.Tests.csproj --no-restore -c Release --filter 'FullyQualifiedName~RetryReceivedBeforeTheOriginalFailedCloseTaskCompletesIsRetainedAndSerialized' --logger 'trx;LogFileName=r18-held-retry-red.trx' --results-directory artifacts/app-installation-control
