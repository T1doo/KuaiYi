export DOTNET_GENERATE_ASPNET_CERTIFICATE=false
source /workspace/.kuaiyi-setup/activate.sh
export DOTNET_GENERATE_ASPNET_CERTIFICATE=false
 dotnet test tests/KuaiYi.Core.Tests/KuaiYi.Core.Tests.csproj -c Release --no-restore --filter FullyQualifiedName~DiagnosticDisposeSettlementTests --logger "trx;LogFileName=r01-parent-red.trx" --results-directory artifacts/diagnostic-dispose-settlement > artifacts/diagnostic-dispose-settlement/r01-parent-red.log 2>&1
