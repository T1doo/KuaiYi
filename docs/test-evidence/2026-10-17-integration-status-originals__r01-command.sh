export DOTNET_GENERATE_ASPNET_CERTIFICATE=false
source /workspace/.kuaiyi-setup/activate.sh
export DOTNET_GENERATE_ASPNET_CERTIFICATE=false
dotnet test tests/KuaiYi.Core.Tests/KuaiYi.Core.Tests.csproj -c Release --no-restore --filter FullyQualifiedName~IntegrationStatusTests --logger "trx;LogFileName=r01-focused.trx" --results-directory artifacts/integration-status-page > artifacts/integration-status-page/r01-focused.log 2>&1
