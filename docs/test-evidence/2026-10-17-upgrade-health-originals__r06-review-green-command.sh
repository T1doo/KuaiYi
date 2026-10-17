export DOTNET_GENERATE_ASPNET_CERTIFICATE=false
source /workspace/.kuaiyi-setup/activate.sh
export DOTNET_GENERATE_ASPNET_CERTIFICATE=false
dotnet test tests/KuaiYi.Core.Tests/KuaiYi.Core.Tests.csproj --no-restore -c Release --filter 'FullyQualifiedName~AnotherCoordinatorCannotStartOrUpgradeOverALiveOwnedApplication|FullyQualifiedName~AChildThatAlreadyExitedStillMustSettleItsOriginalStderrBeforePublication' --logger 'trx;LogFileName=r06-review-green.trx' --results-directory artifacts/upgrade-health-rollback
