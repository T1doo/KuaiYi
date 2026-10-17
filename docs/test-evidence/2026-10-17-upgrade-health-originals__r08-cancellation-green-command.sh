export DOTNET_GENERATE_ASPNET_CERTIFICATE=false
source /workspace/.kuaiyi-setup/activate.sh
export DOTNET_GENERATE_ASPNET_CERTIFICATE=false
dotnet test tests/KuaiYi.Core.Tests/KuaiYi.Core.Tests.csproj --no-restore -c Release --filter 'FullyQualifiedName~BlockingHealthCancellationCallbackCannotHoldTheUpgradeCallerBeyondItsOwnedBudget' --logger 'trx;LogFileName=r08-cancellation-green.trx' --results-directory artifacts/upgrade-health-rollback
