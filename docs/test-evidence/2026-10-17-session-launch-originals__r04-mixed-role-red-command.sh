export DOTNET_GENERATE_ASPNET_CERTIFICATE=false
source /workspace/.kuaiyi-setup/activate.sh
export DOTNET_GENERATE_ASPNET_CERTIFICATE=false
dotnet test tests/KuaiYi.Core.Tests/KuaiYi.Core.Tests.csproj --no-restore -c Release --filter 'FullyQualifiedName~ASessionCannotStealOrDisposeTheEarlierRoleHeldByTheSameCoordinator' --logger 'trx;LogFileName=r04-mixed-role-red.trx' --results-directory artifacts/installed-session-launch
