export DOTNET_GENERATE_ASPNET_CERTIFICATE=false
source /workspace/.kuaiyi-setup/activate.sh
export DOTNET_GENERATE_ASPNET_CERTIFICATE=false
dotnet test tests/KuaiYi.Provider.Tests/KuaiYi.Provider.Tests.csproj -c Release --no-restore --filter FullyQualifiedName~TwoValidatorsCapturedAtSameOwner --logger "trx;LogFileName=r03-cas-red.trx" --results-directory artifacts/display-save-stream-continuation > artifacts/display-save-stream-continuation/r03-cas-red.log 2>&1
