export DOTNET_GENERATE_ASPNET_CERTIFICATE=false
source /workspace/.kuaiyi-setup/activate.sh
export DOTNET_GENERATE_ASPNET_CERTIFICATE=false
dotnet test tests/KuaiYi.Provider.Tests/KuaiYi.Provider.Tests.csproj -c Release --no-restore --filter FullyQualifiedName~UnsafeNonProfileSavePreservesOriginal --logger "trx;LogFileName=r25-parent-fallback-control.trx" --results-directory artifacts/display-save-stream-continuation > artifacts/display-save-stream-continuation/r25-parent-fallback-control.log 2>&1
