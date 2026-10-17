export DOTNET_GENERATE_ASPNET_CERTIFICATE=false
source /workspace/.kuaiyi-setup/activate.sh
export DOTNET_GENERATE_ASPNET_CERTIFICATE=false
dotnet test tests/KuaiYi.Core.Tests/KuaiYi.Core.Tests.csproj --no-restore -c Release --filter 'FullyQualifiedName~HealthyReceiverLivesAfterActualControllerExitAndHasAnExplicitSelfCleanupRecipient' --logger 'trx;LogFileName=r04-legacy-red.trx' --results-directory /workspace/KuaiYi/artifacts/runtime-ownership-handoff
