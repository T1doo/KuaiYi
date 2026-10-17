export DOTNET_GENERATE_ASPNET_CERTIFICATE=false
source /workspace/.kuaiyi-setup/activate.sh
export DOTNET_GENERATE_ASPNET_CERTIFICATE=false
dotnet test tests/KuaiYi.Provider.Tests/KuaiYi.Provider.Tests.csproj -c Release --no-restore --filter 'FullyQualifiedName~AChangedDisplayContextBeforeTheYieldContinuationNeverStartsNativeCapture' --logger 'trx;LogFileName=r04-context-red.trx' --results-directory artifacts/screenshot-recapture-recovery
