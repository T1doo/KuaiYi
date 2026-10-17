export DOTNET_GENERATE_ASPNET_CERTIFICATE=false
source /workspace/.kuaiyi-setup/activate.sh
export DOTNET_GENERATE_ASPNET_CERTIFICATE=false
 dotnet test tests/KuaiYi.Core.Tests/KuaiYi.Core.Tests.csproj -c Release --no-restore --filter FullyQualifiedName~DiagnosticDisposeSettlementTests|FullyQualifiedName~SelectionReuseHostTests|FullyQualifiedName~HostTerminationTests --logger "trx;LogFileName=r04-related.trx" --results-directory artifacts/diagnostic-dispose-settlement > artifacts/diagnostic-dispose-settlement/r04-related.log 2>&1
