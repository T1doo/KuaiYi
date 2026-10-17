export DOTNET_GENERATE_ASPNET_CERTIFICATE=false
source /workspace/.kuaiyi-setup/activate.sh
export DOTNET_GENERATE_ASPNET_CERTIFICATE=false
dotnet test tests/KuaiYi.Core.Tests/KuaiYi.Core.Tests.csproj --no-restore -c Release --filter 'FullyQualifiedName~RetryJoinsTheOriginalLifetimeStopTaskAfterActualExitBeforeRestoringTheOldGeneration' --logger 'trx;LogFileName=r11-stop-task-red.trx' --results-directory /workspace/KuaiYi/artifacts/runtime-ownership-handoff
